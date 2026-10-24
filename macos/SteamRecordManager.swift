import AppKit
import Foundation
import CryptoKit

// Native macOS preview. It intentionally never edits Steam indexes or deletes permanently.
enum UIStrings {
    static var language = UserDefaults.standard.integer(forKey: "language")
    static func text(_ traditional: String, _ simplified: String, _ english: String) -> String {
        [traditional, simplified, english][max(0, min(2, language))]
    }
}
func tr(_ tw: String, _ cn: String, _ en: String) -> String { UIStrings.text(tw, cn, en) }
struct ManagerError: LocalizedError {
    let message: String
    var errorDescription: String? { message }
    init(_ message: String) { self.message = message }
}
enum RecordingKind: String {
    case background = "bg", manual = "fg", clip = "clip"
    var label: String {
        switch self {
        case .background: return tr("背景錄影", "后台录像", "Background")
        case .manual: return tr("手動錄影", "手动录像", "Manual")
        case .clip: return tr("剪輯", "剪辑", "Clip")
        }
    }
}
struct Session {
    let url: URL
    let appID: String
    let game: String
    let kind: RecordingKind
    let bytes: Int64
    let modified: Date
    let fingerprint: String
    var id: String { url.path }
}
struct ScanResult { var sessions: [Session]; var skipped: [String] }
enum RecordingCore {
    static let fm = FileManager.default
    static let gib = 1_073_741_824.0
    static func inside(_ candidate: URL, root: URL) -> Bool {
        let path = candidate.standardizedFileURL.path
        let base = root.standardizedFileURL.path
        return path == base || path.hasPrefix(base.hasSuffix("/") ? base : base + "/")
    }
    static func noLinks(_ url: URL) throws {
        var current = url.standardizedFileURL
        while true {
            let attrs = try fm.attributesOfItem(atPath: current.path)
            if attrs[.type] as? FileAttributeType == .typeSymbolicLink {
                throw ManagerError(tr("不支援符號連結：", "不支持符号链接：", "Symbolic links are not supported: ") + current.path)
            }
            if current.path == "/" { break }
            let parent = current.deletingLastPathComponent()
            if parent.path == current.path { break }
            current = parent
        }
    }
    static func rootCheck(_ root: URL) throws {
        guard root.isFileURL else { throw ManagerError("A local folder is required.") }
        try noLinks(root)
        if try root.resourceValues(forKeys: [.volumeIsLocalKey]).volumeIsLocal == false {
            throw ManagerError(tr("只支援本機磁碟。", "只支持本地磁盘。", "Only local volumes are supported."))
        }
        for name in ["video", "clips"] {
            let child = root.appendingPathComponent(name, isDirectory: true)
            try noLinks(child)
            guard try child.resourceValues(forKeys: [.isDirectoryKey]).isDirectory == true else {
                throw ManagerError(tr("請選擇含 video 與 clips 的錄影根目錄。", "请选择含 video 与 clips 的录像根目录。", "Select the recording root containing video and clips."))
            }
        }
    }
    static func inspect(_ url: URL, names: [String: String]) throws -> Session {
        let pattern = #"^(bg|fg|clip)_([0-9]+)_([0-9]{8})_([0-9]{6})$"#
        let regex = try NSRegularExpression(pattern: pattern)
        let name = url.lastPathComponent
        guard let match = regex.firstMatch(in: name, range: NSRange(name.startIndex..., in: name)),
              let kindRange = Range(match.range(at: 1), in: name),
              let appRange = Range(match.range(at: 2), in: name),
              let kind = RecordingKind(rawValue: String(name[kindRange])) else {
            throw ManagerError(tr("未知資料夾：", "未知文件夹：", "Unrecognized folder: ") + name)
        }
        let app = String(name[appRange])
        var stack = [url], signatures: [String] = [], bytes: Int64 = 0
        var latest = Date.distantPast
        while let directory = stack.popLast() {
            try noLinks(directory)
            let attributes = try fm.attributesOfItem(atPath: directory.path)
            guard attributes[.type] as? FileAttributeType == .typeDirectory else { throw ManagerError("Expected directory.") }
            let date = attributes[.modificationDate] as? Date ?? .distantPast
            latest = max(latest, date)
            signatures.append("D|\(directory.path)|\(date.timeIntervalSince1970.bitPattern)")
            for file in try fm.contentsOfDirectory(at: directory, includingPropertiesForKeys: nil) {
                let attrs = try fm.attributesOfItem(atPath: file.path)
                if attrs[.type] as? FileAttributeType == .typeDirectory { stack.append(file); continue }
                guard attrs[.type] as? FileAttributeType == .typeRegular,
                      let length = attrs[.size] as? NSNumber else {
                    throw ManagerError(tr("包含連結或特殊檔案。", "包含链接或特殊文件。", "Contains a link or special file."))
                }
                let addition = bytes.addingReportingOverflow(length.int64Value)
                guard !addition.overflow, length.int64Value >= 0 else { throw ManagerError("Invalid file size.") }
                bytes = addition.partialValue
                let modified = attrs[.modificationDate] as? Date ?? .distantPast
                latest = max(latest, modified)
                signatures.append("F|\(file.path)|\(length.int64Value)|\(modified.timeIntervalSince1970.bitPattern)")
            }
        }
        let digest = SHA256.hash(data: try JSONEncoder().encode(signatures.sorted())).map { String(format: "%02x", $0) }.joined()
        return Session(url: url, appID: app, game: names[app] ?? "App \(app)", kind: kind, bytes: bytes, modified: latest, fingerprint: digest)
    }
    static func scan(_ root: URL) throws -> ScanResult {
        try rootCheck(root)
        let names = gameNames()
        var result = ScanResult(sessions: [], skipped: [])
        for category in ["video", "clips"] {
            for url in try fm.contentsOfDirectory(at: root.appendingPathComponent(category), includingPropertiesForKeys: [.isDirectoryKey]) {
                do {
                    let session = try inspect(url, names: names)
                    guard (category == "clips") == (session.kind == .clip) else { throw ManagerError("Folder/type mismatch.") }
                    result.sessions.append(session)
                } catch { result.skipped.append(url.lastPathComponent + ": " + error.localizedDescription) }
            }
        }
        return result
    }
    static func plan(_ sessions: [Session], protected: Set<String>, limit: Double, days: Int, now: Date = Date()) -> Set<String> {
        var remaining = sessions.filter { $0.kind == .background }.reduce(0.0) { $0 + Double($1.bytes) }
        var selected = Set<String>()
        for session in sessions.sorted(by: { $0.modified < $1.modified }) {
            guard session.kind == .background, !protected.contains(session.id), session.modified < now.addingTimeInterval(-600) else { continue }
            if (days > 0 && session.modified < now.addingTimeInterval(-Double(days) * 86400)) || (limit > 0 && remaining > limit * gib) {
                selected.insert(session.id); remaining -= Double(session.bytes)
            }
        }
        return selected
    }
    static func validate(_ session: Session, root: URL, protected: Set<String>) throws {
        try rootCheck(root)
        let category = session.kind == .clip ? "clips" : "video"
        guard session.url.deletingLastPathComponent().standardizedFileURL.path == root.appendingPathComponent(category).standardizedFileURL.path,
              !protected.contains(session.id) else { throw ManagerError("Protected item or outside recording root.") }
        let current = try inspect(session.url, names: [:])
        guard current.fingerprint == session.fingerprint, current.kind == session.kind, current.bytes == session.bytes,
              current.modified == session.modified, current.modified < Date().addingTimeInterval(-600) else {
            throw ManagerError(tr("錄影已變動或仍太新，請重新掃描並等待 10 分鐘。", "录像已更改或仍太新，请重新扫描并等待 10 分钟。", "Recording changed or is too recent. Rescan and wait 10 minutes."))
        }
    }
    static var steamRunning: Bool {
        NSWorkspace.shared.runningApplications.contains {
            $0.bundleIdentifier == "com.valvesoftware.steam" || ["steam_osx", "steam"].contains($0.executableURL?.lastPathComponent ?? "")
        }
    }
    static func trash(_ session: Session, root: URL, protected: Set<String>) throws {
        guard !steamRunning else { throw ManagerError(tr("請完全退出 Steam 後再清理。", "请完全退出 Steam 后再清理。", "Fully exit Steam before cleanup.")) }
        try validate(session, root: root, protected: protected)
        try fm.trashItem(at: session.url, resultingItemURL: nil)
    }
    static func gameNames() -> [String: String] {
        let steam = fm.homeDirectoryForCurrentUser.appendingPathComponent("Library/Application Support/Steam")
        var result = (try? AppInfoReader.names(at: steam.appendingPathComponent("appcache/appinfo.vdf"))) ?? [:]
        var libraries = [steam]
        let libraryFile = steam.appendingPathComponent("steamapps/libraryfolders.vdf")
        if let size = (try? fm.attributesOfItem(atPath: libraryFile.path)[.size]) as? NSNumber, size.intValue < 4_194_304,
           let text = try? String(contentsOf: libraryFile, encoding: .utf8), let regex = try? NSRegularExpression(pattern: #""path"\s+"([^"]+)""#) {
            for match in regex.matches(in: text, range: NSRange(text.startIndex..., in: text)) {
                if let range = Range(match.range(at: 1), in: text) { libraries.append(URL(fileURLWithPath: String(text[range]))) }
            }
        }
        for library in libraries {
            guard (try? library.resourceValues(forKeys: [.volumeIsLocalKey]).volumeIsLocal) != false else { continue }
            for file in (try? fm.contentsOfDirectory(at: library.appendingPathComponent("steamapps"), includingPropertiesForKeys: nil)) ?? [] where file.lastPathComponent.hasPrefix("appmanifest_") && file.pathExtension == "acf" {
                guard let size = (try? fm.attributesOfItem(atPath: file.path)[.size]) as? NSNumber, size.intValue < 4_194_304,
                      let text = try? String(contentsOf: file, encoding: .utf8) else { continue }
                func value(_ key: String) -> String? {
                    guard let regex = try? NSRegularExpression(pattern: "\"" + key + "\"\\s+\"([^\"]+)\""), let match = regex.firstMatch(in: text, range: NSRange(text.startIndex..., in: text)), let range = Range(match.range(at: 1), in: text) else { return nil }
                    return String(text[range])
                }
                if let id = value("appid"), let name = value("name") { result[id] = name }
            }
        }
        return result
    }
}

// Name-only appinfo v39/v40/v41 reader. It never writes the Steam cache.
final class AppInfoReader {
    let bytes: [UInt8]; var p = 0; var keys: [String]?
    init(_ data: Data) { bytes = Array(data) }
    func number(_ count: Int, end: Int) throws -> UInt64 {
        guard count >= 0, p <= end - count, end <= bytes.count else { throw ManagerError("Truncated appinfo.") }
        var value: UInt64 = 0
        for index in 0..<count { value |= UInt64(bytes[p + index]) << (index * 8) }
        p += count; return value
    }
    func string(end: Int) throws -> String {
        let start = p
        while p < end && p - start < 1_048_576 {
            if bytes[p] == 0 { let text = String(decoding: bytes[start..<p], as: UTF8.self); p += 1; return text }
            p += 1
        }
        throw ManagerError("Invalid appinfo string.")
    }
    func object(end: Int, path: String = "", depth: Int = 0) throws -> String? {
        guard depth < 64 else { throw ManagerError("Appinfo nesting limit.") }
        while p < end {
            let type = try number(1, end: end)
            if type == 8 { return nil }
            let key: String
            if let table = keys {
                let index = try number(4, end: end)
                guard index < UInt64(table.count) else { throw ManagerError("Invalid appinfo key.") }; key = table[Int(index)]
            } else { key = try string(end: end) }
            let child = path.isEmpty ? key : path + "/" + key
            if type == 0 { if let value = try object(end: end, path: child, depth: depth + 1) { return value } }
            else if type == 1 { let value = try string(end: end); if child == "appinfo/common/name" && !value.isEmpty { return value } }
            else {
                var count: Int
                switch type {
                case 2, 3, 4, 6: count = 4
                case 7, 10: count = 8
                case 5: count = Int(try number(2, end: end)) * 2
                default: throw ManagerError("Unsupported appinfo value.")
                }
                guard p <= end - count else { throw ManagerError("Truncated appinfo value.") }; p += count
            }
        }
        throw ManagerError("Truncated appinfo object.")
    }
    static func names(at url: URL) throws -> [String: String] {
        guard let size = (try RecordingCore.fm.attributesOfItem(atPath: url.path)[.size]) as? NSNumber, size.intValue <= 134_217_728 else { throw ManagerError("Appinfo size limit.") }
        return try AppInfoReader(Data(contentsOf: url)).read()
    }
    func read() throws -> [String: String] {
        let magic = try number(4, end: bytes.count), version = magic & 255
        guard magic >> 8 == 0x075644, (39...41).contains(version) else { throw ManagerError("Unsupported appinfo format.") }
        _ = try number(4, end: bytes.count)
        var end = bytes.count
        if version == 41 {
            let offset = try number(8, end: end), start = p
            guard end >= 4, offset >= UInt64(start), offset <= UInt64(end - 4) else { throw ManagerError("Invalid string table.") }
            p = Int(offset); end = p
            let count = try number(4, end: bytes.count)
            guard count <= 1_000_000, count <= UInt64(bytes.count - p) else { throw ManagerError("String-table limit.") }
            var table: [String] = []
            for _ in 0..<count { table.append(try string(end: bytes.count)) }
            keys = table; p = start
        }
        var result: [String: String] = [:]
        while p <= end - 4 {
            let id = try number(4, end: end); if id == 0 { break }
            let size = try number(4, end: end), header = version >= 40 ? 60 : 40
            guard size >= UInt64(header), size <= UInt64(end - p) else { throw ManagerError("Invalid appinfo record.") }
            let next = p + Int(size); p += header
            if let name = try? object(end: next) { result[String(id)] = name }
            p = next
        }
        return result
    }
}

final class ManagerApp: NSObject, NSApplicationDelegate, NSWindowDelegate, NSTableViewDataSource, NSTableViewDelegate, NSTextFieldDelegate {
    var window: NSWindow!
    let table = NSTableView(), rootField = NSTextField(), search = NSTextField(), cap = NSTextField(string: "100"), days = NSTextField(string: "30")
    let summary = NSTextField(labelWithString: ""), status = NSTextField(labelWithString: ""), titleLabel = NSTextField(labelWithString: "")
    let language = NSPopUpButton(), showClips = NSButton(checkboxWithTitle: "", target: nil, action: nil)
    var controls: [NSControl] = [], captions: [(NSButton, () -> String)] = []
    var sessions: [Session] = [], skipped: [String] = [], chosen = Set<String>(), protected = Set(UserDefaults.standard.stringArray(forKey: "protectedPaths") ?? [])
    var scannedRoot: URL?, busy = false
    var visible: [Session] {
        sessions.filter { (showClips.state == .on || $0.kind == .background) && (search.stringValue.isEmpty || ($0.game + " " + $0.appID + " " + $0.url.lastPathComponent).localizedCaseInsensitiveContains(search.stringValue)) }.sorted { $0.bytes > $1.bytes }
    }
    func applicationDidFinishLaunching(_ notification: Notification) { buildWindow(); NSApp.activate(ignoringOtherApps: true) }
    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool { true }
    func windowShouldClose(_ sender: NSWindow) -> Bool { if busy { alert(tr("請等候作業完成。", "请等待操作完成。", "Please wait for the operation to finish.")); return false }; return true }
    func button(_ caption: @escaping () -> String, _ action: Selector) -> NSButton {
        let button = NSButton(title: caption(), target: self, action: action)
        captions.append((button, caption)); controls.append(button); return button
    }
    func row(_ views: [NSView]) -> NSStackView { let view = NSStackView(views: views); view.orientation = .horizontal; view.spacing = 10; return view }
    func buildWindow() {
        window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 1250, height: 760), styleMask: [.titled, .closable, .miniaturizable, .resizable], backing: .buffered, defer: false)
        window.delegate = self; window.minSize = NSSize(width: 1100, height: 650); window.center()
        titleLabel.font = .boldSystemFont(ofSize: 21)
        language.addItems(withTitles: ["繁體中文", "简体中文", "English"]); language.selectItem(at: max(0, min(2, UIStrings.language))); language.target = self; language.action = #selector(changeLanguage)
        rootField.placeholderString = "video / clips"; search.delegate = self
        rootField.widthAnchor.constraint(greaterThanOrEqualToConstant: 700).isActive = true
        search.widthAnchor.constraint(equalToConstant: 300).isActive = true
        for field in [cap, days] { field.widthAnchor.constraint(equalToConstant: 70).isActive = true; let formatter = NumberFormatter(); formatter.minimum = 0; formatter.maximum = 100_000; formatter.allowsFloats = false; field.formatter = formatter }
        showClips.target = self; showClips.action = #selector(refreshAction)
        controls += [rootField, search, cap, days, language, showClips]
        let policy = row([NSTextField(labelWithString: "GiB"), cap, NSTextField(labelWithString: "Days"), days, button({ tr("預覽清理建議", "预览清理建议", "Preview cleanup") }, #selector(preview)), button({ tr("取消選取", "取消选择", "Clear selection") }, #selector(clearSelection))])
        let scroll = NSScrollView(); scroll.hasVerticalScroller = true; scroll.hasHorizontalScroller = true
        for (id, width) in [("pick", 55.0), ("game", 240.0), ("kind", 120.0), ("size", 100.0), ("date", 170.0), ("protected", 100.0), ("folder", 300.0)] {
            let column = NSTableColumn(identifier: NSUserInterfaceItemIdentifier(id)); column.width = width; table.addTableColumn(column)
        }
        table.dataSource = self; table.delegate = self; table.rowHeight = 28; scroll.documentView = table
        let note = NSTextField(labelWithString: tr("macOS 預覽版：不修改 Steam 索引；清理前須退出 Steam。", "macOS 预览版：不修改 Steam 索引；清理前须退出 Steam。", "macOS preview: no Steam index editing. Exit Steam before cleanup."))
        note.identifier = NSUserInterfaceItemIdentifier("note")
        let layout = NSStackView(views: [row([titleLabel, language]), row([rootField, button({ tr("選擇資料夾", "选择文件夹", "Browse") }, #selector(browse)), button({ tr("掃描", "扫描", "Scan") }, #selector(scan))]), summary, row([search, showClips]), policy, scroll, row([button({ tr("保護／取消保護", "保护／取消保护", "Protect / unprotect") }, #selector(toggleProtection)), button({ tr("開啟資料夾", "打开文件夹", "Open folder") }, #selector(openFolder)), button({ tr("移至垃圾桶…", "移至废纸篓…", "Move to Trash…") }, #selector(cleanup))]), note, status])
        layout.orientation = .vertical; layout.alignment = .leading; layout.spacing = 12; layout.translatesAutoresizingMaskIntoConstraints = false
        let content = window.contentView!; content.addSubview(layout)
        NSLayoutConstraint.activate([layout.leadingAnchor.constraint(equalTo: content.leadingAnchor, constant: 20), layout.trailingAnchor.constraint(equalTo: content.trailingAnchor, constant: -20), layout.topAnchor.constraint(equalTo: content.topAnchor, constant: 20), layout.bottomAnchor.constraint(equalTo: content.bottomAnchor, constant: -20), scroll.widthAnchor.constraint(equalTo: layout.widthAnchor), scroll.heightAnchor.constraint(greaterThanOrEqualToConstant: 280)])
        applyLanguage(); window.makeKeyAndOrderFront(nil)
    }
    @objc func changeLanguage() { UIStrings.language = language.indexOfSelectedItem; UserDefaults.standard.set(UIStrings.language, forKey: "language"); applyLanguage() }
    func applyLanguage() {
        window.title = tr("Steam Record 空間管理員 — macOS 預覽版", "Steam Record 空间管理器 — macOS 预览版", "Steam Record Manager — macOS Preview")
        titleLabel.stringValue = "STEAM RECORD  1.4.0-preview.1"
        search.placeholderString = tr("搜尋遊戲／App ID", "搜索游戏／App ID", "Search game / App ID")
        showClips.title = tr("顯示手動錄影及剪輯", "显示手动录像及剪辑", "Show manual recordings and clips")
        for (button, caption) in captions { button.title = caption() }
        let headers = [tr("選取", "选择", "Select"), tr("遊戲", "游戏", "Game"), tr("類型", "类型", "Type"), "GiB", tr("最後寫入", "最后写入", "Last modified"), tr("保護", "保护", "Protected"), tr("資料夾", "文件夹", "Folder")]
        for (column, header) in zip(table.tableColumns, headers) { column.title = header }
        if let note = window.contentView?.subviews.compactMap({ $0 as? NSStackView }).first?.arrangedSubviews.first(where: { $0.identifier?.rawValue == "note" }) as? NSTextField { note.stringValue = tr("macOS 預覽版：不修改 Steam 索引；清理前須退出 Steam。", "macOS 预览版：不修改 Steam 索引；清理前须退出 Steam。", "macOS preview: no Steam index editing. Exit Steam before cleanup.") }
        refresh()
    }
    func setBusy(_ value: Bool) { busy = value; controls.forEach { $0.isEnabled = !value }; table.isEnabled = !value }
    func alert(_ message: String) { let alert = NSAlert(); alert.messageText = "Steam Record Manager"; alert.informativeText = message; alert.runModal() }
    @objc func browse() { let panel = NSOpenPanel(); panel.canChooseDirectories = true; panel.canChooseFiles = false; if panel.runModal() == .OK, let url = panel.url { rootField.stringValue = url.path; scan() } }
    @objc func scan() {
        guard !busy, !rootField.stringValue.isEmpty else { return }
        let root = URL(fileURLWithPath: rootField.stringValue).standardizedFileURL
        setBusy(true); status.stringValue = tr("掃描中…", "扫描中…", "Scanning…")
        DispatchQueue.global(qos: .userInitiated).async {
            let result = Result { try RecordingCore.scan(root) }
            DispatchQueue.main.async {
                self.setBusy(false)
                switch result {
                case .success(let value): self.scannedRoot = root; self.sessions = value.sessions; self.skipped = value.skipped; self.chosen = []; self.refresh()
                case .failure(let error): self.alert(error.localizedDescription); self.status.stringValue = tr("掃描失敗。", "扫描失败。", "Scan failed.")
                }
            }
        }
    }
    func refresh() {
        table.reloadData()
        let total = sessions.reduce(0.0) { $0 + Double($1.bytes) } / RecordingCore.gib
        summary.stringValue = String(format: "%.2f GiB  /  %d %@  /  %d %@", total, sessions.count, tr("個工作階段", "个会话", "sessions"), skipped.count, tr("略過", "跳过", "skipped"))
        status.stringValue = String(format: "%d %@ / %.2f GiB — %@", chosen.count, tr("項已選", "项已选", "selected"), sessions.filter { chosen.contains($0.id) }.reduce(0.0) { $0 + Double($1.bytes) } / RecordingCore.gib, tr("建議只作用於搜尋範圍；0 停用規則。", "建议只作用于搜索范围；0 停用规则。", "Suggestions use search results; 0 disables a rule."))
    }
    func controlTextDidChange(_ notification: Notification) { refresh() }
    @objc func refreshAction() { refresh() }
    @objc func preview() { chosen = RecordingCore.plan(visible, protected: protected, limit: max(0, cap.doubleValue), days: max(0, days.integerValue)); refresh() }
    @objc func clearSelection() { chosen = []; refresh() }
    @objc func pick(_ sender: NSButton) { let rows = visible; guard rows.indices.contains(sender.tag) else { return }; let id = rows[sender.tag].id; if protected.contains(id) { return }; if sender.state == .on { chosen.insert(id) } else { chosen.remove(id) }; refresh() }
    @objc func toggleProtection() { let rows = visible; guard rows.indices.contains(table.selectedRow) else { return }; let id = rows[table.selectedRow].id; if protected.contains(id) { protected.remove(id) } else { protected.insert(id); chosen.remove(id) }; UserDefaults.standard.set(Array(protected), forKey: "protectedPaths"); refresh() }
    @objc func openFolder() { let rows = visible; guard rows.indices.contains(table.selectedRow) else { return }; NSWorkspace.shared.activateFileViewerSelecting([rows[table.selectedRow].url]) }
    func numberOfRows(in tableView: NSTableView) -> Int { visible.count }
    func tableView(_ tableView: NSTableView, viewFor column: NSTableColumn?, row: Int) -> NSView? {
        let rows = visible; guard rows.indices.contains(row), let id = column?.identifier.rawValue else { return nil }; let session = rows[row]
        if id == "pick" { let box = NSButton(checkboxWithTitle: "", target: self, action: #selector(pick(_:))); box.tag = row; box.state = chosen.contains(session.id) ? .on : .off; box.isEnabled = !busy && !protected.contains(session.id); return box }
        let text: String
        switch id {
        case "game": text = session.game
        case "kind": text = session.kind.label
        case "size": text = String(format: "%.2f", Double(session.bytes) / RecordingCore.gib)
        case "date": let formatter = DateFormatter(); formatter.dateFormat = "yyyy-MM-dd HH:mm"; text = formatter.string(from: session.modified)
        case "protected": text = protected.contains(session.id) ? "✓" : ""
        default: text = session.url.lastPathComponent
        }
        let field = NSTextField(labelWithString: text); field.lineBreakMode = .byTruncatingMiddle; field.toolTip = text; return field
    }
    @objc func cleanup() {
        guard !busy, let root = scannedRoot, !chosen.isEmpty else { return }
        guard URL(fileURLWithPath: rootField.stringValue).standardizedFileURL.path == root.path else { alert(tr("路徑已變動，請重新掃描。", "路径已更改，请重新扫描。", "Folder changed. Rescan.")); return }
        guard !RecordingCore.steamRunning else { alert(tr("請完全退出 Steam 後再清理。", "请完全退出 Steam 后再清理。", "Fully exit Steam before cleanup.")); return }
        let targets = sessions.filter { chosen.contains($0.id) }
        let confirmation = NSAlert(); confirmation.messageText = tr("移至垃圾桶", "移至废纸篓", "Move to Trash")
        confirmation.informativeText = "\(targets.count) / \(String(format: "%.2f", targets.reduce(0.0) { $0 + Double($1.bytes) } / RecordingCore.gib)) GiB\n" + tr("輸入 TRASH 確認。索引不會修改；清空垃圾桶後才釋放空間。", "输入 TRASH 确认。索引不会修改；清空废纸篓后才释放空间。", "Type TRASH to confirm. Indexes stay unchanged; empty Trash later to free space.")
        let token = NSTextField(frame: NSRect(x: 0, y: 0, width: 600, height: 24))
        let list = NSTextView(frame: NSRect(x: 0, y: 36, width: 600, height: 210)); list.isEditable = false; list.string = targets.map { $0.game + " | " + $0.url.lastPathComponent }.joined(separator: "\n")
        let accessory = NSView(frame: NSRect(x: 0, y: 0, width: 600, height: 250)); accessory.addSubview(token); let scroll = NSScrollView(frame: list.frame); list.frame.origin = .zero; scroll.documentView = list; scroll.hasVerticalScroller = true; accessory.addSubview(scroll); confirmation.accessoryView = accessory
        confirmation.addButton(withTitle: tr("確認", "确认", "Confirm")); confirmation.addButton(withTitle: tr("取消", "取消", "Cancel"))
        guard confirmation.runModal() == .alertFirstButtonReturn, token.stringValue == "TRASH" else { return }
        do { for session in targets { try RecordingCore.validate(session, root: root, protected: protected) } }
        catch { alert(error.localizedDescription); return }
        setBusy(true)
        var completed = 0
        do { for session in targets { try RecordingCore.trash(session, root: root, protected: protected); completed += 1 } }
        catch { alert("\(completed) " + tr("項完成，其餘停止：", "项完成，其余停止：", "completed; remaining items stopped: ") + error.localizedDescription) }
        setBusy(false); scan()
    }
}

enum CoreTests {
    static func check(_ value: @autoclosure () -> Bool, _ message: String) throws { if !value() { throw ManagerError("FAIL: " + message) } }
    static func run() throws {
        // macOS aliases /var to /private/var; keep fixtures in the checkout so
        // the production policy refusing linked ancestors is exercised unchanged.
        let root = URL(fileURLWithPath: FileManager.default.currentDirectoryPath, isDirectory: true).appendingPathComponent("srm-mac-test-" + UUID().uuidString)
        for path in ["video", "clips"] { try FileManager.default.createDirectory(at: root.appendingPathComponent(path), withIntermediateDirectories: true) }
        let session = root.appendingPathComponent("video/bg_123_20200101_120000")
        try FileManager.default.createDirectory(at: session, withIntermediateDirectories: false)
        let file = session.appendingPathComponent("session.mpd"); try Data("test".utf8).write(to: file)
        let old = Date().addingTimeInterval(-60 * 86400)
        try FileManager.default.setAttributes([.modificationDate: old], ofItemAtPath: file.path); try FileManager.default.setAttributes([.modificationDate: old], ofItemAtPath: session.path)
        let item = try RecordingCore.inspect(session, names: ["123": "測試遊戲"])
        try check(item.bytes == 4 && item.game == "測試遊戲", "size and Unicode game name")
        try RecordingCore.validate(item, root: root, protected: [])
        let scan = try RecordingCore.scan(root)
        try check(scan.sessions.count == 1 && scan.skipped.isEmpty, "recording-root scan")
        try check(RecordingCore.plan([item], protected: [], limit: 0, days: 30).contains(item.id), "age rule")
        try check(RecordingCore.plan([item], protected: [item.id], limit: 0, days: 30).isEmpty, "protected rule")
        try check(RecordingCore.plan([item], protected: [], limit: 0, days: 0).isEmpty, "disabled rules")
        let clip = Session(url: root.appendingPathComponent("clips/clip_123_20200101_120000"), appID: "123", game: "Synthetic clip", kind: .clip, bytes: 100_000, modified: old, fingerprint: "fixture")
        let manual = Session(url: root.appendingPathComponent("video/fg_123_20200101_120000"), appID: "123", game: "Synthetic manual", kind: .manual, bytes: 100_000, modified: old, fingerprint: "fixture")
        try check(RecordingCore.plan([item, clip, manual], protected: [], limit: 1 / RecordingCore.gib, days: 0) == [item.id], "quota excludes clips and manual recordings")
        try check(!RecordingCore.inside(URL(fileURLWithPath: root.path + "-other/file"), root: root), "path boundary")
        try Data("changed".utf8).write(to: file)
        var rejected = false; do { try RecordingCore.validate(item, root: root, protected: []) } catch { rejected = true }; try check(rejected, "changed recording rejected")
        let link = root.appendingPathComponent("video/bg_123_20200102_120000")
        try FileManager.default.createSymbolicLink(at: link, withDestinationURL: session)
        rejected = false; do { _ = try RecordingCore.inspect(link, names: [:]) } catch { rejected = true }; try check(rejected, "symlink rejected")
        for language in 0...2 { UIStrings.language = language; try check(!RecordingKind.background.label.isEmpty, "language resource") }
        for version in [39, 40, 41] {
            var bytes: [UInt8] = []
            func integer(_ n: UInt64, _ count: Int = 4) { for i in 0..<count { bytes.append(UInt8((n >> (8 * i)) & 255)) } }
            func string(_ text: String) { bytes += Array(text.utf8); bytes.append(0) }
            func key(_ text: String) { if version == 41 { integer(UInt64(["appinfo", "common", "name"].firstIndex(of: text)!)) } else { string(text) } }
            integer(0x07564400 | UInt64(version)); integer(1); if version == 41 { integer(0, 8) }
            integer(123); let sizePosition = bytes.count; integer(0); let start = bytes.count
            bytes += Array(repeating: 0, count: version >= 40 ? 60 : 40)
            bytes.append(0); key("appinfo"); bytes.append(0); key("common"); bytes.append(1); key("name"); string("測試遊戲 ✓"); bytes += [8, 8, 8]
            let length = bytes.count - start
            for i in 0..<4 { bytes[sizePosition + i] = UInt8((length >> (8 * i)) & 255) }
            integer(0)
            if version == 41 { let offset = bytes.count; integer(3); for text in ["appinfo", "common", "name"] { string(text) }; for i in 0..<8 { bytes[8 + i] = UInt8((UInt64(offset) >> (8 * i)) & 255) } }
            let names = try AppInfoReader(Data(bytes)).read(); try check(names["123"] == "測試遊戲 ✓", "appinfo v\(version)")
        }
        print("PASS: macOS preview scan metadata, Unicode names, retention, protection, disabled rules, path bounds, changed-file rejection and symlink refusal. Synthetic fixtures only; Trash not invoked.")
    }
}

if CommandLine.arguments.contains("--ui-smoke-test") {
    let app = NSApplication.shared
    let delegate = ManagerApp(); app.delegate = delegate; app.setActivationPolicy(.accessory)
    delegate.buildWindow(); delegate.window.close()
    print("PASS: native AppKit window and controls initialized.")
} else if CommandLine.arguments.contains("--self-test") {
    do { try CoreTests.run() } catch { fputs(error.localizedDescription + "\n", stderr); exit(1) }
} else {
    let app = NSApplication.shared
    let delegate = ManagerApp(); app.delegate = delegate
    app.setActivationPolicy(.regular)
    let main = NSMenu(), appMenu = NSMenu(), item = NSMenuItem()
    appMenu.addItem(withTitle: "Quit Steam Record Manager", action: #selector(NSApplication.terminate(_:)), keyEquivalent: "q")
    item.submenu = appMenu; main.addItem(item); app.mainMenu = main
    withExtendedLifetime(delegate) { app.run() }
}
