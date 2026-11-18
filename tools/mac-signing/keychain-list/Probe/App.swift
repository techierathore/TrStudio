import SwiftUI
import Security

// Keychain lister for TrStudio (proof tool, decision 15). Signed with the same Personal Team profile,
// app id and Keychain access group as TrStudio, so it sees the app's data-protection Keychain items.
// It prints each item's service, account and dates — never a value — then quits.
// Run: tools/mac-signing/keychain-list.sh
@main
struct ProbeApp: App {
    init() {
        // --delete-unlock-key: deletes TrStudio's unlock key item. Only for a proof run when Registry &
        // providers shows no stored key and no sign-in is remembered: anything encrypted with it is lost.
        if CommandLine.arguments.contains("--delete-unlock-key") {
            let del: [String: Any] = [
                kSecClass as String: kSecClassGenericPassword,
                kSecAttrAccount as String: "trstudio.keystore.unlock.v1",
                kSecUseDataProtectionKeychain as String: true,
            ]
            print("delete unlock key: OSStatus \(SecItemDelete(del as CFDictionary))")
        }
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecMatchLimit as String: kSecMatchLimitAll,
            kSecReturnAttributes as String: true,
            kSecUseDataProtectionKeychain as String: true,
        ]
        var result: AnyObject?
        let status = SecItemCopyMatching(query as CFDictionary, &result)
        var lines: [String] = []
        if status == errSecItemNotFound {
            lines.append("items: 0")
        } else if status != errSecSuccess {
            lines.append("keychain query failed: OSStatus \(status)")
        } else {
            let items = (result as? [[String: Any]]) ?? []
            lines.append("items: \(items.count)")
            let f = ISO8601DateFormatter()
            for i in items {
                let svc = i[kSecAttrService as String] as? String ?? "-"
                let acct = i[kSecAttrAccount as String] as? String ?? "-"
                let grp = i[kSecAttrAccessGroup as String] as? String ?? "-"
                let c = (i[kSecAttrCreationDate as String] as? Date).map { f.string(from: $0) } ?? "-"
                let m = (i[kSecAttrModificationDate as String] as? Date).map { f.string(from: $0) } ?? "-"
                lines.append("service=\(svc) account=\(acct) group=\(grp) created=\(c) modified=\(m)")
            }
        }
        print(lines.joined(separator: "\n"))
        fflush(stdout)
        exit(0)
    }

    var body: some Scene {
        WindowGroup { Text("keychain-list") }
    }
}
