import SwiftUI
import Foundation

struct TrialAccount: Codable {
    let username: String
    let granted: Int
    let used: Int
    let remaining: Int
}
struct TrialConfiguration: Decodable {
    let enabled: Bool
    let gift: Int
    let message: String
    let pool_limit: Int
    let pool_remaining: Int
}
private struct TrialHTTPFailure: Decodable { let code: String; let message: String }

struct TrialServiceError: LocalizedError {
    let code: String
    let message: String
    var errorDescription: String? { message }
}

@MainActor
final class SharedTrialAccount: ObservableObject {
    static let shared = SharedTrialAccount()
    @Published private(set) var account: TrialAccount?
    @Published private(set) var configuration: TrialConfiguration?
    @Published private(set) var busy = false
    @Published var feedback = ""
    @Published var feedbackIsError = false
    private var token = ""
    private var authRevision = UUID()
    private let keychain = KeychainTextStore(service: SecureKeyStore.applicationID + ".yike-account", account: "session")
    private var pendingRequests: [Data: String] = [:]
    var isSignedIn: Bool { !token.isEmpty }
    private var baseURL: URL? {
        guard let value = Bundle.main.object(forInfoDictionaryKey: "YikeTrialAPIBaseURL") as? String,
              let url = URL(string: value), url.scheme == "https", url.host != nil else { return nil }
        return url
    }
    init() { token = (try? keychain.load()) ?? "" }

    private func request<T: Decodable>(_ path: String, method: String = "GET", body: [String: Any]? = nil, authenticated: Bool = true) async throws -> T {
        guard let baseURL else { throw TrialServiceError(code: "not_configured", message: "此版本尚未配置公共体验服务，请更新 Yike。") }
        var request = URLRequest(url: baseURL.appendingPathComponent(path))
        request.httpMethod = method
        request.timeoutInterval = 50
        request.cachePolicy = .reloadIgnoringLocalCacheData
        if authenticated { request.setValue("Bearer \(token)", forHTTPHeaderField: "Authorization") }
        if let body {
            request.setValue("application/json", forHTTPHeaderField: "Content-Type")
            request.httpBody = try JSONSerialization.data(withJSONObject: body)
        }
        do {
            let (data, response) = try await URLSession.shared.data(for: request)
            guard let http = response as? HTTPURLResponse else { throw URLError(.badServerResponse) }
            guard (200..<300).contains(http.statusCode) else {
                let failure = try? JSONDecoder().decode(TrialHTTPFailure.self, from: data)
                throw TrialServiceError(code: failure?.code ?? "http_\(http.statusCode)", message: failure?.message ?? "公共体验服务暂不可用（\(http.statusCode)），请稍后重试。")
            }
            return try JSONDecoder().decode(T.self, from: data)
        } catch let error as URLError where error.code != .cancelled {
            throw TrialServiceError(code: "network", message: "无法连接 Yike 公共体验服务，请检查网络后重试。Apple 翻译和自填密钥不受影响。")
        }
    }

    func refresh() async {
        let revision = authRevision
        do {
            let config: TrialConfiguration = try await request("v1/config", authenticated: false)
            guard revision == authRevision else { return }
            configuration = config
            if isSignedIn {
                struct Response: Decodable { let account: TrialAccount }
                let response: Response = try await request("v1/me")
                guard revision == authRevision else { return }
                account = response.account
            }
        } catch {
            guard revision == authRevision else { return }
            if (error as? TrialServiceError)?.code == "login_required" { clearSession() }
            feedback = error.localizedDescription; feedbackIsError = true
        }
    }

    func signIn(username: String, password: String, register: Bool) async {
        guard !busy else { return }
        busy = true; feedback = ""; defer { busy = false }
        do {
            struct Response: Decodable { let token: String; let account: TrialAccount; let message: String }
            let response: Response = try await request(register ? "v1/register" : "v1/login", method: "POST", body: ["username": username, "password": password], authenticated: false)
            try keychain.save(response.token)
            authRevision = UUID(); token = response.token; account = response.account; pendingRequests.removeAll()
            feedback = response.message; feedbackIsError = false
        } catch { feedback = error.localizedDescription; feedbackIsError = true }
    }

    private func clearSession() {
        authRevision = UUID(); token = ""; account = nil; pendingRequests.removeAll()
        try? keychain.delete()
    }
    func logout() async {
        guard !busy else { return }
        busy = true; defer { busy = false }
        struct Response: Decodable { let message: String }
        do {
            let _: Response = try await request("v1/logout", method: "POST", body: [:])
            clearSession(); feedback = "已退出登录。"; feedbackIsError = false
        } catch { feedback = "暂时无法撤销服务器会话，请联网后重试退出。"; feedbackIsError = true }
    }
    func changePassword(old: String, new: String) async {
        guard !busy else { return }
        busy = true; defer { busy = false }
        do {
            struct Response: Decodable { let message: String }
            let response: Response = try await request("v1/password", method: "POST", body: ["old_password":old,"new_password":new])
            clearSession(); feedback = response.message; feedbackIsError = false
        } catch { feedback = error.localizedDescription; feedbackIsError = true }
    }
    func translate(_ texts: [String], source: String, target: String) async throws -> [String] {
        guard isSignedIn else { throw TrialServiceError(code: "login_required", message: "请在设置的“账号与安全”中注册或登录，领取公共 DeepL 体验额度。") }
        let revision = authRevision
        let content: [String: Any] = ["text":texts,"source":source,"target":target]
        let fingerprint = try JSONSerialization.data(withJSONObject: content, options: [.sortedKeys])
        let id = pendingRequests[fingerprint] ?? UUID().uuidString
        pendingRequests[fingerprint] = id
        var body = content; body["request_id"] = id
        struct Response: Decodable { let translations: [String]; let account: TrialAccount }
        do {
            let result: Response = try await request("v1/translate", method: "POST", body: body)
            try Task.checkCancellation()
            guard revision == authRevision else { throw CancellationError() }
            account = result.account
            guard result.translations.count == texts.count else { throw TranslationFailure.invalidResponse }
            pendingRequests.removeValue(forKey: fingerprint)
            return result.translations
        } catch {
            // An explicit rejection may safely use a new request ID next time. Network
            // failures retain the ID so retries cannot double-charge the same request.
            if revision == authRevision, let failure = error as? TrialServiceError, failure.code != "network" {
                pendingRequests.removeValue(forKey: fingerprint)
            }
            throw error
        }
    }
}

struct TrialAccountSettings: View {
    @ObservedObject private var account = SharedTrialAccount.shared
    @ObservedObject var model: TranslatorViewModel
    @State private var registering = false
    @State private var username = ""
    @State private var password = ""
    @State private var confirmation = ""
    @State private var oldPassword = ""
    @State private var newPassword = ""
    @State private var changingPassword = false
    @State private var accepted = false
    var body: some View {
        VStack(alignment: .leading, spacing: 16) {
            if let current = account.account {
                Label(current.username, systemImage: "person.crop.circle")
                    .font(.system(size: 16, weight: .semibold))
                LabeledContent("我的体验余额") { Text("\(current.remaining.formatted()) / \(current.granted.formatted()) 字符") }
                ProgressView(value: Double(current.remaining), total: Double(max(1,current.granted)))
                Text("注册赠送 5 万字符，公共池总计 100 万字符。按实际使用先到先用，用完即止；不自动续赠。").font(.system(size: 12)).foregroundStyle(.secondary)
                HStack {
                    Button("使用公共 DeepL") { model.setEngine(.sharedDeepL) }
                        .disabled(account.configuration?.enabled != true || current.remaining == 0)
                    Button("刷新额度") { Task { await account.refresh() } }
                    Button("修改密码") { changingPassword.toggle() }
                    Button("退出登录") { model.setEngine(.apple); Task { await account.logout() } }
                }
                if changingPassword {
                    SecureField("原密码", text: $oldPassword)
                    SecureField("新密码（至少 10 位）", text: $newPassword)
                    Button("更新密码并重新登录") {
                        Task { await account.changePassword(old: oldPassword, new: newPassword); oldPassword = ""; newPassword = "" }
                    }.disabled(newPassword.count < 10)
                }
            } else {
                Text("注册领取 50,000 字符公共 DeepL 体验").font(.system(size: 15, weight: .semibold))
                Text("共享池共 1,000,000 字符，用完为止。Apple 翻译及使用自己的 API 密钥无需注册。").font(.system(size: 12)).foregroundStyle(.secondary)
                Picker("账号操作", selection: $registering) {
                    Text("登录").tag(false)
                    Text("注册账号").tag(true)
                }.pickerStyle(.segmented)
                TextField("账号名（4–24 位英文、数字或下划线）", text: $username)
                SecureField("密码（至少 10 位）", text: $password)
                if registering {
                    SecureField("再次输入密码", text: $confirmation)
                    Toggle("我已了解公共池规则与下方的数据处理说明", isOn: $accepted).font(.system(size: 11))
                }
                Button(registering ? "注册并领取体验额度" : "登录账号") {
                    Task { await account.signIn(username: username.trimmingCharacters(in: .whitespacesAndNewlines), password: password, register: registering); password = ""; confirmation = "" }
                }.disabled(username.count < 4 || password.count < 10 || (registering && (!accepted || password != confirmation)))
                Text("请妥善保存账号和密码；当前不提供邮箱找回。").font(.system(size: 11)).foregroundStyle(.secondary)
            }
            if account.busy { ProgressView().controlSize(.small) }
            if !account.feedback.isEmpty {
                Label(account.feedback, systemImage: account.feedbackIsError ? "exclamationmark.circle" : "checkmark.circle")
                    .font(.system(size: 12)).foregroundStyle(account.feedbackIsError ? Color.orange : Color.green)
            }
            if let config = account.configuration {
                Divider()
                LabeledContent("公共池预算剩余") { Text("\(config.pool_remaining.formatted()) 字符") }
                if !config.enabled { Text(config.message).font(.system(size: 12)).foregroundStyle(.secondary) }
            }
            Divider()
            Text("公共翻译会将文字经 Yike 服务器发送到 DeepL。服务器保存账号、密码摘要和额度记录，不保存原文；为避免重复扣额，译文缓存有效期为 10 分钟，过期后定期清理。共享池也受 DeepL 实际可用额度限制。").font(.system(size: 11)).foregroundStyle(.secondary)
        }
        .textFieldStyle(.roundedBorder)
        .buttonStyle(.bordered)
        .disabled(account.busy)
        .task { await account.refresh() }
    }
}
