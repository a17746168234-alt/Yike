import Foundation
import Security

struct KeychainTextStore {
    let service: String
    let account: String

    func load() throws -> String? {
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: account,
            kSecReturnData as String: true,
            kSecMatchLimit as String: kSecMatchLimitOne
        ]
        var item: CFTypeRef?
        let status = SecItemCopyMatching(query as CFDictionary, &item)
        if status == errSecItemNotFound { return nil }
        guard status == errSecSuccess else { throw KeychainStoreError(status: status) }
        guard let data = item as? Data,
              let value = String(data: data, encoding: .utf8) else {
            throw KeychainStoreError.invalidData
        }
        return value
    }

    func save(_ value: String) throws {
        guard let data = value.data(using: .utf8) else {
            throw KeychainStoreError.invalidData
        }
        let key: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: account
        ]
        let attributes: [String: Any] = [
            kSecValueData as String: data
        ]
        let updateStatus = SecItemUpdate(key as CFDictionary, attributes as CFDictionary)
        if updateStatus == errSecSuccess { return }
        guard updateStatus == errSecItemNotFound else {
            throw KeychainStoreError(status: updateStatus)
        }

        var newItem = key
        attributes.forEach { newItem[$0.key] = $0.value }
        let addStatus = SecItemAdd(newItem as CFDictionary, nil)
        guard addStatus == errSecSuccess else { throw KeychainStoreError(status: addStatus) }
    }

    func delete() throws {
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: account
        ]
        let status = SecItemDelete(query as CFDictionary)
        guard status == errSecSuccess || status == errSecItemNotFound else {
            throw KeychainStoreError(status: status)
        }
    }
}

enum KeychainStoreError: LocalizedError {
    case invalidData
    case status(OSStatus)

    init(status: OSStatus) {
        self = .status(status)
    }

    var errorDescription: String? {
        switch self {
        case .invalidData:
            return "钥匙串数据格式无效"
        case .status(let status):
            return (SecCopyErrorMessageString(status, nil) as String?) ?? "钥匙串错误（\(status)）"
        }
    }
}

enum SecureKeyStore {
    // Separate QA bundles must never read, migrate or overwrite the user's key.
    static let applicationID = Bundle.main.bundleIdentifier ?? "com.yijian.translator.kimi"
    private static let store = KeychainTextStore(
        service: applicationID + ".deepl",
        account: "deepl-api-key"
    )

    private static var legacyKeyFileURL: URL? {
        guard applicationID == "com.yijian.translator.kimi" else { return nil }
        return FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)
            .first?
            .appendingPathComponent("Mac翻译", isDirectory: true)
            .appendingPathComponent("deepl-api-key")
    }

    static func loadDeepLKey() -> String {
        if let value = try? store.load()?.trimmingCharacters(in: .whitespacesAndNewlines),
           !value.isEmpty {
            return value
        }

        // One-time migration from builds that stored the key in Application Support.
        guard let legacyKeyFileURL,
              let legacyValue = try? String(contentsOf: legacyKeyFileURL, encoding: .utf8)
                .trimmingCharacters(in: .whitespacesAndNewlines),
              !legacyValue.isEmpty else { return "" }
        do {
            try store.save(legacyValue)
            try? FileManager.default.removeItem(at: legacyKeyFileURL)
            return legacyValue
        } catch {
            // Preserve the old file if migration fails so the user does not lose the key.
            return legacyValue
        }
    }

    static func saveDeepLKey(_ value: String) throws {
        let cleanValue = value.trimmingCharacters(in: .whitespacesAndNewlines)
        if cleanValue.isEmpty {
            try store.delete()
        } else {
            try store.save(cleanValue)
        }
        if let legacyKeyFileURL {
            try? FileManager.default.removeItem(at: legacyKeyFileURL)
        }
    }
}
