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

@MainActor
final class UpdateManager: ObservableObject {
    static let shared = UpdateManager()

    @Published private(set) var latest: YikeUpdate?
    @Published private(set) var isChecking = false
    @Published private(set) var status = ""
    @Published var showsUpdateAlert = false
    @Published var showsInstallError = false

    private let lastCheckKey = "yike.update.lastCheck"
    private let interval: TimeInterval = 24 * 60 * 60

    var currentBuild: Int {
        Int(Bundle.main.object(forInfoDictionaryKey: "CFBundleVersion") as? String ?? "0") ?? 0
    }

    var hasUpdate: Bool { (latest?.build ?? 0) > currentBuild }

    func checkIfNeeded() async {
        let last = UserDefaults.standard.double(forKey: lastCheckKey)
        guard Date().timeIntervalSince1970 - last >= interval else { return }
        _ = await check(force: false)
    }

    enum CheckResult { case current, available, failed }

    func checkNow() async -> CheckResult? { await check(force: true) }

    func installNow() async {
        guard let update = latest, !isChecking else { return }
        isChecking = true
        status = "正在下载新版…"
        defer { isChecking = false }
        do {
            let request = URLRequest(url: update.downloadURL, cachePolicy: .reloadIgnoringLocalAndRemoteCacheData, timeoutInterval: 120)
            let (temporaryURL, response) = try await URLSession.shared.download(for: request)
            guard let http = response as? HTTPURLResponse, http.statusCode == 200 else { throw URLError(.badServerResponse) }
            let data = try Data(contentsOf: temporaryURL, options: .mappedIfSafe)
            let digest = SHA256.hash(data: data).map { String(format: "%02x", $0) }.joined()
            guard digest.caseInsensitiveCompare(update.sha256) == .orderedSame else { throw UpdateError.invalidChecksum }

            let directory = FileManager.default.temporaryDirectory.appendingPathComponent("YikeUpdate-\(UUID().uuidString)", isDirectory: true)
            try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
            let dmg = directory.appendingPathComponent("Yike.dmg")
            try FileManager.default.copyItem(at: temporaryURL, to: dmg)
            let helper = directory.appendingPathComponent("install.sh")
            try Self.installerScript.write(to: helper, atomically: true, encoding: .utf8)
            try FileManager.default.setAttributes([.posixPermissions: 0o700], ofItemAtPath: helper.path)

            let process = Process()
            process.executableURL = URL(fileURLWithPath: "/bin/zsh")
            process.arguments = [helper.path, String(ProcessInfo.processInfo.processIdentifier), dmg.path,
                                 Bundle.main.bundleURL.path, Bundle.main.bundleIdentifier ?? "com.yijian.translator.kimi"]
            try process.run()
            status = "正在安装，Yike 即将重新启动…"
            DispatchQueue.main.asyncAfter(deadline: .now() + 0.35) { NSApp.terminate(nil) }
        } catch {
            status = error is UpdateError ? "安装包校验失败，已保留当前版本。" : "更新失败，已保留当前版本，请稍后重试。"
            showsInstallError = true
        }
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
for _ in {1..100}; do
    kill -0 "$old_pid" 2>/dev/null || break
    sleep 0.1
done
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
/usr/bin/osascript -e 'display dialog "Yike 更新完毕。点击“知道了”后将重新启动。" buttons {"知道了"} default button "知道了" with title "Yike 更新完成"' >/dev/null 2>&1 || true
/usr/bin/open "$current_app"
"""#
}
