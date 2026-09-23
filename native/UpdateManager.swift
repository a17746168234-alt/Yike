import SwiftUI
import AppKit
import CryptoKit

struct YikeUpdate: Codable, Equatable {
    let version: String
    let build: Int
    let title: String
    let notes: String
    let downloadURL: URL
    let sha256: String

    enum CodingKeys: String, CodingKey {
        case version, build, title, notes, sha256
        case downloadURL = "download_url"
    }
}

struct YikeUpdateProgressView: View {
    @ObservedObject var updater: UpdateManager
    var body: some View {
        VStack(alignment: .leading, spacing: 16) {
            Text("Yike 更新").font(.title2.bold())
            Text(updater.status).font(.subheadline)
            if let progress = updater.downloadProgress {
                ProgressView(value: progress)
                Text("下载进度 \(Int(progress * 100))%")
                    .font(.caption).foregroundStyle(.secondary)
            } else if updater.isChecking { ProgressView() }
            HStack {
                Spacer()
                if updater.showsInstallReady {
                    Button("稍后再说") { updater.showsProgress = false }
                    Button("退出并重启 Yike") { updater.restartAndInstall() }.buttonStyle(.borderedProminent)
                } else if !updater.isChecking {
                    Button("关闭") { updater.showsProgress = false }
                }
            }
        }
        .padding(24)
        .frame(width: 390)
    }
}

private final class YikeDownloadDelegate: NSObject, URLSessionDownloadDelegate, @unchecked Sendable {
    let progressHandler: (Double) -> Void
    var continuation: CheckedContinuation<(URL, HTTPURLResponse), Error>?
    init(progressHandler: @escaping (Double) -> Void) { self.progressHandler = progressHandler }
    func urlSession(_ session: URLSession, downloadTask: URLSessionDownloadTask, didWriteData bytesWritten: Int64, totalBytesWritten: Int64, totalBytesExpectedToWrite: Int64) {
        guard totalBytesExpectedToWrite > 0 else { return }
        progressHandler(min(1, max(0, Double(totalBytesWritten) / Double(totalBytesExpectedToWrite))))
    }
    func urlSession(_ session: URLSession, downloadTask: URLSessionDownloadTask, didFinishDownloadingTo location: URL) {
        guard let response = downloadTask.response as? HTTPURLResponse else { continuation?.resume(throwing: URLError(.badServerResponse)); return }
        let target = FileManager.default.temporaryDirectory.appendingPathComponent("Yike-download-\(UUID().uuidString).dmg")
        do { try FileManager.default.copyItem(at: location, to: target); continuation?.resume(returning: (target, response)) }
        catch { continuation?.resume(throwing: error) }
        continuation = nil
    }
    func urlSession(_ session: URLSession, task: URLSessionTask, didCompleteWithError error: Error?) {
        if let error, continuation != nil { continuation?.resume(throwing: error); continuation = nil }
    }
}

@MainActor
final class UpdateManager: ObservableObject {
    static let shared = UpdateManager()

    @Published private(set) var latest: YikeUpdate?
    @Published private(set) var isChecking = false
    @Published private(set) var status = ""
    @Published private(set) var downloadProgress: Double?
    @Published var showsUpdateAlert = false
    @Published var showsInstallError = false
    @Published var showsInstallReady = false
    @Published var showsProgress = false
    @Published var showsUpdateComplete = false
    private var stagedDMG: URL?

    private let lastCheckKey = "yike.update.lastCheck"
    private let lastLaunchedBuildKey = "yike.update.lastLaunchedBuild"
    private let interval: TimeInterval = 24 * 60 * 60

    var currentBuild: Int {
        Int(Bundle.main.object(forInfoDictionaryKey: "CFBundleVersion") as? String ?? "0") ?? 0
    }

    var hasUpdate: Bool { (latest?.build ?? 0) > currentBuild }

    func checkIfNeeded() async {
        let pending = UserDefaults.standard.integer(forKey: "yike.update.pendingBuild")
        let previousBuild = UserDefaults.standard.integer(forKey: lastLaunchedBuildKey)
        let checkedInOlderBuild = UserDefaults.standard.double(forKey: lastCheckKey) > 0
        if pending > 0, currentBuild >= pending {
            UserDefaults.standard.removeObject(forKey: "yike.update.pendingBuild")
            showsUpdateComplete = true
        } else if previousBuild > 0, currentBuild > previousBuild {
            showsUpdateComplete = true
        } else if previousBuild == 0, currentBuild >= 75, checkedInOlderBuild {
            // Build 73/74 did not record a pending build before relaunch.
            showsUpdateComplete = true
        }
        UserDefaults.standard.set(currentBuild, forKey: lastLaunchedBuildKey)
        let last = UserDefaults.standard.double(forKey: lastCheckKey)
        guard Date().timeIntervalSince1970 - last >= interval else { return }
        _ = await check(force: false)
    }

    enum CheckResult { case current, available, failed }

    func checkNow() async -> CheckResult? { await check(force: true) }

    func installNow() async {
        guard let update = latest, !isChecking else { return }
        isChecking = true
        showsInstallReady = false
        if let stagedDMG { try? FileManager.default.removeItem(at: stagedDMG.deletingLastPathComponent()) }
        stagedDMG = nil
        showsProgress = true
        downloadProgress = 0
        status = "正在下载新版…"
        defer { isChecking = false }
        do {
            let request = URLRequest(url: update.downloadURL, cachePolicy: .reloadIgnoringLocalAndRemoteCacheData, timeoutInterval: 120)
            let delegate = YikeDownloadDelegate { [weak self] value in Task { @MainActor in self?.downloadProgress = value } }
            let session = URLSession(configuration: .default, delegate: delegate, delegateQueue: nil)
            let (temporaryURL, response) = try await withCheckedThrowingContinuation { (continuation: CheckedContinuation<(URL, HTTPURLResponse), Error>) in
                delegate.continuation = continuation
                session.downloadTask(with: request).resume()
            }
            session.invalidateAndCancel()
            guard response.statusCode == 200 else { throw URLError(.badServerResponse) }
            let data = try Data(contentsOf: temporaryURL, options: .mappedIfSafe)
            status = "正在校验安装包…"
            let digest = SHA256.hash(data: data).map { String(format: "%02x", $0) }.joined()
            guard digest.caseInsensitiveCompare(update.sha256) == .orderedSame else { throw UpdateError.invalidChecksum }

            let directory = FileManager.default.temporaryDirectory.appendingPathComponent("YikeUpdate-\(UUID().uuidString)", isDirectory: true)
            try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
            let dmg = directory.appendingPathComponent("Yike.dmg")
            try FileManager.default.copyItem(at: temporaryURL, to: dmg)
            try? FileManager.default.removeItem(at: temporaryURL)
            stagedDMG = dmg
            downloadProgress = 1
            status = "更新包下载并校验完成，可以退出并重启 Yike。"
            showsInstallReady = true
        } catch {
            downloadProgress = nil
            status = error is UpdateError ? "安装包校验失败，已保留当前版本。" : "更新失败：\(error.localizedDescription) 已保留当前版本，请稍后重试。"
            showsInstallError = true
        }
    }

    func restartAndInstall() {
        guard let dmg = stagedDMG else { return }
        do {
            let directory = dmg.deletingLastPathComponent()
            let helper = directory.appendingPathComponent("install.sh")
            try Self.installerScript.write(to: helper, atomically: true, encoding: .utf8)
            try FileManager.default.setAttributes([.posixPermissions: 0o700], ofItemAtPath: helper.path)
            let process = Process(); process.executableURL = URL(fileURLWithPath: "/bin/zsh")
            process.arguments = [helper.path, String(ProcessInfo.processInfo.processIdentifier), dmg.path, Bundle.main.bundleURL.path, Bundle.main.bundleIdentifier ?? "com.yijian.translator.kimi"]
            try process.run(); status = "正在退出并重启 Yike…"
            if let latest { UserDefaults.standard.set(latest.build, forKey: "yike.update.pendingBuild") }
            DispatchQueue.main.asyncAfter(deadline: .now() + 0.35) { NSApp.terminate(nil) }
        } catch { status = "无法启动重启流程，请重新下载更新。"; showsInstallError = true }
    }

    private func check(force: Bool) async -> CheckResult? {
        guard !isChecking else { return nil }
        isChecking = true
        status = force ? "正在检查更新…" : ""
        defer { isChecking = false }

        guard let base = Bundle.main.object(forInfoDictionaryKey: "YikeTrialAPIBaseURL") as? String,
              let url = URL(string: base)?.appendingPathComponent("v1/update/macos") else {
            if force { status = "更新服务地址无效。" }
            return .failed
        }

        var request = URLRequest(url: url)
        request.timeoutInterval = 10
        request.cachePolicy = .reloadIgnoringLocalAndRemoteCacheData
        do {
            let (data, response) = try await URLSession.shared.data(for: request)
            guard let http = response as? HTTPURLResponse, http.statusCode == 200 else {
                throw URLError(.badServerResponse)
            }
            let update = try JSONDecoder().decode(YikeUpdate.self, from: data)
            latest = update
            UserDefaults.standard.set(Date().timeIntervalSince1970, forKey: lastCheckKey)
            if update.build > currentBuild {
                status = "发现新版本 \(update.version)（Build \(update.build)）"
                if !force { showsUpdateAlert = true }
                return .available
            } else {
                status = "当前已是最新版。"
                return .current
            }
        } catch {
            if force { status = "暂时无法检查更新，请确认网络后重试。" }
            return .failed
        }
    }

    private enum UpdateError: Error { case invalidChecksum }

    private static let installerScript = #"""
#!/bin/zsh
set -u
old_pid="$1"
dmg="$2"
current_app="$3"
expected_id="$4"
for _ in {1..300}; do
    kill -0 "$old_pid" 2>/dev/null || break
    sleep 0.1
done
if kill -0 "$old_pid" 2>/dev/null; then exit 1; fi
mount_dir="$(mktemp -d /tmp/yike-update-mount.XXXXXX)" || exit 1
backup_root="$(mktemp -d /tmp/yike-update-backup.XXXXXX)" || exit 1
backup_app="$backup_root/Yike.app"
cleanup() {
    /usr/bin/hdiutil detach "$mount_dir" -quiet 2>/dev/null || true
    /bin/rm -rf "$mount_dir" "$backup_root" "$(dirname "$dmg")"
}
trap cleanup EXIT
/usr/bin/hdiutil verify "$dmg" >/dev/null || exit 1
/usr/bin/hdiutil attach "$dmg" -nobrowse -readonly -mountpoint "$mount_dir" >/dev/null || exit 1
new_app="$mount_dir/Yike.app"
actual_id="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleIdentifier' "$new_app/Contents/Info.plist" 2>/dev/null)"
[[ "$actual_id" == "$expected_id" ]] || exit 1
/usr/bin/codesign --verify --deep --strict "$new_app" || exit 1
/usr/bin/ditto "$current_app" "$backup_app" || exit 1
/bin/rm -rf "$current_app" || exit 1
if ! /usr/bin/ditto "$new_app" "$current_app" || ! /usr/bin/codesign --verify --deep --strict "$current_app"; then
    /bin/rm -rf "$current_app"
    /usr/bin/ditto "$backup_app" "$current_app"
    /usr/bin/osascript -e 'display dialog "Yike 更新失败，已恢复原版本。" buttons {"知道了"} with title "Yike 更新"' >/dev/null 2>&1 || true
    exit 1
fi
/usr/bin/open "$current_app"
"""#
}
