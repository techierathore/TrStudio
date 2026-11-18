import Foundation
import CoreGraphics

// macui win            -> "<id> <x> <y> <w> <h>" of the largest TrStudio window
// macui click <x> <y>  -> CGEvent left click at screen point
let args = CommandLine.arguments
let pidT: pid_t? = ProcessInfo.processInfo.environment["MACUI_PID"].flatMap { Int32($0) }
if args.count >= 2 && args[1] == "win" {
    let list = CGWindowListCopyWindowInfo([.optionOnScreenOnly], kCGNullWindowID) as? [[String: Any]] ?? []
    var best: (Int, CGRect)? = nil
    for w in list where (w[kCGWindowOwnerName as String] as? String) == "TrStudio" {
        let b = w[kCGWindowBounds as String] as? [String: CGFloat] ?? [:]
        let r = CGRect(x: b["X"] ?? 0, y: b["Y"] ?? 0, width: b["Width"] ?? 0, height: b["Height"] ?? 0)
        if best == nil || r.width * r.height > best!.1.width * best!.1.height {
            best = (w[kCGWindowNumber as String] as? Int ?? 0, r)
        }
    }
    if let b = best { print("\(b.0) \(Int(b.1.minX)) \(Int(b.1.minY)) \(Int(b.1.width)) \(Int(b.1.height))") } else { print("none") }
} else if args.count >= 2 && args[1] == "type" {
    // text from stdin, never argv
    let text = String(data: FileHandle.standardInput.readDataToEndOfFile(), encoding: .utf8) ?? ""
    let src = CGEventSource(stateID: .hidSystemState)
    for u in text.trimmingCharacters(in: .newlines).utf16 {
        var c = u
        let d = CGEvent(keyboardEventSource: src, virtualKey: 0, keyDown: true)
        d?.keyboardSetUnicodeString(stringLength: 1, unicodeString: &c)
        if let pid = pidT { d?.postToPid(pid) } else { d?.post(tap: .cghidEventTap) }
        let up = CGEvent(keyboardEventSource: src, virtualKey: 0, keyDown: false)
        up?.keyboardSetUnicodeString(stringLength: 1, unicodeString: &c)
        if let pid = pidT { up?.postToPid(pid) } else { up?.post(tap: .cghidEventTap) }
        usleep(15000)
    }
} else if args.count >= 2 && args[1] == "selectall" {
    let src = CGEventSource(stateID: .hidSystemState)
    let d = CGEvent(keyboardEventSource: src, virtualKey: 0, keyDown: true); d?.flags = .maskCommand; d?.post(tap: .cghidEventTap)
    let u = CGEvent(keyboardEventSource: src, virtualKey: 0, keyDown: false); u?.flags = .maskCommand; u?.post(tap: .cghidEventTap)
    usleep(50000)
    let b = CGEvent(keyboardEventSource: src, virtualKey: 51, keyDown: true); b?.post(tap: .cghidEventTap)
    CGEvent(keyboardEventSource: src, virtualKey: 51, keyDown: false)?.post(tap: .cghidEventTap)
} else if args.count >= 4 && args[1] == "click" {
    let p = CGPoint(x: Double(args[2])!, y: Double(args[3])!)
    let src = CGEventSource(stateID: .hidSystemState)
    CGEvent(mouseEventSource: src, mouseType: .mouseMoved, mouseCursorPosition: p, mouseButton: .left)?.post(tap: .cghidEventTap)
    usleep(80000)
    CGEvent(mouseEventSource: src, mouseType: .leftMouseDown, mouseCursorPosition: p, mouseButton: .left)?.post(tap: .cghidEventTap)
    usleep(60000)
    CGEvent(mouseEventSource: src, mouseType: .leftMouseUp, mouseCursorPosition: p, mouseButton: .left)?.post(tap: .cghidEventTap)
}
