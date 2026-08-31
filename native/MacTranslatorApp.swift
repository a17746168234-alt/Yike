import SwiftUI
import AppKit
import Speech
import AVFoundation
import Translation
import Vision
import NaturalLanguage
import UniformTypeIdentifiers
import ApplicationServices
import Carbon.HIToolbox

private let maxSourceCharacters = 5_000
private let globalShortcutDescription = "⇧⌘F"

private extension Notification.Name {
    static let translateSelectedText = Notification.Name("translateSelectedText")
}

private struct SelectedTextCapture {
    let text: String
    let anchor: CGPoint
}

private final class GlobalHotKeyController {
    private var hotKeyRef: EventHotKeyRef?
    private var eventHandlerRef: EventHandlerRef?

    init() {
        var eventType = EventTypeSpec(
            eventClass: OSType(kEventClassKeyboard),
            eventKind: UInt32(kEventHotKeyPressed)
        )
        InstallEventHandler(
            GetApplicationEventTarget(),
            { _, event, _ in
                guard let event else { return OSStatus(eventNotHandledErr) }
                var hotKeyID = EventHotKeyID()
                let status = GetEventParameter(
                    event,
                    EventParamName(kEventParamDirectObject),
                    EventParamType(typeEventHotKeyID),
                    nil,
                    MemoryLayout<EventHotKeyID>.size,
                    nil,
                    &hotKeyID
                )
                guard status == noErr, hotKeyID.id == 1 else {
                    return OSStatus(eventNotHandledErr)
                }
                let processID = NSWorkspace.shared.frontmostApplication?.processIdentifier
                NotificationCenter.default.post(
                    name: .translateSelectedText,
                    object: nil,
                    userInfo: processID.map { ["pid": Int($0)] }
                )
                return noErr
            },
            1,
            &eventType,
            nil,
            &eventHandlerRef
        )

        let hotKeyID = EventHotKeyID(signature: 0x46594E59, id: 1) // FYNY
        RegisterEventHotKey(
            UInt32(kVK_ANSI_F),
            UInt32(cmdKey | shiftKey),
            hotKeyID,
            GetApplicationEventTarget(),
            0,
            &hotKeyRef
        )
    }

    deinit {
        if let hotKeyRef { UnregisterEventHotKey(hotKeyRef) }
        if let eventHandlerRef { RemoveEventHandler(eventHandlerRef) }
    }
}

private final class AppDelegate: NSObject, NSApplicationDelegate, NSWindowDelegate {
    private var globalHotKeyController: GlobalHotKeyController?
    private var statusItem: NSStatusItem?
    private var mainWindow: NSWindow?

    func applicationDidFinishLaunching(_ notification: Notification) {
        guard claimSingleRunningInstance() else { return }
        globalHotKeyController = GlobalHotKeyController()
        configureStatusItem()
        DispatchQueue.main.async { [weak self] in
            self?.captureMainWindow()
        }
    }

    private func claimSingleRunningInstance() -> Bool {
        guard let bundleIdentifier = Bundle.main.bundleIdentifier else { return true }
        let currentProcessID = ProcessInfo.processInfo.processIdentifier
        guard let existingInstance = NSRunningApplication
            .runningApplications(withBundleIdentifier: bundleIdentifier)
            .first(where: { $0.processIdentifier != currentProcessID && !$0.isTerminated }) else {
            return true
        }

        existingInstance.activate(options: [.activateIgnoringOtherApps])
        NSApp.terminate(nil)
        return false
    }

    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool {
        false
    }

    func applicationShouldHandleReopen(_ sender: NSApplication, hasVisibleWindows flag: Bool) -> Bool {
        if !flag { showMainWindow() }
        return true
    }

    private func captureMainWindow() {
        guard let window = NSApp.windows.first(where: {
            $0.level == .normal && $0.styleMask.contains(.titled)
        }) else {
            DispatchQueue.main.asyncAfter(deadline: .now() + 0.15) { [weak self] in
                self?.captureMainWindow()
            }
            return
        }
        mainWindow = window
        window.isReleasedWhenClosed = false
        window.delegate = self
        window.isOpaque = false
        window.backgroundColor = .clear
        window.titlebarAppearsTransparent = true
        window.titleVisibility = .hidden
        window.toolbarStyle = .unified
        window.isMovableByWindowBackground = true
        NSApp.setActivationPolicy(.regular)
    }

    func windowShouldClose(_ sender: NSWindow) -> Bool {
        guard sender === mainWindow else { return true }
        sender.orderOut(nil)
        DispatchQueue.main.async {
            NSApp.setActivationPolicy(.accessory)
        }
        return false
    }

    private func configureStatusItem() {
        let item = NSStatusBar.system.statusItem(withLength: NSStatusItem.squareLength)
        if let button = item.button {
            button.image = NSImage(systemSymbolName: "character.book.closed", accessibilityDescription: "Mac翻译")
            button.toolTip = "Mac翻译 · \(globalShortcutDescription) 划词翻译"
        }
        let menu = NSMenu()
        let openItem = NSMenuItem(title: "打开 Mac翻译", action: #selector(showMainWindow), keyEquivalent: "")
        openItem.target = self
        menu.addItem(openItem)
        let shortcutItem = NSMenuItem(title: "划词翻译：\(globalShortcutDescription)", action: nil, keyEquivalent: "")
        shortcutItem.isEnabled = false
        menu.addItem(shortcutItem)
        menu.addItem(.separator())
        let quitItem = NSMenuItem(title: "退出 Mac翻译", action: #selector(quitApplication), keyEquivalent: "q")
        quitItem.target = self
        menu.addItem(quitItem)
        item.menu = menu
        statusItem = item
    }

    @objc private func showMainWindow() {
        if mainWindow == nil { captureMainWindow() }
        NSApp.setActivationPolicy(.regular)
        NSApp.activate(ignoringOtherApps: true)
        mainWindow?.makeKeyAndOrderFront(nil)
    }

    @objc private func quitApplication() {
        NSApp.terminate(nil)
    }

}

private let languageLabels: [String: String] = [
    "auto": "自动检测",
    "en": "英语",
    "zh-CN": "简体中文",
    "ja": "日语",
    "ko": "韩语"
]

private let concreteLanguages = ["en", "zh-CN", "ja", "ko"]
private let sourceLanguagesWithAuto = ["auto", "en", "zh-CN", "ja", "ko"]

private let speechLocales: [String: String] = [
    "en": "en-US",
    "zh-CN": "zh-CN",
    "ja": "ja-JP",
    "ko": "ko-KR"
]

private let appleTranslationLocales: [String: String] = [
    "en": "en",
    "zh-CN": "zh",
    "ja": "ja",
    "ko": "ko"
]

private func languageName(_ code: String) -> String {
    languageLabels[code] ?? code
}

private func historyTimeText(_ date: Date) -> String {
    let formatter = RelativeDateTimeFormatter()
    formatter.locale = Locale(identifier: "zh_CN")
    formatter.unitsStyle = .short
    return formatter.localizedString(for: date, relativeTo: Date())
}

private struct RecognizedImageBlock: Identifiable, Equatable, Codable {
    let id: UUID
    var text: String
    var boundingBox: CGRect

    init(id: UUID = UUID(), text: String, boundingBox: CGRect) {
        self.id = id
        self.text = text
        self.boundingBox = boundingBox
    }
}

private struct AppleImageTranslationRequest: Identifiable, Equatable {
    let id = UUID()
    let blocks: [RecognizedImageBlock]
    let source: String
    let target: String
}

private struct PopupAppleTranslationRequest: Identifiable, Equatable {
    let id = UUID()
    let text: String
    let originalText: String
    let source: String
    let target: String
    let glossaryMap: [String: String]
}

private enum ImageTranslationRenderStyle: String, CaseIterable, Identifiable, Codable {
    case natural
    case contrast

    var id: String { rawValue }

    var title: String {
        switch self {
        case .natural: return "自然融入"
        case .contrast: return "高对比"
        }
    }

    var icon: String {
        switch self {
        case .natural: return "wand.and.stars"
        case .contrast: return "rectangle.inset.filled"
        }
    }
}

private enum ImageOverlayTextColor: String, CaseIterable, Identifiable, Codable {
    case automatic
    case black
    case white
    case blue
    case red

    var id: String { rawValue }

    var title: String {
        switch self {
        case .automatic: return "自动"
        case .black: return "黑色"
        case .white: return "白色"
        case .blue: return "蓝色"
        case .red: return "红色"
        }
    }

    func resolved(automaticColor: NSColor) -> NSColor {
        switch self {
        case .automatic: return automaticColor
        case .black: return .black
        case .white: return .white
        case .blue: return NSColor.systemBlue
        case .red: return NSColor.systemRed
        }
    }
}

private enum ImageOverlayTextAlignment: String, CaseIterable, Identifiable, Codable {
    case automatic
    case left
    case center
    case right

    var id: String { rawValue }

    var title: String {
        switch self {
        case .automatic: return "自动"
        case .left: return "左"
        case .center: return "中"
        case .right: return "右"
        }
    }

    func resolved(automaticAlignment: NSTextAlignment) -> NSTextAlignment {
        switch self {
        case .automatic: return automaticAlignment
        case .left: return .left
        case .center: return .center
        case .right: return .right
        }
    }
}

private struct EditableImageTranslationBlock: Identifiable, Equatable, Codable {
    let id: UUID
    var originalText: String
    var translatedText: String
    var boundingBox: CGRect
    var fontScale: Double
    var textColor: ImageOverlayTextColor
    var alignment: ImageOverlayTextAlignment

    init(
        id: UUID = UUID(),
        originalText: String,
        translatedText: String,
        boundingBox: CGRect,
        fontScale: Double = 1,
        textColor: ImageOverlayTextColor = .automatic,
        alignment: ImageOverlayTextAlignment = .automatic
    ) {
        self.id = id
        self.originalText = originalText
        self.translatedText = translatedText
        self.boundingBox = boundingBox
        self.fontScale = fontScale
        self.textColor = textColor
        self.alignment = alignment
    }
}

private struct ImageBackgroundSample {
    let leading: NSColor
    let trailing: NSColor
    let average: NSColor
    let complexity: CGFloat
}

private func averageColor(_ colors: [NSColor]) -> NSColor {
    guard !colors.isEmpty else { return .windowBackgroundColor }
    var red: CGFloat = 0
    var green: CGFloat = 0
    var blue: CGFloat = 0
    for color in colors {
        let rgb = color.usingColorSpace(.deviceRGB) ?? color
        red += rgb.redComponent
        green += rgb.greenComponent
        blue += rgb.blueComponent
    }
    let count = CGFloat(colors.count)
    return NSColor(deviceRed: red / count, green: green / count, blue: blue / count, alpha: 1)
}

private func imageBackgroundSample(
    representation: NSBitmapImageRep?,
    around normalized: CGRect
) -> ImageBackgroundSample {
    guard let representation, representation.pixelsWide > 1, representation.pixelsHigh > 1 else {
        return ImageBackgroundSample(leading: .windowBackgroundColor, trailing: .windowBackgroundColor, average: .windowBackgroundColor, complexity: 0)
    }

    func color(x: CGFloat, y: CGFloat) -> NSColor? {
        let clampedX = min(max(x, 0), 1)
        let clampedY = min(max(y, 0), 1)
        let pixelX = min(representation.pixelsWide - 1, max(0, Int(clampedX * CGFloat(representation.pixelsWide - 1))))
        let pixelY = min(representation.pixelsHigh - 1, max(0, Int(clampedY * CGFloat(representation.pixelsHigh - 1))))
        return representation.colorAt(x: pixelX, y: pixelY)?.usingColorSpace(.deviceRGB)
    }

    let xPadding = max(0.006, min(0.025, normalized.width * 0.10))
    let yPadding = max(0.004, min(0.018, normalized.height * 0.28))
    let verticalSamples = [normalized.minY, normalized.midY, normalized.maxY]
    let horizontalSamples = [normalized.minX, normalized.midX, normalized.maxX]
    let leadingColors = verticalSamples.compactMap { color(x: normalized.minX - xPadding, y: $0) }
    let trailingColors = verticalSamples.compactMap { color(x: normalized.maxX + xPadding, y: $0) }
    let edgeColors = leadingColors + trailingColors
        + horizontalSamples.compactMap { color(x: $0, y: normalized.minY - yPadding) }
        + horizontalSamples.compactMap { color(x: $0, y: normalized.maxY + yPadding) }
    let average = averageColor(edgeColors)
    let rgbAverage = average.usingColorSpace(.deviceRGB) ?? average
    let complexity: CGFloat
    if edgeColors.isEmpty {
        complexity = 0
    } else {
        complexity = edgeColors.reduce(0) { partial, value in
            let rgb = value.usingColorSpace(.deviceRGB) ?? value
            let delta = abs(rgb.redComponent - rgbAverage.redComponent)
                + abs(rgb.greenComponent - rgbAverage.greenComponent)
                + abs(rgb.blueComponent - rgbAverage.blueComponent)
            return partial + delta / 3
        } / CGFloat(edgeColors.count)
    }
    return ImageBackgroundSample(
        leading: averageColor(leadingColors.isEmpty ? edgeColors : leadingColors),
        trailing: averageColor(trailingColors.isEmpty ? edgeColors : trailingColors),
        average: average,
        complexity: complexity
    )
}

private func smartMergedImageBlocks(_ rawBlocks: [RecognizedImageBlock]) -> [RecognizedImageBlock] {
    guard rawBlocks.count > 1 else { return rawBlocks }
    let sorted = rawBlocks.sorted { left, right in
        let verticalDifference = abs(left.boundingBox.midY - right.boundingBox.midY)
        if verticalDifference > max(left.boundingBox.height, right.boundingBox.height) * 0.45 {
            return left.boundingBox.midY > right.boundingBox.midY
        }
        return left.boundingBox.minX < right.boundingBox.minX
    }

    // Vision occasionally splits a visual line into several fragments. Join only
    // fragments with strong vertical overlap and a modest horizontal gap.
    var lines: [RecognizedImageBlock] = []
    for block in sorted {
        guard var previous = lines.last else {
            lines.append(block)
            continue
        }
        let overlap = max(0, min(previous.boundingBox.maxY, block.boundingBox.maxY)
            - max(previous.boundingBox.minY, block.boundingBox.minY))
        let overlapRatio = overlap / max(0.001, min(previous.boundingBox.height, block.boundingBox.height))
        let gap = block.boundingBox.minX - previous.boundingBox.maxX
        let sameLine = overlapRatio > 0.58
            && gap >= -0.012
            && gap < max(0.028, max(previous.boundingBox.height, block.boundingBox.height) * 1.55)
        if sameLine {
            previous.text += previous.text.last?.isWhitespace == true ? block.text : " " + block.text
            previous.boundingBox = previous.boundingBox.union(block.boundingBox)
            lines[lines.count - 1] = previous
        } else {
            lines.append(block)
        }
    }

    let heights = lines.map(\.boundingBox.height).sorted()
    let medianHeight = heights[heights.count / 2]
    func isList(_ text: String) -> Bool {
        text.range(of: #"^\s*([\u2022\u00b7\u25aa\u25e6*-]|\d+[.)\u3001])\s*"#, options: .regularExpression) != nil
    }

    // Merge nearby body lines into paragraphs, while retaining headings, lists,
    // labels and buttons as independent editable translation regions.
    var paragraphs: [RecognizedImageBlock] = []
    for line in lines {
        guard var previous = paragraphs.last else {
            paragraphs.append(line)
            continue
        }
        let upper = previous.boundingBox
        let lower = line.boundingBox
        let verticalGap = upper.minY - lower.maxY
        let heightRatio = lower.height / max(upper.height, 0.001)
        let horizontalOverlap = max(0, min(upper.maxX, lower.maxX) - max(upper.minX, lower.minX))
            / max(0.001, min(upper.width, lower.width))
        let aligned = abs(upper.minX - lower.minX) < 0.045 || horizontalOverlap > 0.72
        let titleLike = upper.height > medianHeight * 1.48 || lower.height > medianHeight * 1.48
        let mayMerge = verticalGap >= -0.006
            && verticalGap < max(0.018, (upper.height + lower.height) * 0.68)
            && (0.68...1.48).contains(heightRatio)
            && aligned
            && !titleLike
            && !isList(previous.text)
            && !isList(line.text)
            && previous.text.count + line.text.count < 360
        if mayMerge {
            previous.text += "\n" + line.text
            previous.boundingBox = upper.union(lower)
            paragraphs[paragraphs.count - 1] = previous
        } else {
            paragraphs.append(line)
        }
    }
    return paragraphs
}

private func recognizeTextBlocksInImage(at url: URL) throws -> [RecognizedImageBlock] {
    let request = VNRecognizeTextRequest()
    request.recognitionLevel = .accurate
    request.revision = VNRecognizeTextRequestRevision3
    request.usesLanguageCorrection = true
    request.automaticallyDetectsLanguage = true
    let desiredLanguages = ["en-US", "zh-Hans", "ja-JP", "ko-KR"]
    let supportedLanguages = try request.supportedRecognitionLanguages()
    request.recognitionLanguages = desiredLanguages.filter(supportedLanguages.contains)

    let handler = VNImageRequestHandler(url: url, options: [:])
    try handler.perform([request])

    let observations = (request.results ?? []).sorted { left, right in
        let verticalDifference = abs(left.boundingBox.midY - right.boundingBox.midY)
        if verticalDifference > 0.025 {
            return left.boundingBox.midY > right.boundingBox.midY
        }
        return left.boundingBox.minX < right.boundingBox.minX
    }
    let rawBlocks: [RecognizedImageBlock] = observations.compactMap { observation -> RecognizedImageBlock? in
        guard let text = observation.topCandidates(1).first?.string
            .trimmingCharacters(in: .whitespacesAndNewlines),
              !text.isEmpty else { return nil }
        return RecognizedImageBlock(text: text, boundingBox: observation.boundingBox)
    }
    return smartMergedImageBlocks(rawBlocks)
}

private func renderedTranslationImage(
    source: NSImage,
    edits: [EditableImageTranslationBlock],
    style: ImageTranslationRenderStyle
) -> NSImage {
    let imageSize = source.size
    let bitmap = source.tiffRepresentation.flatMap(NSBitmapImageRep.init(data:))
    let pixelWidth = bitmap?.pixelsWide ?? max(1, Int(imageSize.width.rounded()))
    let pixelHeight = bitmap?.pixelsHigh ?? max(1, Int(imageSize.height.rounded()))
    guard let outputRepresentation = NSBitmapImageRep(
        bitmapDataPlanes: nil,
        pixelsWide: pixelWidth,
        pixelsHigh: pixelHeight,
        bitsPerSample: 8,
        samplesPerPixel: 4,
        hasAlpha: true,
        isPlanar: false,
        colorSpaceName: .deviceRGB,
        bytesPerRow: 0,
        bitsPerPixel: 0
    ) else {
        return source.copy() as? NSImage ?? source
    }
    // Set the point size before creating the graphics context. On Retina images the
    // bitmap is commonly 2x the point size; creating the context first makes AppKit
    // draw the source into only one quarter of the output bitmap.
    outputRepresentation.size = imageSize
    guard let graphicsContext = NSGraphicsContext(bitmapImageRep: outputRepresentation) else {
        return source.copy() as? NSImage ?? source
    }
    NSGraphicsContext.saveGraphicsState()
    NSGraphicsContext.current = graphicsContext
    defer { NSGraphicsContext.restoreGraphicsState() }

    source.draw(
        in: NSRect(origin: .zero, size: imageSize),
        from: NSRect(origin: .zero, size: source.size),
        operation: .copy,
        fraction: 1
    )

    let paragraph = NSMutableParagraphStyle()
    paragraph.alignment = .center
    paragraph.lineBreakMode = .byWordWrapping

    for edit in edits {
        let translated = edit.translatedText.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !translated.isEmpty else { continue }

        let normalized = edit.boundingBox
        let originalRect = NSRect(
            x: normalized.minX * imageSize.width,
            y: normalized.minY * imageSize.height,
            width: normalized.width * imageSize.width,
            height: normalized.height * imageSize.height
        )
        let horizontalExpansion = min(imageSize.width * 0.024, max(4, originalRect.width * 0.08))
        let verticalExpansion = min(imageSize.height * 0.016, max(3, originalRect.height * 0.22))
        var box = originalRect.insetBy(dx: -horizontalExpansion, dy: -verticalExpansion)
        box.origin.x = max(2, box.origin.x)
        box.origin.y = max(2, box.origin.y)
        box.size.width = min(box.width, imageSize.width - box.minX - 2)
        box.size.height = min(max(box.height, originalRect.height * 1.45), imageSize.height - box.minY - 2)

        let sample = imageBackgroundSample(representation: bitmap, around: normalized)
        let trimmedOriginal = edit.originalText.trimmingCharacters(in: .whitespacesAndNewlines)
        let looksLikeList = trimmedOriginal.range(
            of: #"^([•·▪◦*-]|\d+[.)、])\s*"#,
            options: .regularExpression
        ) != nil
        let looksLikeTitle = originalRect.height > imageSize.height * 0.045
            || (trimmedOriginal.count < 42 && originalRect.width > imageSize.width * 0.42)
        let automaticAlignment: NSTextAlignment = (looksLikeList || originalRect.width > imageSize.width * 0.38) ? .left : .center
        paragraph.alignment = edit.alignment.resolved(automaticAlignment: automaticAlignment)
        let rgbBackground = sample.average.usingColorSpace(.deviceRGB) ?? sample.average
        let luminance = 0.2126 * rgbBackground.redComponent
            + 0.7152 * rgbBackground.greenComponent
            + 0.0722 * rgbBackground.blueComponent
        let textColor: NSColor
        let backgroundPath = NSBezierPath(
            roundedRect: box,
            xRadius: style == .natural ? max(1.5, box.height * 0.045) : max(3, box.height * 0.12),
            yRadius: style == .natural ? max(1.5, box.height * 0.045) : max(3, box.height * 0.12)
        )

        if style == .natural {
            if sample.complexity < 0.16 {
                NSGradient(starting: sample.leading, ending: sample.trailing)?.draw(in: backgroundPath, angle: 0)
                textColor = luminance > 0.58 ? NSColor(calibratedWhite: 0.08, alpha: 1) : .white
            } else {
                // Reconstruct the covered area from real texture immediately to
                // the left and right of the OCR box. Stretching both edge strips
                // toward the centre removes the old glyphs while preserving local
                // colour, lighting and texture without a GPU-dependent blur pass.
                NSGraphicsContext.saveGraphicsState()
                backgroundPath.addClip()
                let stripWidth = max(2, min(box.width * 0.10, imageSize.width * 0.018))
                let leadingSource = NSRect(
                    x: max(0, originalRect.minX - stripWidth),
                    y: box.minY,
                    width: min(stripWidth, max(0, originalRect.minX)),
                    height: box.height
                )
                let trailingX = min(imageSize.width, originalRect.maxX)
                let trailingSource = NSRect(
                    x: trailingX,
                    y: box.minY,
                    width: min(stripWidth, max(0, imageSize.width - trailingX)),
                    height: box.height
                )
                let leftHalf = NSRect(x: box.minX, y: box.minY, width: box.width * 0.56, height: box.height)
                let rightHalf = NSRect(x: box.midX, y: box.minY, width: box.width * 0.50, height: box.height)
                if leadingSource.width > 0 {
                    source.draw(in: leftHalf, from: leadingSource, operation: .copy, fraction: 1)
                }
                if trailingSource.width > 0 {
                    source.draw(in: rightHalf, from: trailingSource, operation: .sourceOver, fraction: 0.92)
                }
                NSGradient(
                    starting: sample.leading.withAlphaComponent(0.34),
                    ending: sample.trailing.withAlphaComponent(0.34)
                )?.draw(in: backgroundPath, angle: 0)
                NSGraphicsContext.restoreGraphicsState()
                sample.average.withAlphaComponent(0.18).setFill()
                backgroundPath.fill()
                NSColor.white.withAlphaComponent(luminance > 0.52 ? 0.16 : 0.09).setStroke()
                backgroundPath.lineWidth = 0.8
                backgroundPath.stroke()
                textColor = luminance > 0.52 ? NSColor(calibratedWhite: 0.08, alpha: 1) : .white
            }
        } else {
            NSColor(calibratedWhite: 0.05, alpha: 0.82).setFill()
            backgroundPath.fill()
            textColor = .white
        }

        let textRect = box.insetBy(dx: max(3, box.width * 0.035), dy: max(2, box.height * 0.08))
        let resolvedTextColor = edit.textColor.resolved(automaticColor: textColor)
        let preferredFontSize = min(max(8, originalRect.height * 0.78 * CGFloat(edit.fontScale)), 54)
        // A geometric fit is deterministic and avoids invoking TextKit's
        // unbounded-height measurement while an off-screen AppKit bitmap context
        // is active. The square-root estimate is conservative for wrapped CJK and
        // Latin text and still honours the user's font scale.
        let characterCount = CGFloat(max(1, translated.count))
        let areaFit = sqrt(max(1, textRect.width * textRect.height) / max(1, characterCount * 0.82))
        let fontSize = min(preferredFontSize, max(8, areaFit * 0.92))
        let attributes: [NSAttributedString.Key: Any] = [
            .font: NSFont.systemFont(ofSize: fontSize, weight: looksLikeTitle ? .bold : .semibold),
            .foregroundColor: resolvedTextColor,
            .paragraphStyle: paragraph,
            .strokeColor: luminance > 0.58 ? NSColor.black.withAlphaComponent(0.28) : NSColor.white.withAlphaComponent(0.34),
            .strokeWidth: -0.35
        ]
        (translated as NSString).draw(
            with: textRect,
            options: [.usesLineFragmentOrigin, .usesFontLeading],
            attributes: attributes
        )
    }

    graphicsContext.flushGraphics()
    let output = NSImage(size: imageSize)
    output.addRepresentation(outputRepresentation)
    return output
}

private enum TranslationEngine: String, CaseIterable, Identifiable {
    case apple
    case deepl

    var id: String { rawValue }

    var name: String {
        switch self {
        case .apple: return "Apple 系统翻译"
        case .deepl: return "DeepL 高质量"
        }
    }

    var shortName: String {
        switch self {
        case .apple: return "Apple 翻译"
        case .deepl: return "DeepL"
        }
    }
}

private struct AppleTranslationRequest: Identifiable, Equatable {
    let id: UUID
    let text: String
    let originalText: String
    let source: String
    let target: String
    let glossaryMap: [String: String]
}

private struct DeepLTranslationPayload: Decodable {
    struct Item: Decodable {
        let text: String
    }

    let translations: [Item]
}

private struct TranslationHistory: Identifiable, Codable {
    let id: UUID
    let original: String
    let result: String
    let source: String
    let target: String
    var date: Date
    var engine: String
    var isPinned: Bool

    init(id: UUID, original: String, result: String, source: String, target: String,
         date: Date = Date(), engine: String = "", isPinned: Bool = false) {
        self.id = id
        self.original = original
        self.result = result
        self.source = source
        self.target = target
        self.date = date
        self.engine = engine
        self.isPinned = isPinned
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        id = try container.decode(UUID.self, forKey: .id)
        original = try container.decode(String.self, forKey: .original)
        result = try container.decode(String.self, forKey: .result)
        source = try container.decode(String.self, forKey: .source)
        target = try container.decode(String.self, forKey: .target)
        date = try container.decodeIfPresent(Date.self, forKey: .date) ?? Date()
        engine = try container.decodeIfPresent(String.self, forKey: .engine) ?? ""
        isPinned = try container.decodeIfPresent(Bool.self, forKey: .isPinned) ?? false
    }

    private enum CodingKeys: String, CodingKey {
        case id, original, result, source, target, date, engine, isPinned
    }
}

private struct ImageTranslationHistory: Identifiable, Codable {
    let id: UUID
    var date: Date
    var source: String
    var target: String
    var engine: String
    var style: ImageTranslationRenderStyle
    var sourceFileName: String
    var translatedFileName: String
    var originalText: String
    var translatedText: String
    var blocks: [EditableImageTranslationBlock]
}

private struct GlossaryEntry: Identifiable, Codable, Equatable {
    var id = UUID()
    var source: String
    var target: String
    var sourceLanguage: String
    var targetLanguage: String
}

private enum NoticeKind {
    case success
    case info
    case error

    var icon: String {
        switch self {
        case .success: return "checkmark.circle.fill"
        case .info: return "info.circle.fill"
        case .error: return "exclamationmark.circle.fill"
        }
    }

    var color: Color {
        switch self {
        case .success: return Color(red: 0.16, green: 0.65, blue: 0.42)
        case .info: return Color(red: 0.08, green: 0.45, blue: 0.96)
        case .error: return Color.orange
        }
    }

    var title: String {
        switch self {
        case .success: return "已完成"
        case .info: return "提示"
        case .error: return "需要注意"
        }
    }
}

private enum NoticeAction {
    case openAccessibilitySettings
    case openSpeechRecognitionSettings
    case openMicrophoneSettings

    var buttonTitle: String { "打开系统设置" }

    var url: URL? {
        switch self {
        case .openAccessibilitySettings:
            return URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility")
        case .openSpeechRecognitionSettings:
            return URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_SpeechRecognition")
        case .openMicrophoneSettings:
            return URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_Microphone")
        }
    }

    func open() {
        if let url { NSWorkspace.shared.open(url) }
    }
}

private struct AppNotice {
    let kind: NoticeKind
    let message: String
    var action: NoticeAction? = nil
}

private struct DeepLUsagePayload: Decodable {
    let characterCount: Int
    let characterLimit: Int

    enum CodingKeys: String, CodingKey {
        case characterCount = "character_count"
        case characterLimit = "character_limit"
    }

    var usedPercentText: String {
        guard characterLimit > 0 else { return "0" }
        return String(format: "%.1f", Double(characterCount) / Double(characterLimit) * 100)
    }
}

@MainActor
private final class TranslatorViewModel: NSObject, ObservableObject, AVAudioPlayerDelegate, AVSpeechSynthesizerDelegate {
    static let shared = TranslatorViewModel()

    @Published var sourceText = ""
    @Published var translatedText = ""
    @Published var sourceLanguage = "auto"
    @Published var targetLanguage = "zh-CN"
    @Published var isLoading = false
    @Published var notice: AppNotice?
    @Published private(set) var deepLUsage: DeepLUsagePayload?
    @Published var historyRecordingEnabled = true
    @Published var imageHistoryRecordingEnabled = true
    @Published var copyLabel = "复制译文"
    @Published var isListening = false
    @Published var history: [TranslationHistory] = []
    @Published private(set) var imageHistory: [ImageTranslationHistory] = []
    @Published var selectedEngine: TranslationEngine = .apple
    @Published var appleTranslationRequest: AppleTranslationRequest?
    @Published var appleImageTranslationRequest: AppleImageTranslationRequest?
    @Published private(set) var popupAppleTranslationRequest: PopupAppleTranslationRequest?
    @Published private(set) var popupSourceText = ""
    @Published private(set) var popupTranslatedText = ""
    @Published private(set) var popupSourceLanguage = "en"
    @Published private(set) var popupTargetLanguage = "zh-CN"
    @Published private(set) var popupIsLoading = false
    @Published private(set) var popupNotice: AppNotice?
    @Published private(set) var hasDeepLKey = false
    @Published private(set) var isRecognizingImage = false
    @Published private(set) var isCapturingRegion = false
    @Published private(set) var screenCaptureCountdown = 0
    @Published private(set) var sourceImage: NSImage?
    @Published private(set) var translatedImage: NSImage?
    @Published private(set) var imageRenderStyle: ImageTranslationRenderStyle = .natural
    @Published private(set) var imageActionLabel = "复制图片"
    @Published private(set) var isSpeakingResult = false
    @Published private(set) var isResultSpeechPaused = false
    @Published private(set) var isPreparingSpeech = false
    @Published private(set) var speechStatusMessage: String?
    @Published var onlineVoicePersona: OnlineVoicePersona = .female
    @Published var onlineSpeechRatePercent = -6
    @Published private(set) var speechHighlightedSentenceIndex = -1
    @Published var glossary: [GlossaryEntry] = []
    @Published var isEditingImageOCR = false
    @Published private(set) var isInitialImageOCRReview = false
    @Published private(set) var editableImageBlocks: [EditableImageTranslationBlock] = []
    @Published private(set) var speechCacheBytes: Int64 = 0
    @Published var popupPinned = false

    private let historyKey = "yijian.translation.history"
    private let engineKey = "yijian.translation.engine"
    private let imageRenderStyleKey = "yijian.image.translation.render-style"
    private let historyEnabledKey = "yijian.history.recording.enabled"
    private let imageHistoryKey = "yijian.image.translation.history"
    private let imageHistoryEnabledKey = "yijian.image.history.recording.enabled"
    private let glossaryKey = "yijian.translation.glossary"
    private let voicePersonaKey = "yijian.online-speech.voice-persona"
    private let speechRateKey = "yijian.online-speech.rate-percent"
    private let audioEngine = AVAudioEngine()
    private let onlineSpeechService = OnlineTTSService()
    private var onlineSpeechRate: String {
        onlineSpeechRatePercent > 0 ? "+\(onlineSpeechRatePercent)%" : "\(onlineSpeechRatePercent)%"
    }
    private var resultAudioPlayer: AVAudioPlayer?
    private let fallbackSpeechSynthesizer = AVSpeechSynthesizer()
    private var speechTask: Task<Void, Never>?
    private var speechHighlightTask: Task<Void, Never>?
    private var speechAudioURL: URL?
    private var speechPreloadTask: Task<URL?, Never>?
    private var translationTask: Task<Void, Never>?
    private var translationRequestGate = TranslationRequestGate()
    private var imageTranslationTask: Task<Void, Never>?
    private var popupTranslationTask: Task<Void, Never>?
    private var speechPreloadKey: String?
    private var deepLAPIKey = ""
    private var recognitionRequest: SFSpeechAudioBufferRecognitionRequest?
    private var recognitionTask: SFSpeechRecognitionTask?
    private var silenceTask: Task<Void, Never>?
    private var speechBaseText = ""
    private var speechSentences: [String] = []
    private var lastRecognizedText = ""
    private var lastSelectionRequestAt = Date.distantPast
    private var lastSelectionProcessID: pid_t?
    private var recognizedImageBlocks: [RecognizedImageBlock] = []
    private var imageBlockTranslations: [String] = []
    private var pendingImageSourceLanguage = "en"
    private var pendingImageTargetLanguage = "zh-CN"
    private var currentImageHistoryID: UUID?
    private lazy var selectionPanelController = SelectionTranslationPanelController()

    private struct PasteboardItemSnapshot {
        let values: [(NSPasteboard.PasteboardType, Data)]
    }

    override init() {
        super.init()
        fallbackSpeechSynthesizer.delegate = self
        trimOnlineSpeechCache()
        if let value = UserDefaults.standard.string(forKey: engineKey),
           let engine = TranslationEngine(rawValue: value) {
            selectedEngine = engine
        }
        if let value = UserDefaults.standard.string(forKey: imageRenderStyleKey),
           let style = ImageTranslationRenderStyle(rawValue: value) {
            imageRenderStyle = style
        }
        if UserDefaults.standard.object(forKey: historyEnabledKey) != nil {
            historyRecordingEnabled = UserDefaults.standard.bool(forKey: historyEnabledKey)
        }
        if UserDefaults.standard.object(forKey: imageHistoryEnabledKey) != nil {
            imageHistoryRecordingEnabled = UserDefaults.standard.bool(forKey: imageHistoryEnabledKey)
        }
        if let value = UserDefaults.standard.string(forKey: voicePersonaKey) {
            let persona: OnlineVoicePersona = (value == "male" || value == "youthfulMale") ? .male : .female
            onlineVoicePersona = persona
            UserDefaults.standard.set(persona.rawValue, forKey: voicePersonaKey)
        }
        if UserDefaults.standard.object(forKey: speechRateKey) != nil {
            onlineSpeechRatePercent = min(30, max(-30, UserDefaults.standard.integer(forKey: speechRateKey)))
        }
        if let data = UserDefaults.standard.data(forKey: glossaryKey),
           let items = try? JSONDecoder().decode([GlossaryEntry].self, from: data) {
            glossary = items
        }
        deepLAPIKey = SecureKeyStore.loadDeepLKey()
        hasDeepLKey = !deepLAPIKey.isEmpty
        if let data = UserDefaults.standard.data(forKey: historyKey),
           let items = try? JSONDecoder().decode([TranslationHistory].self, from: data) {
            history = items
        }
        if let data = UserDefaults.standard.data(forKey: imageHistoryKey),
           let items = try? JSONDecoder().decode([ImageTranslationHistory].self, from: data) {
            imageHistory = Array(items.filter { item in
                FileManager.default.fileExists(atPath: imageHistoryDirectory.appendingPathComponent(item.sourceFileName).path)
                    && FileManager.default.fileExists(atPath: imageHistoryDirectory.appendingPathComponent(item.translatedFileName).path)
            }.prefix(10))
        }
        refreshSpeechCacheSize()
    }

    private func setError(_ message: String, action: NoticeAction? = nil) {
        notice = AppNotice(kind: .error, message: message, action: action)
    }

    private func setSuccess(_ message: String) {
        notice = AppNotice(kind: .success, message: message)
    }

    private func setInfo(_ message: String, action: NoticeAction? = nil) {
        notice = AppNotice(kind: .info, message: message, action: action)
    }

    var sourceName: String { languageName(sourceLanguage) }
    var targetName: String { languageName(targetLanguage) }
    var canTranslate: Bool { !sourceText.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty && !isLoading }
    var hasImageTranslation: Bool { sourceImage != nil }
    var canEditImageSourceText: Bool {
        sourceImage != nil && !recognizedImageBlocks.isEmpty && !isRecognizingImage
    }

    func translateSelectedText(from processID: pid_t?) {
        let now = Date()
        if processID == lastSelectionProcessID,
           now.timeIntervalSince(lastSelectionRequestAt) < 0.45 {
            return
        }
        lastSelectionRequestAt = now
        lastSelectionProcessID = processID

        guard let processID else {
            showGlobalShortcutError("没有找到当前使用的软件，请重新选中文字后再试")
            return
        }

        if let capture = accessibilitySelection(from: processID), !capture.text.isEmpty {
            useSelectedTextAndTranslate(capture.text, anchor: capture.anchor, sourceProcessID: processID)
            return
        }

        copySelectedTextFromFrontmostApp { [weak self] selectedText in
            guard let self else { return }
            guard let selectedText, !selectedText.isEmpty else {
                self.showGlobalShortcutError("没有读取到选中的文字。请确认已选中文字；如果刚打开辅助功能权限，请完全退出“Mac翻译”后重新打开")
                return
            }
            self.useSelectedTextAndTranslate(selectedText, anchor: NSEvent.mouseLocation, sourceProcessID: processID)
        }
    }

    func requestGlobalShortcutPermission() {
        let promptKey = kAXTrustedCheckOptionPrompt.takeUnretainedValue() as String
        if AXIsProcessTrustedWithOptions([promptKey: true] as CFDictionary) {
            setSuccess("划词翻译已可使用：选中文字后按 \(globalShortcutDescription)")
        } else {
            setInfo("请在系统设置 → 隐私与安全性 → 辅助功能中允许“Mac翻译”", action: .openAccessibilitySettings)
        }
    }

    private func accessibilitySelection(from processID: pid_t) -> SelectedTextCapture? {
        let application = AXUIElementCreateApplication(processID)
        var focusedValue: CFTypeRef?
        guard AXUIElementCopyAttributeValue(
            application,
            kAXFocusedUIElementAttribute as CFString,
            &focusedValue
        ) == .success,
        let focusedValue,
        CFGetTypeID(focusedValue) == AXUIElementGetTypeID() else { return nil }

        let focusedElement = unsafeBitCast(focusedValue, to: AXUIElement.self)
        var selectedValue: CFTypeRef?
        guard AXUIElementCopyAttributeValue(
            focusedElement,
            kAXSelectedTextAttribute as CFString,
            &selectedValue
        ) == .success else { return nil }
        guard let text = (selectedValue as? String)?.trimmingCharacters(in: .whitespacesAndNewlines),
              !text.isEmpty else { return nil }
        return SelectedTextCapture(text: text, anchor: selectionAnchor(for: focusedElement) ?? NSEvent.mouseLocation)
    }

    private func selectionAnchor(for element: AXUIElement) -> CGPoint? {
        var rangeValue: CFTypeRef?
        guard AXUIElementCopyAttributeValue(
            element,
            kAXSelectedTextRangeAttribute as CFString,
            &rangeValue
        ) == .success,
        let rangeValue,
        CFGetTypeID(rangeValue) == AXValueGetTypeID() else { return nil }

        var boundsValue: CFTypeRef?
        guard AXUIElementCopyParameterizedAttributeValue(
            element,
            kAXBoundsForRangeParameterizedAttribute as CFString,
            rangeValue,
            &boundsValue
        ) == .success,
        let boundsValue,
        CFGetTypeID(boundsValue) == AXValueGetTypeID() else { return nil }

        var accessibilityRect = CGRect.zero
        guard AXValueGetValue(
            unsafeBitCast(boundsValue, to: AXValue.self),
            .cgRect,
            &accessibilityRect
        ) else { return nil }

        let primaryScreenHeight = NSScreen.screens.first?.frame.height ?? 0
        let cocoaY = primaryScreenHeight - accessibilityRect.maxY
        return CGPoint(x: accessibilityRect.midX, y: cocoaY)
    }

    private func copySelectedTextFromFrontmostApp(completion: @escaping (String?) -> Void) {
        let pasteboard = NSPasteboard.general
        let snapshot = pasteboardSnapshot(pasteboard)
        let marker = "fanyi-selection-\(UUID().uuidString)"
        pasteboard.clearContents()
        pasteboard.setString(marker, forType: .string)

        guard let source = CGEventSource(stateID: .hidSystemState),
              let keyDown = CGEvent(keyboardEventSource: source, virtualKey: CGKeyCode(kVK_ANSI_C), keyDown: true),
              let keyUp = CGEvent(keyboardEventSource: source, virtualKey: CGKeyCode(kVK_ANSI_C), keyDown: false) else {
            restorePasteboard(snapshot, to: pasteboard)
            completion(nil)
            return
        }
        keyDown.flags = .maskCommand
        keyUp.flags = .maskCommand
        keyDown.post(tap: .cghidEventTap)
        keyUp.post(tap: .cghidEventTap)

        DispatchQueue.main.asyncAfter(deadline: .now() + 0.18) { [weak self] in
            let copied = pasteboard.string(forType: .string)
            self?.restorePasteboard(snapshot, to: pasteboard)
            completion(copied == marker ? nil : copied?.trimmingCharacters(in: .whitespacesAndNewlines))
        }
    }

    private func pasteboardSnapshot(_ pasteboard: NSPasteboard) -> [PasteboardItemSnapshot] {
        (pasteboard.pasteboardItems ?? []).map { item in
            PasteboardItemSnapshot(values: item.types.compactMap { type in
                item.data(forType: type).map { (type, $0) }
            })
        }
    }

    private func restorePasteboard(_ snapshot: [PasteboardItemSnapshot], to pasteboard: NSPasteboard) {
        pasteboard.clearContents()
        let items = snapshot.map { snapshotItem -> NSPasteboardItem in
            let item = NSPasteboardItem()
            snapshotItem.values.forEach { item.setData($0.1, forType: $0.0) }
            return item
        }
        if !items.isEmpty { pasteboard.writeObjects(items) }
    }

    private func useSelectedTextAndTranslate(_ text: String, anchor: CGPoint, sourceProcessID: pid_t) {
        let limitedText = String(text.prefix(maxSourceCharacters))
        popupSourceText = limitedText
        popupSourceLanguage = detectSupportedLanguage(in: limitedText)
        popupTargetLanguage = popupSourceLanguage == "en" ? "zh-CN" : "en"
        popupTranslatedText = ""
        popupNotice = nil
        selectionPanelController.show(model: self, anchor: anchor, sourceProcessID: sourceProcessID)
        translatePopup()
    }

    private func showGlobalShortcutError(_ message: String) {
        popupTranslationTask?.cancel()
        popupTranslationTask = nil
        popupAppleTranslationRequest = nil
        popupSourceText = ""
        popupTranslatedText = ""
        popupIsLoading = false
        popupNotice = AppNotice(kind: .error, message: message)
        selectionPanelController.show(model: self, anchor: NSEvent.mouseLocation, sourceProcessID: nil)
    }

    func setPopupSourceLanguage(_ language: String) {
        guard language != popupTargetLanguage else { return }
        popupSourceLanguage = language
        popupTranslatedText = ""
        translatePopup()
    }

    func setPopupTargetLanguage(_ language: String) {
        guard language != popupSourceLanguage else { return }
        popupTargetLanguage = language
        popupTranslatedText = ""
        translatePopup()
    }

    private func translatePopup() {
        let cleanText = popupSourceText.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !cleanText.isEmpty else { return }
        popupTranslationTask?.cancel()
        popupTranslationTask = nil
        popupAppleTranslationRequest = nil
        popupTranslatedText = ""
        popupNotice = nil
        popupIsLoading = true

        let source = popupSourceLanguage
        let target = popupTargetLanguage
        let prepared = glossaryPreparedText(cleanText, source: source, target: target)

        switch selectedEngine {
        case .apple:
            if #available(macOS 15.0, *) {
                let request = PopupAppleTranslationRequest(
                    text: prepared.0,
                    originalText: cleanText,
                    source: source,
                    target: target,
                    glossaryMap: prepared.1
                )
                popupAppleTranslationRequest = request
            } else {
                popupIsLoading = false
                popupNotice = AppNotice(kind: .error, message: "Apple 系统翻译需要 macOS 15 或更高版本")
            }
        case .deepl:
            guard hasDeepLKey else {
                popupIsLoading = false
                popupNotice = AppNotice(kind: .error, message: "请先在主窗口设置 DeepL API Free 密钥")
                return
            }
            popupTranslationTask = Task { [weak self] in
                guard let self else { return }
                do {
                    let results = try await self.deepLTranslations(for: [prepared.0], source: source, target: target)
                    guard !Task.isCancelled, let result = results.first else { return }
                    self.finishPopupTranslation(
                        result,
                        original: cleanText,
                        source: source,
                        target: target,
                        glossaryMap: prepared.1
                    )
                    Task { await self.fetchDeepLUsage() }
                } catch {
                    guard !Task.isCancelled else { return }
                    self.showPopupTranslationError(error)
                }
            }
        }
    }

    @available(macOS 15.0, *)
    func completePopupAppleTranslation(using session: TranslationSession, request: PopupAppleTranslationRequest) async {
        guard popupAppleTranslationRequest?.id == request.id else { return }
        do {
            try await session.prepareTranslation()
            let response = try await session.translate(request.text)
            guard popupAppleTranslationRequest?.id == request.id else { return }
            finishPopupTranslation(
                response.targetText,
                original: request.originalText,
                source: request.source,
                target: request.target,
                glossaryMap: request.glossaryMap
            )
        } catch {
            guard popupAppleTranslationRequest?.id == request.id else { return }
            showPopupTranslationError(error)
        }
    }

    @available(macOS 26.0, *)
    private func translatePopupWithInstalledApple(_ request: PopupAppleTranslationRequest) async {
        guard popupAppleTranslationRequest?.id == request.id,
              let sourceID = appleTranslationLocales[request.source],
              let targetID = appleTranslationLocales[request.target] else { return }
        let source = Locale.Language(identifier: sourceID)
        let target = Locale.Language(identifier: targetID)
        guard await LanguageAvailability().status(from: source, to: target) == .installed else {
            popupIsLoading = false
            popupAppleTranslationRequest = nil
            popupTranslationTask = nil
            popupNotice = AppNotice(kind: .error, message: "Apple 系统翻译语言包尚未安装")
            return
        }
        await completePopupAppleTranslation(
            using: TranslationSession(installedSource: source, target: target),
            request: request
        )
    }

    private func finishPopupTranslation(
        _ result: String,
        original: String,
        source: String,
        target: String,
        glossaryMap: [String: String]
    ) {
        popupTranslationTask = nil
        popupAppleTranslationRequest = nil
        popupTranslatedText = restoreGlossary(in: result, replacements: glossaryMap)
        popupIsLoading = false
        popupNotice = nil
        saveHistory(original: original, result: popupTranslatedText, source: source, target: target)
    }

    private func showPopupTranslationError(_ error: Error) {
        popupTranslationTask = nil
        popupAppleTranslationRequest = nil
        popupIsLoading = false
        if let known = error as? AppTranslationError {
            popupNotice = AppNotice(kind: .error, message: known.localizedDescription)
        } else {
            popupNotice = AppNotice(kind: .error, message: "翻译服务暂时不可用，请检查网络后重试")
        }
    }

    func setEngine(_ engine: TranslationEngine) {
        selectedEngine = engine
        UserDefaults.standard.set(engine.rawValue, forKey: engineKey)
        notice = nil
    }

    func addGlossaryEntry(source: String, target: String, sourceLanguage: String, targetLanguage: String) {
        let cleanSource = source.trimmingCharacters(in: .whitespacesAndNewlines)
        let cleanTarget = target.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !cleanSource.isEmpty, !cleanTarget.isEmpty, sourceLanguage != targetLanguage else { return }
        if let index = glossary.firstIndex(where: {
            $0.source.caseInsensitiveCompare(cleanSource) == .orderedSame
                && $0.sourceLanguage == sourceLanguage
                && $0.targetLanguage == targetLanguage
        }) {
            glossary[index].target = cleanTarget
        } else {
            glossary.append(GlossaryEntry(
                source: cleanSource,
                target: cleanTarget,
                sourceLanguage: sourceLanguage,
                targetLanguage: targetLanguage
            ))
        }
        persistGlossary()
    }

    func deleteGlossaryEntries(at offsets: IndexSet) {
        glossary.remove(atOffsets: offsets)
        persistGlossary()
    }

    private func persistGlossary() {
        if let data = try? JSONEncoder().encode(glossary) {
            UserDefaults.standard.set(data, forKey: glossaryKey)
        }
    }

    private func glossaryPreparedText(_ text: String, source: String, target: String) -> (String, [String: String]) {
        let matching = glossary
            .filter { $0.sourceLanguage == source && $0.targetLanguage == target }
            .sorted { $0.source.count > $1.source.count }
        guard !matching.isEmpty else { return (text, [:]) }

        var value = text
        var replacements: [String: String] = [:]
        for (index, entry) in matching.enumerated() {
            let token = "ZXQGLOSSARY\(index)QXZ"
            let replaced = value.replacingOccurrences(
                of: entry.source,
                with: token,
                options: [.caseInsensitive],
                range: nil
            )
            if replaced != value {
                value = replaced
                replacements[token] = entry.target
            }
        }
        return (value, replacements)
    }

    private func restoreGlossary(in text: String, replacements: [String: String]) -> String {
        var value = text
        for (token, replacement) in replacements {
            value = value.replacingOccurrences(of: token, with: replacement, options: [.caseInsensitive])
            let flexibleToken = token.map { NSRegularExpression.escapedPattern(for: String($0)) }
                .joined(separator: "\\s*")
            if let expression = try? NSRegularExpression(pattern: flexibleToken, options: [.caseInsensitive]) {
                let fullRange = NSRange(value.startIndex..<value.endIndex, in: value)
                value = expression.stringByReplacingMatches(
                    in: value,
                    range: fullRange,
                    withTemplate: NSRegularExpression.escapedTemplate(for: replacement)
                )
            }
        }
        return value
    }

    @discardableResult
    func saveDeepLKey(_ value: String) -> Bool {
        let cleanValue = value.trimmingCharacters(in: .whitespacesAndNewlines)
        do {
            try SecureKeyStore.saveDeepLKey(cleanValue)
        } catch {
            setError("无法保存到系统钥匙串：\(error.localizedDescription)")
            return false
        }
        deepLAPIKey = cleanValue
        hasDeepLKey = !cleanValue.isEmpty
        if hasDeepLKey { setEngine(.deepl) }
        deepLUsage = nil
        if hasDeepLKey {
            Task { await self.fetchDeepLUsage() }
        }
        return true
    }

    func setSourceLanguage(_ value: String) {
        if isListening { stopListening() }
        sourceLanguage = value
        if targetLanguage == value {
            targetLanguage = value == "zh-CN" ? "en" : "zh-CN"
        }
    }

    func setTargetLanguage(_ value: String) {
        targetLanguage = value
        if sourceLanguage == value {
            setSourceLanguage(value == "zh-CN" ? "en" : "zh-CN")
        }
    }

    func swapLanguages() {
        guard !hasImageTranslation else { return }
        if isListening { stopListening() }
        cancelSpeechPreload()
        if sourceLanguage == "auto" {
            sourceLanguage = detectSupportedLanguage(in: sourceText)
        }
        (sourceLanguage, targetLanguage) = (targetLanguage, sourceLanguage)
        (sourceText, translatedText) = (translatedText, sourceText)
        notice = nil
        preGenerateResultSpeech()
    }

    func clear() {
        if isListening { stopListening() }
        cancelSpeechPreload()
        stopOnlineSpeech()
        translationTask?.cancel()
        translationTask = nil
        translationRequestGate.cancel()
        imageTranslationTask?.cancel()
        imageTranslationTask = nil
        isLoading = false
        isRecognizingImage = false
        isCapturingRegion = false
        screenCaptureCountdown = 0
        isEditingImageOCR = false
        isInitialImageOCRReview = false
        sourceText = ""
        translatedText = ""
        sourceImage = nil
        translatedImage = nil
        recognizedImageBlocks = []
        imageBlockTranslations = []
        editableImageBlocks = []
        currentImageHistoryID = nil
        appleImageTranslationRequest = nil
        appleTranslationRequest = nil
        notice = nil
    }

    func recognizeAndTranslateImage(at url: URL) {
        beginImageRecognition(at: url, deleteAfterUse: false)
    }

    func captureScreenRegionAndTranslate() {
        guard !isCapturingRegion, !isRecognizingImage, !isLoading else { return }
        isCapturingRegion = true
        screenCaptureCountdown = 3
        notice = AppNotice(kind: .info, message: "请切换到目标窗口，3 秒后开始框选")
        let destination = FileManager.default.temporaryDirectory
            .appendingPathComponent("MacTranslation-Capture-\(UUID().uuidString).png")

        Task {
            for remaining in stride(from: 3, through: 1, by: -1) {
                screenCaptureCountdown = remaining
                notice = AppNotice(kind: .info, message: "请切换到目标窗口，\(remaining) 秒后开始框选")
                try? await Task.sleep(for: .seconds(1))
            }
            screenCaptureCountdown = 0
            notice = AppNotice(kind: .info, message: "拖动选择要翻译的区域，按 Esc 可取消")
            NSApp.hide(nil)
            try? await Task.sleep(for: .milliseconds(220))

            let result = await Task.detached(priority: .userInitiated) { () -> Result<URL, Error> in
                do {
                    let process = Process()
                    process.executableURL = URL(fileURLWithPath: "/usr/sbin/screencapture")
                    process.arguments = ["-i", "-x", destination.path]
                    try process.run()
                    process.waitUntilExit()
                    guard process.terminationStatus == 0,
                          FileManager.default.fileExists(atPath: destination.path) else {
                        throw ImageRecognitionError.unreadableImage
                    }
                    return .success(destination)
                } catch {
                    return .failure(error)
                }
            }.value
            restoreApplicationAfterScreenCapture()
            isCapturingRegion = false
            switch result {
            case .success(let url):
                beginImageRecognition(at: url, deleteAfterUse: true)
            case .failure:
                notice = nil // Region selection was normally cancelled with Esc.
                try? FileManager.default.removeItem(at: destination)
            }
        }
    }

    private func restoreApplicationAfterScreenCapture() {
        NSApp.unhide(nil)
        NSApp.setActivationPolicy(.regular)
        NSApp.activate(ignoringOtherApps: true)
        NSApp.windows.first(where: {
            $0.level == .normal && $0.styleMask.contains(.titled)
        })?.makeKeyAndOrderFront(nil)
    }

    private func beginImageRecognition(at url: URL, deleteAfterUse: Bool) {
        guard !isRecognizingImage else { return }
        isRecognizingImage = true
        notice = nil
        sourceText = ""
        translatedText = ""
        translatedImage = nil
        imageBlockTranslations = []
        editableImageBlocks = []
        currentImageHistoryID = nil
        appleImageTranslationRequest = nil
        appleTranslationRequest = nil
        let hasSecurityScope = url.startAccessingSecurityScopedResource()

        Task {
            defer {
                if hasSecurityScope { url.stopAccessingSecurityScopedResource() }
                if deleteAfterUse { try? FileManager.default.removeItem(at: url) }
            }
            do {
                guard let image = NSImage(contentsOf: url) else {
                    throw ImageRecognitionError.unreadableImage
                }
                sourceImage = image
                let blocks = try await Task.detached(priority: .userInitiated) {
                    try recognizeTextBlocksInImage(at: url)
                }.value
                guard !blocks.isEmpty else { throw ImageRecognitionError.noTextFound }

                var acceptedBlocks: [RecognizedImageBlock] = []
                var acceptedCharacters = 0
                for block in blocks where acceptedCharacters < maxSourceCharacters {
                    let remaining = maxSourceCharacters - acceptedCharacters
                    let limitedText = String(block.text.prefix(remaining))
                    guard !limitedText.isEmpty else { continue }
                    acceptedBlocks.append(RecognizedImageBlock(text: limitedText, boundingBox: block.boundingBox))
                    acceptedCharacters += limitedText.count + 1
                }
                recognizedImageBlocks = acceptedBlocks

                sourceText = acceptedBlocks.map(\.text).joined(separator: "\n")
                let detectedLanguage = detectSupportedLanguage(in: sourceText)
                sourceLanguage = detectedLanguage
                if targetLanguage == detectedLanguage {
                    targetLanguage = detectedLanguage == "en" ? "zh-CN" : "en"
                }
                isRecognizingImage = false
                pendingImageSourceLanguage = detectedLanguage
                pendingImageTargetLanguage = targetLanguage
                isInitialImageOCRReview = true
                isEditingImageOCR = true
            } catch {
                isRecognizingImage = false
                sourceImage = nil
                recognizedImageBlocks = []
                if let known = error as? ImageRecognitionError {
                    setError(known.localizedDescription)
                } else {
                    setError("无法识别这张图片，请换一张更清晰的图片重试")
                }
            }
        }
    }

    func imageOCRBlocksForEditing() -> [RecognizedImageBlock] {
        recognizedImageBlocks
    }

    func confirmImageOCRBlocks(_ blocks: [RecognizedImageBlock]) {
        let acceptedBlocks = blocks.compactMap { block -> RecognizedImageBlock? in
            let text = block.text.trimmingCharacters(in: .whitespacesAndNewlines)
            guard !text.isEmpty else { return nil }
            return RecognizedImageBlock(id: block.id, text: text, boundingBox: block.boundingBox)
        }
        guard !acceptedBlocks.isEmpty else {
            setError("识别文字不能为空")
            return
        }
        recognizedImageBlocks = acceptedBlocks
        sourceText = recognizedImageBlocks.map(\.text).joined(separator: "\n")
        let effectiveSource = sourceLanguage == "auto" ? detectSupportedLanguage(in: sourceText) : sourceLanguage
        pendingImageSourceLanguage = effectiveSource
        pendingImageTargetLanguage = targetLanguage
        isInitialImageOCRReview = false
        isEditingImageOCR = false
        beginImageTranslation(source: pendingImageSourceLanguage, target: pendingImageTargetLanguage)
    }

    func cancelImageOCREditing() {
        isEditingImageOCR = false
        if isInitialImageOCRReview {
            isInitialImageOCRReview = false
            clear()
        }
    }

    func editImageSourceText() {
        guard canEditImageSourceText else { return }
        imageTranslationTask?.cancel()
        imageTranslationTask = nil
        appleImageTranslationRequest = nil
        isLoading = false
        pendingImageSourceLanguage = sourceLanguage == "auto" ? detectSupportedLanguage(in: sourceText) : sourceLanguage
        pendingImageTargetLanguage = targetLanguage
        isInitialImageOCRReview = false
        notice = nil
        isEditingImageOCR = true
    }

    private func detectSupportedLanguage(in text: String) -> String {
        let recognizer = NLLanguageRecognizer()
        recognizer.processString(text)
        if let language = recognizer.dominantLanguage {
            switch language.rawValue {
            case "zh-Hans", "zh-Hant", "zh": return "zh-CN"
            case "ja": return "ja"
            case "ko": return "ko"
            case "en": return "en"
            default: break
            }
        }

        if text.unicodeScalars.contains(where: { (0xAC00...0xD7AF).contains(Int($0.value)) }) { return "ko" }
        if text.unicodeScalars.contains(where: {
            (0x3040...0x30FF).contains(Int($0.value)) || (0x31F0...0x31FF).contains(Int($0.value))
        }) { return "ja" }
        if text.unicodeScalars.contains(where: { (0x4E00...0x9FFF).contains(Int($0.value)) }) { return "zh-CN" }
        return "en"
    }

    func use(_ item: TranslationHistory) {
        cancelSpeechPreload()
        sourceImage = nil
        translatedImage = nil
        recognizedImageBlocks = []
        imageBlockTranslations = []
        editableImageBlocks = []
        currentImageHistoryID = nil
        appleImageTranslationRequest = nil
        sourceText = item.original
        translatedText = item.result
        sourceLanguage = item.source
        targetLanguage = item.target
        notice = nil
        preGenerateResultSpeech()
    }

    func translate() {
        let cleanText = sourceText.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !cleanText.isEmpty, !isLoading else { return }

        if sourceImage != nil, !recognizedImageBlocks.isEmpty {
            let effectiveSource = sourceLanguage == "auto" ? detectSupportedLanguage(in: cleanText) : sourceLanguage
            beginImageTranslation(source: effectiveSource, target: targetLanguage)
            return
        }

        cancelSpeechPreload()
        isLoading = true
        notice = nil
        appleImageTranslationRequest = nil

        let effectiveSource = sourceLanguage == "auto" ? detectSupportedLanguage(in: cleanText) : sourceLanguage
        let prepared = glossaryPreparedText(cleanText, source: effectiveSource, target: targetLanguage)
        let target = targetLanguage
        translationTask?.cancel()
        let requestID = translationRequestGate.begin()

        switch selectedEngine {
        case .apple:
            if #available(macOS 15.0, *) {
                let request = AppleTranslationRequest(
                    id: requestID,
                    text: prepared.0,
                    originalText: cleanText,
                    source: effectiveSource,
                    target: target,
                    glossaryMap: prepared.1
                )
                appleTranslationRequest = request
            } else {
                translationRequestGate.cancel()
                isLoading = false
                setError("Apple 系统翻译需要 macOS 15 或更高版本，请改用 DeepL")
            }
        case .deepl:
            guard hasDeepLKey else {
                translationRequestGate.cancel()
                isLoading = false
                setError("请先设置 DeepL API Free 密钥")
                return
            }
            translationTask = Task { [weak self] in
                await self?.translateWithDeepL(
                    prepared.0,
                    originalText: cleanText,
                    source: effectiveSource,
                    target: target,
                    glossaryMap: prepared.1,
                    requestID: requestID
                )
            }
        }
    }

    @available(macOS 15.0, *)
    func completeAppleTranslation(using session: TranslationSession, request: AppleTranslationRequest) async {
        guard appleTranslationRequest?.id == request.id,
              translationRequestGate.accepts(request.id) else { return }
        do {
            try await session.prepareTranslation()
            let response = try await session.translate(request.text)
            guard appleTranslationRequest?.id == request.id,
                  translationRequestGate.accepts(request.id) else { return }
            acceptTranslation(
                response.targetText,
                original: request.originalText,
                source: request.source,
                target: request.target,
                glossaryMap: request.glossaryMap
            )
            translationRequestGate.cancel()
            translationTask = nil
            appleTranslationRequest = nil
            isLoading = false
        } catch {
            guard appleTranslationRequest?.id == request.id,
                  translationRequestGate.accepts(request.id) else { return }
            translationRequestGate.cancel()
            translationTask = nil
            appleTranslationRequest = nil
            isLoading = false
            setError("Apple 系统翻译暂时无法完成：\(error.localizedDescription)")
        }
    }

    @available(macOS 26.0, *)
    private func translateWithInstalledApple(_ request: AppleTranslationRequest) async {
        guard appleTranslationRequest?.id == request.id,
              translationRequestGate.accepts(request.id),
              let sourceID = appleTranslationLocales[request.source],
              let targetID = appleTranslationLocales[request.target] else { return }

        let source = Locale.Language(identifier: sourceID)
        let target = Locale.Language(identifier: targetID)
        let status = await LanguageAvailability().status(from: source, to: target)
        guard status == .installed else {
            translationRequestGate.cancel()
            translationTask = nil
            appleTranslationRequest = nil
            isLoading = false
            setError("Apple 系统翻译语言包尚未安装，请先在系统设置的语言与地区中下载对应语言")
            return
        }
        let session = TranslationSession(installedSource: source, target: target)
        await completeAppleTranslation(using: session, request: request)
    }

    private func beginImageTranslation(source: String, target: String) {
        guard let sourceImage, !recognizedImageBlocks.isEmpty, !isLoading else { return }
        cancelSpeechPreload()
        stopOnlineSpeech()
        isLoading = true
        notice = nil
        translatedText = ""
        translatedImage = nil
        appleTranslationRequest = nil

        switch selectedEngine {
        case .apple:
            if #available(macOS 15.0, *) {
                let request = AppleImageTranslationRequest(
                    blocks: recognizedImageBlocks,
                    source: source,
                    target: target
                )
                appleImageTranslationRequest = request
            } else {
                isLoading = false
                setError("Apple 系统翻译需要 macOS 15 或更高版本，请改用 DeepL")
            }
        case .deepl:
            guard hasDeepLKey else {
                isLoading = false
                setError("请先设置 DeepL API Free 密钥")
                return
            }
            let blocks = recognizedImageBlocks
            imageTranslationTask?.cancel()
            imageTranslationTask = Task { [weak self] in
                guard let self else { return }
                await self.translateImageWithDeepL(sourceImage: sourceImage, blocks: blocks, source: source, target: target)
            }
        }
    }

    @available(macOS 15.0, *)
    func completeAppleImageTranslation(using session: TranslationSession, request: AppleImageTranslationRequest) async {
        guard appleImageTranslationRequest?.id == request.id, let sourceImage else { return }
        do {
            try await session.prepareTranslation()
            var translations: [String] = []
            translations.reserveCapacity(request.blocks.count)
            for block in request.blocks {
                let prepared = glossaryPreparedText(block.text, source: request.source, target: request.target)
                let response = try await session.translate(prepared.0)
                guard appleImageTranslationRequest?.id == request.id else { return }
                translations.append(restoreGlossary(in: response.targetText, replacements: prepared.1))
            }
            finishImageTranslation(
                sourceImage: sourceImage,
                blocks: request.blocks,
                translations: translations,
                source: request.source,
                target: request.target
            )
            appleImageTranslationRequest = nil
            imageTranslationTask = nil
        } catch {
            guard appleImageTranslationRequest?.id == request.id else { return }
            appleImageTranslationRequest = nil
            imageTranslationTask = nil
            isLoading = false
            setError("Apple 系统图片翻译暂时无法完成：\(error.localizedDescription)")
        }
    }

    @available(macOS 26.0, *)
    private func translateImageWithInstalledApple(_ request: AppleImageTranslationRequest) async {
        guard appleImageTranslationRequest?.id == request.id,
              let sourceID = appleTranslationLocales[request.source],
              let targetID = appleTranslationLocales[request.target] else { return }
        let source = Locale.Language(identifier: sourceID)
        let target = Locale.Language(identifier: targetID)
        guard await LanguageAvailability().status(from: source, to: target) == .installed else {
            appleImageTranslationRequest = nil
            imageTranslationTask = nil
            isLoading = false
            setError("Apple 系统翻译语言包尚未安装，请先下载对应语言")
            return
        }
        await completeAppleImageTranslation(
            using: TranslationSession(installedSource: source, target: target),
            request: request
        )
    }

    private func translateImageWithDeepL(
        sourceImage: NSImage,
        blocks: [RecognizedImageBlock],
        source: String,
        target: String
    ) async {
        do {
            var maps: [[String: String]] = []
            let preparedTexts = blocks.map { block -> String in
                let prepared = glossaryPreparedText(block.text, source: source, target: target)
                maps.append(prepared.1)
                return prepared.0
            }
            let rawTranslations = try await deepLTranslations(for: preparedTexts, source: source, target: target)
            let translations = zip(rawTranslations, maps).map { restoreGlossary(in: $0.0, replacements: $0.1) }
            finishImageTranslation(
                sourceImage: sourceImage,
                blocks: blocks,
                translations: translations,
                source: source,
                target: target
            )
            Task { await self.fetchDeepLUsage() }
        } catch {
            isLoading = false
            showTranslationError(error)
        }
    }

    private func deepLTranslations(for texts: [String], source: String, target: String) async throws -> [String] {
        guard let url = URL(string: "https://api-free.deepl.com/v2/translate") else {
            throw AppTranslationError.invalidRequest
        }
        let sourceCodes = ["en": "EN", "zh-CN": "ZH", "ja": "JA", "ko": "KO"]
        let targetCodes = ["en": "EN-US", "zh-CN": "ZH-HANS", "ja": "JA", "ko": "KO"]
        guard let sourceCode = sourceCodes[source], let targetCode = targetCodes[target] else {
            throw AppTranslationError.invalidRequest
        }

        var results: [String] = []
        let configuration = URLSessionConfiguration.ephemeral
        configuration.timeoutIntervalForRequest = 20
        configuration.timeoutIntervalForResource = 30
        let session = URLSession(configuration: configuration)
        defer { session.finishTasksAndInvalidate() }
        for batchStart in stride(from: 0, to: texts.count, by: 40) {
            let batchEnd = min(batchStart + 40, texts.count)
            let batch = Array(texts[batchStart..<batchEnd])
            var request = URLRequest(url: url)
            request.httpMethod = "POST"
            request.timeoutInterval = 20
            request.setValue("DeepL-Auth-Key \(deepLAPIKey)", forHTTPHeaderField: "Authorization")
            request.setValue("application/json", forHTTPHeaderField: "Content-Type")
            request.httpBody = try JSONSerialization.data(withJSONObject: [
                "text": batch,
                "source_lang": sourceCode,
                "target_lang": targetCode
            ])

            let (data, response) = try await session.data(for: request)
            guard let http = response as? HTTPURLResponse else { throw AppTranslationError.serviceUnavailable }
            switch http.statusCode {
            case 200: break
            case 403: throw AppTranslationError.provider("DeepL 密钥无效，请重新设置")
            case 456: throw AppTranslationError.provider("DeepL 本月免费字符额度已经用完")
            case 429: throw AppTranslationError.provider("DeepL 请求过于频繁，请稍后再试")
            default: throw AppTranslationError.serviceUnavailable
            }
            let payload = try JSONDecoder().decode(DeepLTranslationPayload.self, from: data)
            guard payload.translations.count == batch.count else {
                throw AppTranslationError.provider("DeepL 返回的图片译文数量不完整")
            }
            results.append(contentsOf: payload.translations.map(\.text))
        }
        return results
    }

    private func finishImageTranslation(
        sourceImage: NSImage,
        blocks: [RecognizedImageBlock],
        translations: [String],
        source: String,
        target: String
    ) {
        guard translations.count == blocks.count else {
            isLoading = false
            setError("图片译文数量与识别结果不一致，请重新尝试")
            return
        }
        imageBlockTranslations = translations
        imageTranslationTask = nil
        editableImageBlocks = zip(blocks, translations).map { block, translation in
            EditableImageTranslationBlock(
                id: block.id,
                originalText: block.text,
                translatedText: translation,
                boundingBox: block.boundingBox
            )
        }
        translatedImage = renderedTranslationImage(
            source: sourceImage,
            edits: editableImageBlocks,
            style: imageRenderStyle
        )
        translatedText = translations.joined(separator: "\n")
        saveHistory(original: blocks.map(\.text).joined(separator: "\n"), result: translatedText, source: source, target: target)
        saveImageHistory(sourceImage: sourceImage, source: source, target: target)
        isLoading = false
    }

    func setImageRenderStyle(_ style: ImageTranslationRenderStyle) {
        guard imageRenderStyle != style else { return }
        imageRenderStyle = style
        UserDefaults.standard.set(style.rawValue, forKey: imageRenderStyleKey)
        guard let sourceImage,
              !editableImageBlocks.isEmpty else { return }
        translatedImage = renderedTranslationImage(
            source: sourceImage,
            edits: editableImageBlocks,
            style: style
        )
        updateCurrentImageHistory()
    }

    func applyImageEdits(_ edits: [EditableImageTranslationBlock]) {
        guard let sourceImage else { return }
        editableImageBlocks = edits
        translatedText = edits.map(\.translatedText).joined(separator: "\n")
        translatedImage = renderedTranslationImage(
            source: sourceImage,
            edits: edits,
            style: imageRenderStyle
        )
        updateCurrentImageHistory()
        setSuccess("译文框修改已应用")
    }

    func resetImageEdits() -> [EditableImageTranslationBlock] {
        zip(recognizedImageBlocks, imageBlockTranslations).map { block, translation in
            EditableImageTranslationBlock(
                id: block.id,
                originalText: block.text,
                translatedText: translation,
                boundingBox: block.boundingBox
            )
        }
    }

    func copyTranslatedImage() {
        guard let translatedImage else { return }
        NSPasteboard.general.clearContents()
        NSPasteboard.general.writeObjects([translatedImage])
        imageActionLabel = "已复制 ✓"
        Task {
            try? await Task.sleep(for: .seconds(1.4))
            imageActionLabel = "复制图片"
        }
    }

    func saveTranslatedImage() {
        guard let translatedImage,
              let tiffData = translatedImage.tiffRepresentation,
              let representation = NSBitmapImageRep(data: tiffData),
              let pngData = representation.representation(using: .png, properties: [:]) else {
            setError("无法生成可保存的译文图片")
            return
        }
        let panel = NSSavePanel()
        panel.title = "保存译文图片"
        panel.nameFieldStringValue = "译文图片.png"
        panel.allowedContentTypes = [.png]
        panel.canCreateDirectories = true
        guard panel.runModal() == .OK, let destination = panel.url else { return }
        do {
            try pngData.write(to: destination, options: .atomic)
            imageActionLabel = "保存成功 ✓"
            Task {
                try? await Task.sleep(for: .seconds(1.6))
                imageActionLabel = "复制图片"
            }
        } catch {
            setError("保存译文图片失败：\(error.localizedDescription)")
        }
    }

    private func translateWithDeepL(
        _ cleanText: String,
        originalText: String,
        source: String,
        target: String,
        glossaryMap: [String: String],
        requestID: UUID
    ) async {
        do {
            guard let url = URL(string: "https://api-free.deepl.com/v2/translate") else {
                throw AppTranslationError.invalidRequest
            }
            let deepLLanguages: [String: String] = [
                "en": "EN",
                "zh-CN": "ZH",
                "ja": "JA",
                "ko": "KO"
            ]
            let deepLTargets: [String: String] = [
                "en": "EN-US",
                "zh-CN": "ZH-HANS",
                "ja": "JA",
                "ko": "KO"
            ]
            guard let sourceCode = deepLLanguages[source],
                  let targetCode = deepLTargets[target] else {
                throw AppTranslationError.invalidRequest
            }

            var request = URLRequest(url: url)
            request.httpMethod = "POST"
            request.timeoutInterval = 15
            request.setValue("DeepL-Auth-Key \(deepLAPIKey)", forHTTPHeaderField: "Authorization")
            request.setValue("application/json", forHTTPHeaderField: "Content-Type")
            request.httpBody = try JSONSerialization.data(withJSONObject: [
                "text": [cleanText],
                "source_lang": sourceCode,
                "target_lang": targetCode
            ])

            let (data, response) = try await URLSession.shared.data(for: request)
            guard !Task.isCancelled, translationRequestGate.accepts(requestID) else { return }
            guard let http = response as? HTTPURLResponse else {
                throw AppTranslationError.serviceUnavailable
            }
            switch http.statusCode {
            case 200: break
            case 403: throw AppTranslationError.provider("DeepL 密钥无效，请重新设置")
            case 456: throw AppTranslationError.provider("DeepL 本月免费字符额度已经用完")
            case 429: throw AppTranslationError.provider("DeepL 请求过于频繁，请稍后再试")
            default: throw AppTranslationError.serviceUnavailable
            }

            let payload = try JSONDecoder().decode(DeepLTranslationPayload.self, from: data)
            guard let result = payload.translations.first?.text, !result.isEmpty else {
                throw AppTranslationError.provider("DeepL 没有返回译文")
            }
            guard translationRequestGate.accepts(requestID) else { return }
            acceptTranslation(
                result,
                original: originalText,
                source: source,
                target: target,
                glossaryMap: glossaryMap
            )
            finishTextTranslation(requestID: requestID)
            Task { await self.fetchDeepLUsage() }
        } catch {
            guard !Task.isCancelled, translationRequestGate.accepts(requestID) else { return }
            finishTextTranslation(requestID: requestID)
            showTranslationError(error)
        }
    }

    private func finishTextTranslation(requestID: UUID) {
        guard translationRequestGate.accepts(requestID) else { return }
        translationRequestGate.cancel()
        translationTask = nil
        isLoading = false
    }

    func fetchDeepLUsage() async {
        guard hasDeepLKey, !deepLAPIKey.isEmpty else {
            deepLUsage = nil
            return
        }
        guard let url = URL(string: "https://api-free.deepl.com/v2/usage") else { return }
        var request = URLRequest(url: url)
        request.timeoutInterval = 10
        request.setValue("DeepL-Auth-Key \(deepLAPIKey)", forHTTPHeaderField: "Authorization")
        do {
            let (data, response) = try await URLSession.shared.data(for: request)
            guard let http = response as? HTTPURLResponse, http.statusCode == 200 else { return }
            let usage = try JSONDecoder().decode(DeepLUsagePayload.self, from: data)
            deepLUsage = usage
        } catch {
            // 用量获取失败不影响翻译主流程
        }
    }

    private func showTranslationError(_ error: Error) {
        if let known = error as? AppTranslationError {
            setError(known.localizedDescription)
        } else {
            setError("翻译服务暂时不可用，请检查网络后重试")
        }
    }

    private func acceptTranslation(
        _ result: String,
        original: String,
        source: String,
        target: String,
        glossaryMap: [String: String]
    ) {
        let finalResult = restoreGlossary(in: result, replacements: glossaryMap)
        translatedText = finalResult
        saveHistory(original: original, result: finalResult, source: source, target: target)
        preGenerateResultSpeech(text: finalResult, language: target)
    }

    func copyResult() {
        guard !translatedText.isEmpty else { return }
        NSPasteboard.general.clearContents()
        NSPasteboard.general.setString(translatedText, forType: .string)
        copyLabel = "已复制 ✓"
        Task {
            try? await Task.sleep(for: .seconds(1.4))
            copyLabel = "复制译文"
        }
    }

    func copyPopupResult() {
        guard !popupTranslatedText.isEmpty else { return }
        NSPasteboard.general.clearContents()
        NSPasteboard.general.setString(popupTranslatedText, forType: .string)
        copyLabel = "已复制 ✓"
        Task {
            try? await Task.sleep(for: .seconds(1.4))
            copyLabel = "复制译文"
        }
    }

    func replaceSelectedText(in processID: pid_t) {
        guard !popupTranslatedText.isEmpty,
              let application = NSRunningApplication(processIdentifier: processID) else { return }
        let pasteboard = NSPasteboard.general
        let snapshot = pasteboardSnapshot(pasteboard)
        pasteboard.clearContents()
        pasteboard.setString(popupTranslatedText, forType: .string)
        application.activate(options: [.activateIgnoringOtherApps])
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.12) {
            guard let source = CGEventSource(stateID: .hidSystemState),
                  let keyDown = CGEvent(keyboardEventSource: source, virtualKey: CGKeyCode(kVK_ANSI_V), keyDown: true),
                  let keyUp = CGEvent(keyboardEventSource: source, virtualKey: CGKeyCode(kVK_ANSI_V), keyDown: false) else { return }
            keyDown.flags = .maskCommand
            keyUp.flags = .maskCommand
            keyDown.post(tap: .cghidEventTap)
            keyUp.post(tap: .cghidEventTap)
            DispatchQueue.main.asyncAfter(deadline: .now() + 0.30) { [weak self] in
                self?.restorePasteboard(snapshot, to: pasteboard)
            }
        }
    }

    func speakSource() {
        guard !sourceText.isEmpty else { return }
        let language = sourceLanguage == "auto" ? detectSupportedLanguage(in: sourceText) : sourceLanguage
        startOnlineSpeech(sourceText, language: language)
    }

    func speakResult() {
        guard !translatedText.isEmpty else { return }
        if let player = resultAudioPlayer, player.isPlaying {
            player.pause()
            isSpeakingResult = true
            isResultSpeechPaused = true
        } else if let player = resultAudioPlayer, isResultSpeechPaused {
            player.play()
            isSpeakingResult = true
            isResultSpeechPaused = false
        } else if isPreparingSpeech {
            stopOnlineSpeech()
        } else {
            startOnlineSpeech(translatedText, language: targetLanguage)
        }
    }

    func setOnlineVoicePersona(_ persona: OnlineVoicePersona) {
        guard onlineVoicePersona != persona else { return }
        stopOnlineSpeech()
        cancelSpeechPreload()
        onlineVoicePersona = persona
        UserDefaults.standard.set(persona.rawValue, forKey: voicePersonaKey)
        preGenerateResultSpeech()
    }

    func setOnlineSpeechRatePercent(_ value: Int) {
        let clamped = min(30, max(-30, value))
        guard onlineSpeechRatePercent != clamped else { return }
        stopOnlineSpeech()
        cancelSpeechPreload()
        onlineSpeechRatePercent = clamped
        UserDefaults.standard.set(clamped, forKey: speechRateKey)
        preGenerateResultSpeech()
    }

    var speechRateDisplayText: String {
        onlineSpeechRatePercent > 0 ? "+\(onlineSpeechRatePercent)%" : "\(onlineSpeechRatePercent)%"
    }

    var speechCacheSizeText: String {
        ByteCountFormatter.string(fromByteCount: speechCacheBytes, countStyle: .file)
    }

    var accessibilityPermissionText: String {
        AXIsProcessTrusted() ? "已允许" : "未允许"
    }

    var speechRecognitionPermissionText: String {
        switch SFSpeechRecognizer.authorizationStatus() {
        case .authorized: return "已允许"
        case .denied, .restricted: return "未允许"
        case .notDetermined: return "尚未请求"
        @unknown default: return "未知"
        }
    }

    var microphonePermissionText: String {
        switch AVCaptureDevice.authorizationStatus(for: .audio) {
        case .authorized: return "已允许"
        case .denied, .restricted: return "未允许"
        case .notDetermined: return "尚未请求"
        @unknown default: return "未知"
        }
    }

    func clearSpeechCache() {
        cancelSpeechPreload()
        stopOnlineSpeech()
        let directory = onlineSpeechCacheDirectory
        if let files = try? FileManager.default.contentsOfDirectory(
            at: directory,
            includingPropertiesForKeys: nil,
            options: [.skipsHiddenFiles]
        ) {
            for file in files { try? FileManager.default.removeItem(at: file) }
        }
        refreshSpeechCacheSize()
        speechStatusMessage = "语音缓存已清理"
    }

    func refreshSpeechCacheSize() {
        let keys: Set<URLResourceKey> = [.fileSizeKey]
        let files = (try? FileManager.default.contentsOfDirectory(
            at: onlineSpeechCacheDirectory,
            includingPropertiesForKeys: Array(keys),
            options: [.skipsHiddenFiles]
        )) ?? []
        speechCacheBytes = files.reduce(0) { partial, file in
            partial + Int64((try? file.resourceValues(forKeys: keys).fileSize) ?? 0)
        }
    }

    nonisolated func audioPlayerDidFinishPlaying(_ player: AVAudioPlayer, successfully flag: Bool) {
        Task { @MainActor [weak self] in
            self?.isSpeakingResult = false
            self?.isResultSpeechPaused = false
            self?.speechStatusMessage = nil
            self?.speechHighlightedSentenceIndex = -1
            self?.speechHighlightTask?.cancel()
            self?.speechHighlightTask = nil
        }
    }

    nonisolated func speechSynthesizer(_ synthesizer: AVSpeechSynthesizer, didFinish utterance: AVSpeechUtterance) {
        Task { @MainActor [weak self] in
            self?.isSpeakingResult = false
            self?.isResultSpeechPaused = false
            self?.speechHighlightedSentenceIndex = -1
            self?.speechStatusMessage = nil
        }
    }

    var highlightedTranslatedText: AttributedString {
        var value = AttributedString(translatedText)
        guard speechHighlightedSentenceIndex >= 0,
              speechHighlightedSentenceIndex < speechSentences.count else { return value }
        let sentence = speechSentences[speechHighlightedSentenceIndex]
        guard let range = translatedText.range(of: sentence),
              let lower = AttributedString.Index(range.lowerBound, within: value),
              let upper = AttributedString.Index(range.upperBound, within: value) else { return value }
        value[lower..<upper].backgroundColor = .blue.opacity(0.18)
        value[lower..<upper].foregroundColor = .blue
        return value
    }

    private func startOnlineSpeech(_ text: String, language: String) {
        stopOnlineSpeech()
        guard let voice = OnlineTTSService.voice(for: language, persona: onlineVoicePersona) else {
            speechStatusMessage = "在线朗读仅支持中文、英文、日文和韩文"
            return
        }
        let cacheDirectory = onlineSpeechCacheDirectory
        let rate = onlineSpeechRate
        let cacheKey = OnlineTTSService.cacheKey(text: text, language: language, voice: voice, rate: rate)
        let audioURL = cacheDirectory.appendingPathComponent("\(cacheKey).mp3")
        speechAudioURL = audioURL

        if FileManager.default.fileExists(atPath: audioURL.path) {
            playOnlineAudio(at: audioURL)
            return
        }

        isPreparingSpeech = true
        let matchingPreload = speechPreloadKey == cacheKey ? speechPreloadTask : nil
        speechStatusMessage = matchingPreload == nil ? "正在联网生成自然语音…" : "语音即将就绪…"
        speechTask = Task { [weak self] in
            guard let self else { return }
            do {
                let resultURL: URL
                if let matchingPreload, let preloadedURL = await matchingPreload.value {
                    resultURL = preloadedURL
                } else {
                    resultURL = try await self.onlineSpeechService.synthesize(
                        text: text,
                        voice: voice,
                        outputURL: audioURL,
                        rate: rate
                    )
                }
                guard !Task.isCancelled, self.speechAudioURL == audioURL else { return }
                self.isPreparingSpeech = false
                self.playOnlineAudio(at: resultURL)
                self.trimOnlineSpeechCache()
            } catch is CancellationError {
                self.isPreparingSpeech = false
            } catch {
                guard self.speechAudioURL == audioURL else { return }
                self.isPreparingSpeech = false
                self.startSystemSpeechFallback(text: text, language: language)
            }
        }
    }

    private func preGenerateResultSpeech(text: String? = nil, language: String? = nil) {
        let value = (text ?? translatedText).trimmingCharacters(in: .whitespacesAndNewlines)
        let language = language ?? targetLanguage
        guard !value.isEmpty,
              let voice = OnlineTTSService.voice(for: language, persona: onlineVoicePersona) else { return }

        let cacheKey = OnlineTTSService.cacheKey(
            text: value,
            language: language,
            voice: voice,
            rate: onlineSpeechRate
        )
        let audioURL = onlineSpeechCacheDirectory.appendingPathComponent("\(cacheKey).mp3")
        guard !FileManager.default.fileExists(atPath: audioURL.path) else { return }

        cancelSpeechPreload()
        speechPreloadKey = cacheKey
        speechPreloadTask = Task { [weak self] in
            guard let self else { return nil }
            do {
                let resultURL = try await self.onlineSpeechService.synthesize(
                    text: value,
                    voice: voice,
                    outputURL: audioURL,
                    rate: self.onlineSpeechRate
                )
                guard !Task.isCancelled else { return nil }
                self.trimOnlineSpeechCache()
                if self.speechPreloadKey == cacheKey {
                    self.speechPreloadKey = nil
                    self.speechPreloadTask = nil
                }
                return resultURL
            } catch {
                if self.speechPreloadKey == cacheKey {
                    self.speechPreloadKey = nil
                    self.speechPreloadTask = nil
                }
                return nil
            }
        }
    }

    private func cancelSpeechPreload() {
        speechPreloadTask?.cancel()
        speechPreloadTask = nil
        speechPreloadKey = nil
    }

    private var onlineSpeechCacheDirectory: URL {
        FileManager.default.urls(for: .cachesDirectory, in: .userDomainMask)[0]
            .appendingPathComponent("com.yijian.translator/OnlineSpeech", isDirectory: true)
    }

    private func trimOnlineSpeechCache(maximumBytes: Int = 40 * 1024 * 1024) {
        let directory = onlineSpeechCacheDirectory
        do {
            try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
            let keys: Set<URLResourceKey> = [.fileSizeKey, .contentModificationDateKey]
            let files = try FileManager.default.contentsOfDirectory(
                at: directory,
                includingPropertiesForKeys: Array(keys),
                options: [.skipsHiddenFiles]
            )
            var entries = files.compactMap { url -> (URL, Int, Date)? in
                guard let values = try? url.resourceValues(forKeys: keys),
                      let size = values.fileSize else { return nil }
                return (url, size, values.contentModificationDate ?? .distantPast)
            }
            var total = entries.reduce(0) { $0 + $1.1 }
            entries.sort { $0.2 < $1.2 }
            for entry in entries where total > maximumBytes {
                try? FileManager.default.removeItem(at: entry.0)
                total -= entry.1
            }
            speechCacheBytes = Int64(total)
        } catch {
            // Cache maintenance must never interrupt translation or speech.
        }
    }

    private func playOnlineAudio(at url: URL) {
        do {
            let player = try AVAudioPlayer(contentsOf: url)
            player.delegate = self
            player.prepareToPlay()
            resultAudioPlayer = player
            speechStatusMessage = nil
            isSpeakingResult = true
            isResultSpeechPaused = false
            speechSentences = sentenceParts(in: translatedText)
            startSpeechHighlighting(player: player)
            player.play()
        } catch {
            isSpeakingResult = false
            speechStatusMessage = "无法播放在线音频：\(error.localizedDescription)"
        }
    }

    private func stopOnlineSpeech() {
        resultAudioPlayer?.stop()
        resultAudioPlayer = nil
        fallbackSpeechSynthesizer.stopSpeaking(at: .immediate)
        speechTask?.cancel()
        speechTask = nil
        speechHighlightTask?.cancel()
        speechHighlightTask = nil
        speechHighlightedSentenceIndex = -1
        isPreparingSpeech = false
        isSpeakingResult = false
        isResultSpeechPaused = false
        speechStatusMessage = nil
        speechAudioURL = nil
    }

    private func sentenceParts(in text: String) -> [String] {
        var values: [String] = []
        text.enumerateSubstrings(in: text.startIndex..<text.endIndex, options: [.bySentences, .substringNotRequired]) { _, range, _, _ in
            let sentence = String(text[range]).trimmingCharacters(in: .whitespacesAndNewlines)
            if !sentence.isEmpty { values.append(sentence) }
        }
        return values.isEmpty && !text.isEmpty ? [text] : values
    }

    private func startSpeechHighlighting(player: AVAudioPlayer) {
        speechHighlightTask?.cancel()
        guard !speechSentences.isEmpty else { return }
        speechHighlightTask = Task { @MainActor [weak self, weak player] in
            while !Task.isCancelled, let self, let player, player.duration > 0 {
                let progress = min(0.999, max(0, player.currentTime / player.duration))
                self.speechHighlightedSentenceIndex = min(
                    self.speechSentences.count - 1,
                    Int(progress * Double(self.speechSentences.count))
                )
                try? await Task.sleep(for: .milliseconds(120))
            }
        }
    }

    private func startSystemSpeechFallback(text: String, language: String) {
        let utterance = AVSpeechUtterance(string: text)
        utterance.voice = AVSpeechSynthesisVoice(language: speechLocales[language] ?? "en-US")
        utterance.rate = max(0.35, min(0.60, 0.48 + Float(onlineSpeechRatePercent) / 200))
        speechStatusMessage = "在线语音暂不可用，已使用系统备用语音"
        isSpeakingResult = true
        isResultSpeechPaused = false
        fallbackSpeechSynthesizer.speak(utterance)
    }

    func toggleListening() {
        if isListening {
            stopListening()
            return
        }

        Task {
            let speechStatus = await requestSpeechAuthorization()
            guard speechStatus == .authorized else {
                setError("请在系统设置中允许“Mac翻译”使用语音识别", action: .openSpeechRecognitionSettings)
                return
            }

            let microphoneAllowed = await AVCaptureDevice.requestAccess(for: .audio)
            guard microphoneAllowed else {
                setError("请在系统设置中允许“Mac翻译”使用麦克风", action: .openMicrophoneSettings)
                return
            }

            startListening()
        }
    }

    func stopListening() {
        guard isListening || audioEngine.isRunning else { return }
        silenceTask?.cancel()
        silenceTask = nil
        audioEngine.stop()
        audioEngine.inputNode.removeTap(onBus: 0)
        recognitionRequest?.endAudio()
        recognitionTask?.cancel()
        recognitionTask = nil
        recognitionRequest = nil
        isListening = false
        if !lastRecognizedText.isEmpty {
            sourceText = lastRecognizedText
        }
    }

    private func requestSpeechAuthorization() async -> SFSpeechRecognizerAuthorizationStatus {
        await withCheckedContinuation { continuation in
            SFSpeechRecognizer.requestAuthorization { status in
                continuation.resume(returning: status)
            }
        }
    }

    private func startListening() {
        let listenLanguage = sourceLanguage == "auto" ? detectSupportedLanguage(in: sourceText) : sourceLanguage
        guard let localeID = speechLocales[listenLanguage],
              let recognizer = SFSpeechRecognizer(locale: Locale(identifier: localeID)),
              recognizer.isAvailable else {
            setError("当前原文语言暂时无法使用语音识别")
            return
        }

        recognitionTask?.cancel()
        recognitionTask = nil
        let request = SFSpeechAudioBufferRecognitionRequest()
        request.shouldReportPartialResults = true
        recognitionRequest = request
        speechBaseText = sourceText.trimmingCharacters(in: .whitespacesAndNewlines)
        lastRecognizedText = speechBaseText

        let inputNode = audioEngine.inputNode
        let format = inputNode.outputFormat(forBus: 0)
        inputNode.installTap(onBus: 0, bufferSize: 1024, format: format) { buffer, _ in
            request.append(buffer)
        }

        audioEngine.prepare()
        do {
            try audioEngine.start()
            isListening = true
            notice = nil
            scheduleAutomaticStop(after: 8.0)
        } catch {
            inputNode.removeTap(onBus: 0)
            recognitionRequest = nil
            setError("无法启动麦克风，请稍后重试")
            return
        }

        recognitionTask = recognizer.recognitionTask(with: request) { [weak self] result, error in
            Task { @MainActor in
                guard let self else { return }
                if let result {
                    let spokenText = result.bestTranscription.formattedString.trimmingCharacters(in: .whitespacesAndNewlines)
                    if !spokenText.isEmpty {
                        let combined = self.speechBaseText.isEmpty ? spokenText : "\(self.speechBaseText) \(spokenText)"
                        self.lastRecognizedText = String(combined.prefix(maxSourceCharacters))
                        self.sourceText = self.lastRecognizedText
                    }
                    self.scheduleAutomaticStop(after: 4.0)
                }
                if error != nil { self.stopListening() }
            }
        }
    }

    private func scheduleAutomaticStop(after seconds: Double) {
        silenceTask?.cancel()
        silenceTask = Task { @MainActor [weak self] in
            try? await Task.sleep(nanoseconds: UInt64(seconds * 1_000_000_000))
            guard !Task.isCancelled, let self, self.isListening else { return }
            self.stopListening()
        }
    }

    private var imageHistoryDirectory: URL {
        let base = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
            .appendingPathComponent("Mac翻译", isDirectory: true)
            .appendingPathComponent("ImageHistory", isDirectory: true)
        try? FileManager.default.createDirectory(
            at: base,
            withIntermediateDirectories: true,
            attributes: [.posixPermissions: 0o700]
        )
        return base
    }

    private func pngData(for image: NSImage) -> Data? {
        guard let tiff = image.tiffRepresentation,
              let representation = NSBitmapImageRep(data: tiff) else { return nil }
        return representation.representation(using: .png, properties: [:])
    }

    private func saveImageHistory(sourceImage: NSImage, source: String, target: String) {
        guard imageHistoryRecordingEnabled,
              let translatedImage,
              let sourceData = pngData(for: sourceImage),
              let translatedData = pngData(for: translatedImage) else {
            currentImageHistoryID = nil
            return
        }
        let id = UUID()
        let sourceFileName = "\(id.uuidString)-source.png"
        let translatedFileName = "\(id.uuidString)-translated.png"
        let sourceURL = imageHistoryDirectory.appendingPathComponent(sourceFileName)
        let translatedURL = imageHistoryDirectory.appendingPathComponent(translatedFileName)
        do {
            try sourceData.write(to: sourceURL, options: .atomic)
            try translatedData.write(to: translatedURL, options: .atomic)
        } catch {
            try? FileManager.default.removeItem(at: sourceURL)
            try? FileManager.default.removeItem(at: translatedURL)
            currentImageHistoryID = nil
            return
        }

        imageHistory.insert(ImageTranslationHistory(
            id: id,
            date: Date(),
            source: source,
            target: target,
            engine: selectedEngine.shortName,
            style: imageRenderStyle,
            sourceFileName: sourceFileName,
            translatedFileName: translatedFileName,
            originalText: sourceText,
            translatedText: translatedText,
            blocks: editableImageBlocks
        ), at: 0)
        if imageHistory.count > 10 {
            for item in imageHistory.dropFirst(10) {
                removeImageHistoryFiles(item)
            }
            imageHistory = Array(imageHistory.prefix(10))
        }
        currentImageHistoryID = id
        persistImageHistory()
    }

    private func updateCurrentImageHistory() {
        guard let currentImageHistoryID,
              let translatedImage,
              let translatedData = pngData(for: translatedImage),
              let index = imageHistory.firstIndex(where: { $0.id == currentImageHistoryID }) else { return }
        let destination = imageHistoryDirectory.appendingPathComponent(imageHistory[index].translatedFileName)
        guard (try? translatedData.write(to: destination, options: .atomic)) != nil else { return }
        imageHistory[index].date = Date()
        imageHistory[index].style = imageRenderStyle
        imageHistory[index].translatedText = translatedText
        imageHistory[index].blocks = editableImageBlocks
        persistImageHistory()
    }

    func imageHistoryImage(_ item: ImageTranslationHistory, translated: Bool) -> NSImage? {
        let name = translated ? item.translatedFileName : item.sourceFileName
        return NSImage(contentsOf: imageHistoryDirectory.appendingPathComponent(name))
    }

    func use(_ item: ImageTranslationHistory) {
        guard let source = imageHistoryImage(item, translated: false),
              let translated = imageHistoryImage(item, translated: true) else {
            setError("这条图片历史的文件已不存在")
            return
        }
        cancelSpeechPreload()
        sourceImage = source
        translatedImage = translated
        sourceLanguage = item.source
        targetLanguage = item.target
        imageRenderStyle = item.style
        sourceText = item.originalText
        translatedText = item.translatedText
        editableImageBlocks = item.blocks
        recognizedImageBlocks = item.blocks.map {
            RecognizedImageBlock(id: $0.id, text: $0.originalText, boundingBox: $0.boundingBox)
        }
        imageBlockTranslations = item.blocks.map(\.translatedText)
        currentImageHistoryID = item.id
        notice = nil
    }

    func deleteImageHistory(ids: Set<UUID>) {
        guard !ids.isEmpty else { return }
        let removed = imageHistory.filter { ids.contains($0.id) }
        removed.forEach(removeImageHistoryFiles)
        imageHistory.removeAll { ids.contains($0.id) }
        if let currentImageHistoryID, ids.contains(currentImageHistoryID) {
            self.currentImageHistoryID = nil
        }
        persistImageHistory()
    }

    func clearImageHistory() {
        imageHistory.forEach(removeImageHistoryFiles)
        imageHistory = []
        currentImageHistoryID = nil
        UserDefaults.standard.removeObject(forKey: imageHistoryKey)
    }

    private func removeImageHistoryFiles(_ item: ImageTranslationHistory) {
        try? FileManager.default.removeItem(at: imageHistoryDirectory.appendingPathComponent(item.sourceFileName))
        try? FileManager.default.removeItem(at: imageHistoryDirectory.appendingPathComponent(item.translatedFileName))
    }

    func setImageHistoryRecording(_ enabled: Bool) {
        imageHistoryRecordingEnabled = enabled
        UserDefaults.standard.set(enabled, forKey: imageHistoryEnabledKey)
    }

    private func persistImageHistory() {
        if let data = try? JSONEncoder().encode(imageHistory) {
            UserDefaults.standard.set(data, forKey: imageHistoryKey)
        }
    }

    func clearHistory() {
        history = []
        UserDefaults.standard.removeObject(forKey: historyKey)
    }

    func deleteHistory(ids: Set<UUID>) {
        guard !ids.isEmpty else { return }
        history.removeAll { ids.contains($0.id) }
        persistHistory()
    }

    func copyHistoryOriginal(_ item: TranslationHistory) {
        copyTextToPasteboard(item.original)
    }

    func copyHistoryResult(_ item: TranslationHistory) {
        copyTextToPasteboard(item.result)
    }

    private func copyTextToPasteboard(_ text: String) {
        NSPasteboard.general.clearContents()
        NSPasteboard.general.setString(text, forType: .string)
    }

    private func saveHistory(original: String, result: String, source: String, target: String) {
        guard historyRecordingEnabled else { return }
        let item = TranslationHistory(
            id: UUID(), original: original, result: result,
            source: source, target: target,
            date: Date(), engine: selectedEngine.shortName
        )
        let pinned = history.filter { $0.isPinned && $0.original != original }
        let unpinned = history.filter { !$0.isPinned && $0.original != original }
        let unpinnedKeep = Array(([item] + unpinned).prefix(30))
        history = pinned + unpinnedKeep
        persistHistory()
    }

    func togglePin(_ item: TranslationHistory) {
        guard let index = history.firstIndex(where: { $0.id == item.id }) else { return }
        history[index].isPinned.toggle()
        let pinned = history.filter { $0.isPinned }
        let unpinned = history.filter { !$0.isPinned }
        history = pinned + unpinned
        persistHistory()
    }

    func setHistoryRecording(_ enabled: Bool) {
        historyRecordingEnabled = enabled
        UserDefaults.standard.set(enabled, forKey: historyEnabledKey)
    }

    private func persistHistory() {
        if let data = try? JSONEncoder().encode(history) {
            UserDefaults.standard.set(data, forKey: historyKey)
        }
    }

    private func decodeEntities(_ value: String) -> String {
        value
            .replacingOccurrences(of: "&quot;", with: "\"")
            .replacingOccurrences(of: "&#39;", with: "'")
            .replacingOccurrences(of: "&apos;", with: "'")
            .replacingOccurrences(of: "&amp;", with: "&")
            .replacingOccurrences(of: "&lt;", with: "<")
            .replacingOccurrences(of: "&gt;", with: ">")
    }
}

private enum ImageRecognitionError: LocalizedError {
    case unreadableImage
    case noTextFound

    var errorDescription: String? {
        switch self {
        case .unreadableImage: return "无法读取这张图片，请换一张常见格式的图片重试"
        case .noTextFound: return "图片中没有识别到可翻译的文字"
        }
    }
}

private enum AppTranslationError: LocalizedError {
    case invalidRequest
    case serviceUnavailable
    case provider(String)

    var errorDescription: String? {
        switch self {
        case .invalidRequest: return "翻译请求无效"
        case .serviceUnavailable: return "翻译服务暂时不可用"
        case .provider(let message): return message
        }
    }
}

private final class SelectionTranslationPanel: NSPanel {
    override var canBecomeKey: Bool { true }
    override var canBecomeMain: Bool { false }
}

@MainActor
private final class SelectionTranslationPanelController: NSObject, NSWindowDelegate {
    private var panel: SelectionTranslationPanel?
    private weak var model: TranslatorViewModel?
    private var sourceProcessID: pid_t?
    private var sourceAppObserver: NSObjectProtocol?
    private var globalMouseMonitor: Any?
    private var localMouseMonitor: Any?
    private let defaultSize = NSSize(width: 390, height: 235)
    private let minimumSize = NSSize(width: 320, height: 190)
    private let frameAutosaveName = "Mac翻译.划词浮窗"
    private let hasSavedFrameKey = "yijian.popup.has-saved-frame"

    func show(model: TranslatorViewModel, anchor: CGPoint, sourceProcessID: pid_t?) {
        let panel = panel ?? makePanel(model: model, size: defaultSize)
        self.panel = panel
        self.model = model
        self.sourceProcessID = sourceProcessID
        startFollowingSourceApplication()
        startOutsideClickMonitoring()
        let targetScreen = screen(containing: anchor)
        updateSizeLimits(for: panel, on: targetScreen)
        let currentSize = clampedSize(panel.frame.size, for: targetScreen)
        if panel.frame.size != currentSize {
            panel.setContentSize(currentSize)
        }
        if !UserDefaults.standard.bool(forKey: hasSavedFrameKey) {
            panel.setFrameOrigin(panelOrigin(for: currentSize, anchor: anchor, screen: targetScreen))
        } else if let visibleFrame = targetScreen?.visibleFrame, !visibleFrame.intersects(panel.frame) {
            panel.setFrameOrigin(panelOrigin(for: currentSize, anchor: anchor, screen: targetScreen))
        }
        if sourceProcessID == nil || NSWorkspace.shared.frontmostApplication?.processIdentifier == sourceProcessID {
            panel.orderFrontRegardless()
        }
    }

    private func close() {
        sourceProcessID = nil
        panel?.orderOut(nil)
        if let sourceAppObserver {
            NSWorkspace.shared.notificationCenter.removeObserver(sourceAppObserver)
            self.sourceAppObserver = nil
        }
    }

    private func replaceSelection() {
        guard let sourceProcessID, let model else { return }
        model.replaceSelectedText(in: sourceProcessID)
        if !model.popupPinned { close() }
    }

    private func startFollowingSourceApplication() {
        if let sourceAppObserver {
            NSWorkspace.shared.notificationCenter.removeObserver(sourceAppObserver)
        }
        sourceAppObserver = NSWorkspace.shared.notificationCenter.addObserver(
            forName: NSWorkspace.didActivateApplicationNotification,
            object: nil,
            queue: .main
        ) { [weak self] notification in
            Task { @MainActor in
                guard let self,
                      let app = notification.userInfo?[NSWorkspace.applicationUserInfoKey] as? NSRunningApplication,
                      let sourceProcessID = self.sourceProcessID else { return }
                if app.processIdentifier == sourceProcessID || self.model?.popupPinned == true {
                    self.panel?.orderFrontRegardless()
                } else {
                    self.panel?.orderOut(nil)
                }
            }
        }
    }

    private func makePanel(model: TranslatorViewModel, size: NSSize) -> SelectionTranslationPanel {
        let panel = SelectionTranslationPanel(
            contentRect: NSRect(origin: .zero, size: size),
            styleMask: [.titled, .resizable, .fullSizeContentView, .nonactivatingPanel],
            backing: .buffered,
            defer: false
        )
        panel.level = .floating
        panel.titleVisibility = .hidden
        panel.titlebarAppearsTransparent = true
        panel.standardWindowButton(.closeButton)?.isHidden = true
        panel.standardWindowButton(.miniaturizeButton)?.isHidden = true
        panel.standardWindowButton(.zoomButton)?.isHidden = true
        panel.isMovableByWindowBackground = true
        panel.isOpaque = false
        panel.backgroundColor = .clear
        panel.hasShadow = true
        panel.hidesOnDeactivate = false
        panel.becomesKeyOnlyIfNeeded = true
        panel.contentMinSize = minimumSize
        panel.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary, .transient]
        panel.delegate = self
        panel.setFrameAutosaveName(frameAutosaveName)
        let hostingView = NSHostingView(
            rootView: SelectionTranslationPopup(
                model: model,
                close: { [weak self] in self?.close() },
                replace: { [weak self] in self?.replaceSelection() }
            )
            .frame(minWidth: minimumSize.width, minHeight: minimumSize.height)
        )
        hostingView.frame = NSRect(origin: .zero, size: size)
        hostingView.autoresizingMask = [.width, .height]
        hostingView.sizingOptions = []
        hostingView.wantsLayer = true
        hostingView.layer?.backgroundColor = NSColor.clear.cgColor
        panel.contentView = hostingView
        panel.setContentSize(size)
        return panel
    }

    private func startOutsideClickMonitoring() {
        if globalMouseMonitor == nil {
            globalMouseMonitor = NSEvent.addGlobalMonitorForEvents(matching: [.leftMouseDown, .rightMouseDown]) { [weak self] _ in
                Task { @MainActor in self?.hideForOutsideClickIfNeeded() }
            }
        }
        if localMouseMonitor == nil {
            localMouseMonitor = NSEvent.addLocalMonitorForEvents(matching: [.leftMouseDown, .rightMouseDown]) { [weak self] event in
                Task { @MainActor in self?.hideForOutsideClickIfNeeded() }
                return event
            }
        }
    }

    private func hideForOutsideClickIfNeeded() {
        guard let panel, panel.isVisible, model?.popupPinned != true else { return }
        if !panel.frame.contains(NSEvent.mouseLocation) { close() }
    }

    func windowDidMove(_ notification: Notification) {
        UserDefaults.standard.set(true, forKey: hasSavedFrameKey)
    }

    func windowDidResize(_ notification: Notification) {
        UserDefaults.standard.set(true, forKey: hasSavedFrameKey)
    }

    private func screen(containing anchor: CGPoint) -> NSScreen? {
        NSScreen.screens.first(where: { $0.frame.contains(anchor) })
            ?? NSScreen.main
            ?? NSScreen.screens.first
    }

    private func updateSizeLimits(for panel: NSPanel, on screen: NSScreen?) {
        guard let visibleFrame = screen?.visibleFrame else { return }
        panel.contentMinSize = minimumSize
        panel.contentMaxSize = NSSize(
            width: max(minimumSize.width, floor(visibleFrame.width * 0.5)),
            height: max(minimumSize.height, floor(visibleFrame.height * 0.5))
        )
    }

    private func clampedSize(_ size: NSSize, for screen: NSScreen?) -> NSSize {
        guard let visibleFrame = screen?.visibleFrame else { return size }
        return NSSize(
            width: min(max(size.width, minimumSize.width), floor(visibleFrame.width * 0.5)),
            height: min(max(size.height, minimumSize.height), floor(visibleFrame.height * 0.5))
        )
    }

    private func panelOrigin(for size: NSSize, anchor: CGPoint, screen: NSScreen?) -> NSPoint {
        guard let visibleFrame = screen?.visibleFrame else {
            return NSPoint(x: anchor.x + 12, y: anchor.y - size.height - 10)
        }

        var x = anchor.x + 12
        var y = anchor.y - size.height - 10
        if y < visibleFrame.minY + 8 { y = anchor.y + 22 }
        x = min(max(x, visibleFrame.minX + 8), visibleFrame.maxX - size.width - 8)
        y = min(max(y, visibleFrame.minY + 8), visibleFrame.maxY - size.height - 8)
        return NSPoint(x: x, y: y)
    }
}

private struct SelectionTranslationPopup: View {
    @ObservedObject var model: TranslatorViewModel
    let close: () -> Void
    let replace: () -> Void
    private let blue = MacVisualTokens.accent

    var body: some View {
        GeometryReader { geometry in
            let scale = min(max(geometry.size.width / 390, 0.88), 1.45)
            VStack(spacing: 0) {
            HStack(spacing: 9) {
                popupLanguageMenu(selection: model.popupSourceLanguage, excluding: model.popupTargetLanguage, action: model.setPopupSourceLanguage)
                Image(systemName: "arrow.right")
                    .font(.system(size: 10, weight: .bold))
                    .foregroundStyle(Color.secondary)
                popupLanguageMenu(selection: model.popupTargetLanguage, excluding: model.popupSourceLanguage, action: model.setPopupTargetLanguage)
                Spacer()
                Button {
                    model.popupPinned.toggle()
                } label: {
                    Image(systemName: model.popupPinned ? "pin.fill" : "pin")
                        .font(.system(size: 11, weight: .semibold))
                        .foregroundStyle(model.popupPinned ? blue : Color.secondary)
                        .frame(width: 27, height: 27)
                        .background((model.popupPinned ? blue : Color.secondary).opacity(0.10))
                        .clipShape(Circle())
                }
                .buttonStyle(.plain)
                .help(model.popupPinned ? "取消固定" : "固定浮窗")
                Button(action: replace) {
                    Image(systemName: "arrow.uturn.backward.square")
                        .font(.system(size: 11, weight: .semibold))
                        .foregroundStyle(blue)
                        .frame(width: 27, height: 27)
                        .background(blue.opacity(0.10))
                        .clipShape(Circle())
                }
                .buttonStyle(.plain)
                .disabled(model.popupTranslatedText.isEmpty)
                .help("用译文替换选中文字")
                Button(action: model.copyPopupResult) {
                    Image(systemName: model.copyLabel == "复制译文" ? "doc.on.doc" : "checkmark")
                        .font(.system(size: 11, weight: .semibold))
                        .foregroundStyle(blue)
                        .frame(width: 27, height: 27)
                        .background(blue.opacity(0.10))
                        .clipShape(Circle())
                }
                .buttonStyle(.plain)
                .disabled(model.popupTranslatedText.isEmpty)
                .help(model.copyLabel)
                Button(action: close) {
                    Image(systemName: "xmark")
                        .font(.system(size: 11, weight: .bold))
                        .frame(width: 25, height: 25)
                        .background(Color.black.opacity(0.045))
                        .clipShape(Circle())
                }
                .buttonStyle(.plain)
            }
            .padding(.horizontal, 16)
            .frame(height: 52)

            Divider().opacity(0.65)

            VStack(alignment: .leading, spacing: 11) {
                Text(model.popupSourceText)
                    .font(.system(size: min(15 * scale, 20)))
                    .foregroundStyle(Color.secondary)
                    .lineLimit(2)

                if model.popupIsLoading {
                    HStack(spacing: 10) {
                        ProgressView().controlSize(.small)
                        Text("正在翻译…").font(.system(size: 14, weight: .medium))
                    }
                    .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .center)
                } else if let notice = model.popupNotice {
                    VStack(alignment: .leading, spacing: 8) {
                        Label(notice.kind.title, systemImage: notice.kind.icon)
                            .font(.system(size: 13, weight: .semibold))
                            .foregroundStyle(notice.kind.color)
                        Text(notice.message)
                            .font(.system(size: 12))
                            .foregroundStyle(Color.secondary)
                            .fixedSize(horizontal: false, vertical: true)
                        if let action = notice.action {
                            Button(action.buttonTitle) { action.open() }
                                .buttonStyle(.bordered)
                                .controlSize(.small)
                        }
                    }
                    .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
                } else if !model.popupTranslatedText.isEmpty {
                    ScrollView {
                        Text(model.popupTranslatedText)
                            .font(.system(size: min(17 * scale, 24), weight: .medium))
                            .lineSpacing(5)
                            .textSelection(.enabled)
                            .frame(maxWidth: .infinity, alignment: .topLeading)
                    }
                }
            }
            .padding(.horizontal, 18)
            .padding(.vertical, 13)
            .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)

            }
            .background {
                AdaptiveGlassBackdrop(materialOpacity: 0.94, tintOpacity: 0.22)
                    .ignoresSafeArea()
            }
        }
        .ignoresSafeArea(.container, edges: .top)
        .frame(minWidth: 320, minHeight: 190)
        .overlay(alignment: .bottomTrailing) {
            Image(systemName: "arrow.up.left.and.arrow.down.right")
                .font(.system(size: 9, weight: .semibold))
                .foregroundStyle(Color.secondary.opacity(0.55))
                .padding(6)
                .allowsHitTesting(false)
        }
        .overlay(alignment: .topLeading) {
            if #available(macOS 15.0, *) {
                PopupAppleTranslationWorker(model: model)
            }
        }
    }

    private func popupLanguageMenu(selection: String, excluding: String, action: @escaping (String) -> Void) -> some View {
        Menu {
            ForEach(["en", "zh-CN", "ja", "ko"].filter { $0 != excluding }, id: \.self) { code in
                Button(languageName(code)) { action(code) }
            }
        } label: {
            HStack(spacing: 5) {
                Text(languageName(selection))
                Image(systemName: "chevron.down")
                    .font(.system(size: 8, weight: .semibold))
                    .foregroundStyle(MacVisualTokens.tertiaryLabel)
            }
            .font(.system(size: 13, weight: .medium))
            .foregroundStyle(MacVisualTokens.label)
            .macHoverControl(cornerRadius: 8, horizontalPadding: 8, height: 28)
        }
        .menuStyle(.borderlessButton)
        .menuIndicator(.hidden)
        .fixedSize()
    }
}

@available(macOS 15.0, *)
private struct PopupAppleTranslationWorker: View {
    @ObservedObject var model: TranslatorViewModel
    @State private var configuration: TranslationSession.Configuration?
    @State private var activeRequest: PopupAppleTranslationRequest?

    var body: some View {
        Color.clear
            .frame(width: 1, height: 1)
            .allowsHitTesting(false)
            .onChange(of: model.popupAppleTranslationRequest) { request in
                guard let request,
                      let sourceID = appleTranslationLocales[request.source],
                      let targetID = appleTranslationLocales[request.target] else { return }
                activeRequest = request
                var next = TranslationSession.Configuration(
                    source: Locale.Language(identifier: sourceID),
                    target: Locale.Language(identifier: targetID)
                )
                if configuration == next { next.invalidate() }
                configuration = next
            }
            .translationTask(configuration) { session in
                guard let request = activeRequest,
                      model.popupAppleTranslationRequest?.id == request.id else { return }
                await model.completePopupAppleTranslation(using: session, request: request)
            }
    }
}

private enum MacVisualTokens {
    static let accent = Color.accentColor
    static let label = Color(nsColor: .labelColor)
    static let secondaryLabel = Color(nsColor: .secondaryLabelColor)
    static let tertiaryLabel = Color(nsColor: .tertiaryLabelColor)
    static let separator = Color(nsColor: .separatorColor)
    static let controlFill = Color(nsColor: .controlBackgroundColor)
    static let panelRadius: CGFloat = 20
    static let controlRadius: CGFloat = 9
    static let floatingRadius: CGFloat = 12
}

/// A single, shared glass recipe used by the window backdrop, title bar and
/// translation card.  The material is rendered first, then the mode-specific
/// tint is placed above it so backdrop content cannot introduce a left/right
/// colour shift.
private struct UnifiedGlassLayer: View {
    @AppStorage("fanyi.glass.enabled") private var glassEnabled = true
    @Environment(\.colorScheme) private var colorScheme
    let tint: Color
    let materialOpacity: Double
    let isUltraThin: Bool
    var isRegular: Bool = false

    var body: some View {
        Group {
            if glassEnabled {
                ZStack {
                    if isRegular {
                        Rectangle()
                            .fill(.regularMaterial)
                            .opacity(materialOpacity)
                    } else if isUltraThin {
                        Rectangle()
                            .fill(.ultraThinMaterial)
                            .opacity(materialOpacity)
                    } else {
                        Rectangle()
                            .fill(.thinMaterial)
                            .opacity(materialOpacity)
                    }
                    Rectangle()
                        .fill(tint)
                }
            } else {
                Rectangle()
                    .fill(colorScheme == .dark
                        ? Color(red: 0.055, green: 0.065, blue: 0.085)
                        : Color(red: 0.976, green: 0.978, blue: 0.995))
            }
        }
    }
}

private struct AdaptiveGlassBackdrop: View {
    @AppStorage("fanyi.glass.enabled") private var glassEnabled = true
    @Environment(\.colorScheme) private var colorScheme
    var materialOpacity = 0.88
    var tintOpacity = 0.26
    var regular = true

    var body: some View {
        Group {
            if glassEnabled {
                UnifiedGlassLayer(
                    tint: colorScheme == .dark
                        ? Color.black.opacity(tintOpacity)
                        : Color.white.opacity(tintOpacity),
                    materialOpacity: materialOpacity,
                    isUltraThin: !regular,
                    isRegular: regular
                )
            } else {
                Rectangle()
                    .fill(colorScheme == .dark
                        ? Color(red: 0.10, green: 0.115, blue: 0.145)
                        : Color.white)
            }
        }
    }
}

private enum GlassSurfaceLevel: Equatable {
    case card
    case editor
}

private struct GlassSurfaceModifier: ViewModifier {
    @AppStorage("fanyi.glass.enabled") private var glassEnabled = true
    @Environment(\.colorScheme) private var colorScheme
    let cornerRadius: CGFloat
    let level: GlassSurfaceLevel

    private var tintOpacity: Double {
        switch level {
        case .card: return 0.09
        case .editor: return 0.18
        }
    }

    func body(content: Content) -> some View {
        let shape = RoundedRectangle(cornerRadius: cornerRadius, style: .continuous)
        content
            .background {
                Group {
                    if glassEnabled {
                        ZStack {
                            if level == .editor {
                                shape.fill(.regularMaterial).opacity(0.94)
                            } else {
                                shape.fill(.thinMaterial).opacity(0.86)
                            }
                            shape.fill(colorScheme == .dark
                                ? Color.black.opacity(tintOpacity)
                                : Color.white.opacity(tintOpacity))
                            shape.stroke(
                                LinearGradient(
                                    colors: [Color.white.opacity(colorScheme == .dark ? 0.30 : 0.82),
                                             MacVisualTokens.separator.opacity(0.42)],
                                    startPoint: .top,
                                    endPoint: .bottom
                                ),
                                lineWidth: 0.8
                            )
                        }
                    } else {
                        ZStack {
                            shape.fill(colorScheme == .dark
                                ? Color(red: 0.10, green: 0.115, blue: 0.145)
                                : Color.white)
                            shape.stroke(MacVisualTokens.separator.opacity(0.72), lineWidth: 0.8)
                        }
                    }
                }
            }
            .clipShape(shape)
            .shadow(
                color: glassEnabled
                    ? Color.black.opacity(level == .editor ? (colorScheme == .dark ? 0.22 : 0.10) : 0.07)
                    : Color.black.opacity(0.04),
                radius: glassEnabled ? (level == .editor ? 14 : 8) : 3,
                y: glassEnabled ? (level == .editor ? 6 : 3) : 1
            )
    }
}

private struct HoverMaterialModifier: ViewModifier {
    @State private var isHovering = false
    let cornerRadius: CGFloat
    let horizontalPadding: CGFloat
    let height: CGFloat

    func body(content: Content) -> some View {
        content
            .padding(.horizontal, horizontalPadding)
            .frame(height: height)
            .background {
                RoundedRectangle(cornerRadius: cornerRadius, style: .continuous)
                    .fill(isHovering ? AnyShapeStyle(.thinMaterial) : AnyShapeStyle(Color.clear))
            }
            .contentShape(RoundedRectangle(cornerRadius: cornerRadius, style: .continuous))
            .onHover { hovering in
                withAnimation(.easeOut(duration: 0.14)) { isHovering = hovering }
            }
    }
}

private extension View {
    func macHoverControl(
        cornerRadius: CGFloat = MacVisualTokens.controlRadius,
        horizontalPadding: CGFloat = 9,
        height: CGFloat = 32
    ) -> some View {
        modifier(HoverMaterialModifier(
            cornerRadius: cornerRadius,
            horizontalPadding: horizontalPadding,
            height: height
        ))
    }

    func macGlassBorder(cornerRadius: CGFloat) -> some View {
        overlay {
            RoundedRectangle(cornerRadius: cornerRadius, style: .continuous)
                .stroke(MacVisualTokens.separator.opacity(0.72), lineWidth: 0.75)
        }
    }

    func glassSurface(cornerRadius: CGFloat, level: GlassSurfaceLevel = .card) -> some View {
        modifier(GlassSurfaceModifier(cornerRadius: cornerRadius, level: level))
    }
}

private struct FloatingGlassButtonStyle: ButtonStyle {
    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .glassSurface(cornerRadius: MacVisualTokens.floatingRadius)
            .shadow(color: Color.black.opacity(0.09), radius: 7, y: 3)
            .scaleEffect(configuration.isPressed ? 0.98 : 1)
            .opacity(configuration.isPressed ? 0.84 : 1)
            .animation(.easeOut(duration: 0.10), value: configuration.isPressed)
    }
}

private struct SubmitTextEditor: NSViewRepresentable {
    @Binding var text: String
    let isDarkMode: Bool
    let onSubmit: () -> Void
    let onImageDrop: (URL) -> Void

    private var editorTextColor: NSColor { .labelColor }

    func makeCoordinator() -> Coordinator {
        Coordinator(parent: self)
    }

    func makeNSView(context: Context) -> NSScrollView {
        let scrollView = NSScrollView()
        scrollView.borderType = .noBorder
        scrollView.drawsBackground = false
        scrollView.backgroundColor = .clear
        scrollView.contentView.backgroundColor = .clear
        scrollView.hasVerticalScroller = true
        scrollView.autohidesScrollers = true

        let textView = ImageDropTextView()
        textView.onImageDrop = onImageDrop
        textView.registerForDraggedTypes([.fileURL])
        textView.delegate = context.coordinator
        textView.isRichText = false
        textView.importsGraphics = false
        textView.drawsBackground = false
        textView.backgroundColor = .clear
        textView.allowsUndo = true
        textView.isVerticallyResizable = true
        textView.isHorizontallyResizable = false
        textView.autoresizingMask = [.width]
        textView.textContainer?.widthTracksTextView = true
        textView.textContainerInset = NSSize(width: 24, height: 20)
        textView.font = NSFont.systemFont(ofSize: 15, weight: .regular)
        textView.textColor = editorTextColor
        textView.insertionPointColor = .controlAccentColor
        let paragraph = NSMutableParagraphStyle()
        paragraph.lineSpacing = 5
        textView.defaultParagraphStyle = paragraph
        textView.string = text
        scrollView.documentView = textView
        return scrollView
    }

    func updateNSView(_ scrollView: NSScrollView, context: Context) {
        context.coordinator.parent = self
        guard let textView = scrollView.documentView as? ImageDropTextView else { return }
        textView.onImageDrop = onImageDrop
        textView.textColor = editorTextColor
        if textView.string != text {
            textView.string = text
        }
    }

    final class Coordinator: NSObject, NSTextViewDelegate {
        var parent: SubmitTextEditor

        init(parent: SubmitTextEditor) {
            self.parent = parent
        }

        func textDidChange(_ notification: Notification) {
            guard let textView = notification.object as? NSTextView else { return }
            let limited = String(textView.string.prefix(maxSourceCharacters))
            if textView.string != limited {
                textView.string = limited
            }
            parent.text = limited
        }

        func textView(_ textView: NSTextView, doCommandBy commandSelector: Selector) -> Bool {
            if commandSelector == #selector(NSResponder.insertNewline(_:)) {
                if NSApp.currentEvent?.modifierFlags.contains(.shift) == true {
                    return false
                }
                parent.onSubmit()
                return true
            }
            return false
        }
    }
}

private final class ImageDropTextView: NSTextView {
    var onImageDrop: ((URL) -> Void)?

    override func draggingEntered(_ sender: NSDraggingInfo) -> NSDragOperation {
        imageURL(from: sender.draggingPasteboard) == nil ? super.draggingEntered(sender) : .copy
    }

    override func draggingUpdated(_ sender: NSDraggingInfo) -> NSDragOperation {
        imageURL(from: sender.draggingPasteboard) == nil ? super.draggingUpdated(sender) : .copy
    }

    override func performDragOperation(_ sender: NSDraggingInfo) -> Bool {
        guard let url = imageURL(from: sender.draggingPasteboard) else {
            return super.performDragOperation(sender)
        }
        onImageDrop?(url)
        return true
    }

    private func imageURL(from pasteboard: NSPasteboard) -> URL? {
        let options: [NSPasteboard.ReadingOptionKey: Any] = [.urlReadingFileURLsOnly: true]
        guard let fileURL = (pasteboard.readObjects(forClasses: [NSURL.self], options: options) as? [NSURL])?.first as URL? else {
            return nil
        }
        guard let type = UTType(filenameExtension: fileURL.pathExtension), type.conforms(to: .image) else {
            return nil
        }
        return fileURL
    }
}

private struct LanguageMenu: View {
    let color: Color
    let textColor: Color
    let selection: String
    var options: [String] = concreteLanguages
    let onChange: (String) -> Void

    var body: some View {
        Menu {
            ForEach(options, id: \.self) { code in
                Button(languageName(code)) { onChange(code) }
            }
        } label: {
            HStack(spacing: 7) {
                Image(systemName: "globe")
                    .font(.system(size: 13, weight: .medium))
                    .foregroundStyle(color)
                Text(languageName(selection))
                    .font(.system(size: 14, weight: .medium))
                    .foregroundStyle(textColor)
                Image(systemName: "chevron.down")
                    .font(.system(size: 8, weight: .semibold))
                    .foregroundStyle(MacVisualTokens.tertiaryLabel)
            }
            .macHoverControl(horizontalPadding: 10, height: 34)
        }
        .menuStyle(.borderlessButton)
        .menuIndicator(.hidden)
        .fixedSize()
    }
}

@available(macOS 15.0, *)
private struct AppleTranslationWorker: View {
    @ObservedObject var model: TranslatorViewModel
    @State private var configuration: TranslationSession.Configuration?
    @State private var activeTextRequest: AppleTranslationRequest?
    @State private var activeImageRequest: AppleImageTranslationRequest?

    var body: some View {
        Color.clear
            .frame(width: 1, height: 1)
            .onChange(of: model.appleTranslationRequest) { request in
                guard let request,
                      let sourceID = appleTranslationLocales[request.source],
                      let targetID = appleTranslationLocales[request.target] else { return }
                activeTextRequest = request
                activeImageRequest = nil
                var next = TranslationSession.Configuration(
                    source: Locale.Language(identifier: sourceID),
                    target: Locale.Language(identifier: targetID)
                )
                if configuration == next { next.invalidate() }
                configuration = next
            }
            .onChange(of: model.appleImageTranslationRequest) { request in
                guard let request,
                      let sourceID = appleTranslationLocales[request.source],
                      let targetID = appleTranslationLocales[request.target] else { return }
                activeImageRequest = request
                activeTextRequest = nil
                var next = TranslationSession.Configuration(
                    source: Locale.Language(identifier: sourceID),
                    target: Locale.Language(identifier: targetID)
                )
                if configuration == next { next.invalidate() }
                configuration = next
            }
            .translationTask(configuration) { session in
                if let request = activeImageRequest,
                   model.appleImageTranslationRequest?.id == request.id {
                    await model.completeAppleImageTranslation(using: session, request: request)
                } else if let request = activeTextRequest,
                          model.appleTranslationRequest?.id == request.id {
                    await model.completeAppleTranslation(using: session, request: request)
                }
            }
    }
}

private struct HistorySheet: View {
    private enum HistoryKind: String, CaseIterable, Identifiable {
        case text
        case image
        var id: String { rawValue }
        var title: String { self == .text ? "文字" : "图片" }
    }

    @ObservedObject var model: TranslatorViewModel
    @Binding var isPresented: Bool
    @State private var isSelecting = false
    @State private var selectedIDs: Set<UUID> = []
    @State private var searchText = ""
    @State private var historyKind: HistoryKind = .text
    private let purple = MacVisualTokens.accent
    private let line = MacVisualTokens.separator.opacity(0.68)

    private var filteredHistory: [TranslationHistory] {
        let query = searchText.trimmingCharacters(in: .whitespacesAndNewlines).lowercased()
        guard !query.isEmpty else { return model.history }
        return model.history.filter {
            $0.original.lowercased().contains(query) || $0.result.lowercased().contains(query)
        }
    }

    var body: some View {
        VStack(spacing: 0) {
            HStack {
                Label("历史记录", systemImage: "clock.arrow.circlepath")
                    .font(.system(size: 19, weight: .semibold))
                Spacer()
                if historyKind == .text && !model.history.isEmpty {
                    if isSelecting {
                        Button(selectedIDs.count == filteredHistory.count ? "取消全选" : "全选") {
                            selectedIDs = selectedIDs.count == filteredHistory.count ? [] : Set(filteredHistory.map(\.id))
                        }
                        .buttonStyle(.borderless)
                        Button("删除所选（\(selectedIDs.count)）") {
                            model.deleteHistory(ids: selectedIDs)
                            selectedIDs = []
                            isSelecting = false
                        }
                        .buttonStyle(.borderless)
                        .foregroundStyle(Color.red)
                        .disabled(selectedIDs.isEmpty)
                    } else {
                        Button("多选") { isSelecting = true }
                            .buttonStyle(.borderless)
                        Button("清空全部", action: model.clearHistory)
                            .buttonStyle(.borderless)
                            .foregroundStyle(Color.red.opacity(0.8))
                    }
                } else if historyKind == .image && !model.imageHistory.isEmpty {
                    Button("清空图片历史", action: model.clearImageHistory)
                        .buttonStyle(.borderless)
                        .foregroundStyle(Color.red.opacity(0.8))
                }
                Button(isSelecting ? "取消" : "完成") {
                    if isSelecting {
                        isSelecting = false
                        selectedIDs = []
                    } else {
                        isPresented = false
                    }
                }
                    .buttonStyle(.borderedProminent)
                    .tint(purple)
            }
            .padding(.horizontal, 22)
            .frame(height: 62)
            .background {
                AdaptiveGlassBackdrop(materialOpacity: 0.78, tintOpacity: 0.14, regular: false)
            }

            Picker("历史类型", selection: $historyKind) {
                ForEach(HistoryKind.allCases) { kind in
                    Text(kind.title).tag(kind)
                }
            }
            .pickerStyle(.segmented)
            .labelsHidden()
            .frame(width: 220)
            .padding(.bottom, 12)
            .onChange(of: historyKind) { _ in
                isSelecting = false
                selectedIDs = []
            }

            if historyKind == .text && !model.history.isEmpty {
                HStack(spacing: 8) {
                    Image(systemName: "magnifyingglass")
                        .foregroundStyle(Color.secondary)
                    TextField("搜索原文或译文…", text: $searchText)
                        .textFieldStyle(.plain)
                    if !searchText.isEmpty {
                        Button {
                            searchText = ""
                        } label: {
                            Image(systemName: "xmark.circle.fill")
                                .foregroundStyle(Color.secondary)
                        }
                        .buttonStyle(.plain)
                    }
                }
                .font(.system(size: 13))
                .padding(.horizontal, 12)
                .frame(height: 34)
                .glassSurface(cornerRadius: 10)
                .padding(.horizontal, 22)
                .padding(.bottom, 10)
            }

            Divider()

            if historyKind == .image {
                imageHistoryContent
            } else if model.history.isEmpty {
                VStack(spacing: 12) {
                    Image(systemName: "text.badge.plus")
                        .font(.system(size: 30))
                        .foregroundStyle(purple.opacity(0.65))
                    Text("还没有翻译记录")
                        .font(.system(size: 15, weight: .semibold))
                    Text(model.historyRecordingEnabled ? "完成一次翻译后，记录会自动保存在本机。" : "历史记录已关闭，可在设置菜单中重新开启。")
                        .font(.system(size: 12))
                        .foregroundStyle(Color.secondary)
                }
                .frame(maxWidth: .infinity, maxHeight: .infinity)
            } else if filteredHistory.isEmpty {
                VStack(spacing: 10) {
                    Image(systemName: "magnifyingglass")
                        .font(.system(size: 26))
                        .foregroundStyle(purple.opacity(0.6))
                    Text("没有匹配「\(searchText)」的记录")
                        .font(.system(size: 13))
                        .foregroundStyle(Color.secondary)
                }
                .frame(maxWidth: .infinity, maxHeight: .infinity)
            } else {
                ScrollView {
                    LazyVStack(spacing: 10) {
                        ForEach(filteredHistory) { item in
                            HStack(alignment: .top, spacing: 12) {
                                if isSelecting {
                                    Button {
                                        if selectedIDs.contains(item.id) { selectedIDs.remove(item.id) }
                                        else { selectedIDs.insert(item.id) }
                                    } label: {
                                        Image(systemName: selectedIDs.contains(item.id) ? "checkmark.circle.fill" : "circle")
                                            .font(.system(size: 19))
                                            .foregroundStyle(selectedIDs.contains(item.id) ? purple : Color.secondary)
                                    }
                                    .buttonStyle(.plain)
                                    .padding(.top, 2)
                                }

                                VStack(alignment: .leading, spacing: 9) {
                                    HStack(spacing: 8) {
                                        Text("\(languageName(item.source)) → \(languageName(item.target))")
                                            .font(.system(size: 11, weight: .medium))
                                            .foregroundStyle(purple)
                                        if !item.engine.isEmpty {
                                            Text(item.engine)
                                                .font(.system(size: 10, weight: .medium))
                                                .foregroundStyle(Color.secondary)
                                                .padding(.horizontal, 6)
                                                .padding(.vertical, 2)
                                                .background(Color.secondary.opacity(0.12))
                                                .clipShape(Capsule())
                                        }
                                        Spacer()
                                        Text(historyTimeText(item.date))
                                            .font(.system(size: 10))
                                            .foregroundStyle(Color.secondary)
                                        Button {
                                            model.togglePin(item)
                                        } label: {
                                            Image(systemName: item.isPinned ? "pin.fill" : "pin")
                                                .font(.system(size: 11))
                                                .foregroundStyle(item.isPinned ? purple : Color.secondary)
                                        }
                                        .buttonStyle(.plain)
                                        .help(item.isPinned ? "取消置顶" : "置顶")
                                    }
                                    Text(item.original)
                                        .font(.system(size: 14, weight: .semibold))
                                        .foregroundStyle(Color.primary)
                                        .lineLimit(2)
                                    Text(item.result)
                                        .font(.system(size: 13))
                                        .foregroundStyle(Color.secondary)
                                        .lineLimit(2)
                                    HStack(spacing: 16) {
                                        Button { model.copyHistoryOriginal(item) } label: {
                                            Label("复制原文", systemImage: "doc.on.doc")
                                        }
                                        Button { model.copyHistoryResult(item) } label: {
                                            Label("复制译文", systemImage: "doc.on.doc.fill")
                                        }
                                        Spacer()
                                        if !isSelecting {
                                            Button("在主界面打开") {
                                                model.use(item)
                                                isPresented = false
                                            }
                                        }
                                        Button(role: .destructive) {
                                            model.deleteHistory(ids: [item.id])
                                            selectedIDs.remove(item.id)
                                        } label: {
                                            Image(systemName: "trash")
                                        }
                                    }
                                    .font(.system(size: 11, weight: .medium))
                                    .buttonStyle(.borderless)
                                }
                                .frame(maxWidth: .infinity, alignment: .leading)
                            }
                            .padding(16)
                            .contentShape(Rectangle())
                            .onTapGesture {
                                guard isSelecting else { return }
                                if selectedIDs.contains(item.id) { selectedIDs.remove(item.id) }
                                else { selectedIDs.insert(item.id) }
                            }
                            .glassSurface(cornerRadius: 13)
                            .overlay(RoundedRectangle(cornerRadius: 13).stroke(item.isPinned ? purple.opacity(0.45) : line))
                        }
                    }
                    .padding(20)
                }
                .background(Color.clear)
            }
        }
        .frame(width: 680, height: 560)
        .background {
            AdaptiveGlassBackdrop(materialOpacity: 0.92, tintOpacity: 0.24)
                .ignoresSafeArea()
        }
    }

    @ViewBuilder
    private var imageHistoryContent: some View {
        if model.imageHistory.isEmpty {
            VStack(spacing: 12) {
                Image(systemName: "photo.stack")
                    .font(.system(size: 30))
                    .foregroundStyle(purple.opacity(0.65))
                Text("还没有图片翻译记录")
                    .font(.system(size: 15, weight: .semibold))
                Text(model.imageHistoryRecordingEnabled
                    ? "完成图片翻译后，最近 10 张会保存在本机。"
                    : "图片历史已关闭，可在设置中重新开启。")
                    .font(.system(size: 12))
                    .foregroundStyle(Color.secondary)
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity)
        } else {
            ScrollView {
                LazyVStack(spacing: 12) {
                    ForEach(model.imageHistory) { item in
                        VStack(alignment: .leading, spacing: 10) {
                            HStack(spacing: 12) {
                                historyThumbnail(model.imageHistoryImage(item, translated: false), label: "原图")
                                Image(systemName: "arrow.right")
                                    .foregroundStyle(Color.secondary)
                                historyThumbnail(model.imageHistoryImage(item, translated: true), label: "译图")
                            }
                            HStack {
                                Label("\(languageName(item.source)) → \(languageName(item.target))", systemImage: "globe")
                                    .foregroundStyle(purple)
                                Text("· \(item.engine) · \(item.style.title)")
                                    .foregroundStyle(Color.secondary)
                                Spacer()
                                Text(historyTimeText(item.date))
                                    .foregroundStyle(Color.secondary)
                            }
                            .font(.system(size: 11, weight: .medium))
                            HStack {
                                Text(item.translatedText)
                                    .font(.system(size: 12))
                                    .foregroundStyle(Color.secondary)
                                    .lineLimit(1)
                                Spacer()
                                Button("在主界面打开") {
                                    model.use(item)
                                    isPresented = false
                                }
                                Button(role: .destructive) {
                                    model.deleteImageHistory(ids: [item.id])
                                } label: {
                                    Image(systemName: "trash")
                                }
                            }
                            .buttonStyle(.borderless)
                        }
                        .padding(14)
                        .glassSurface(cornerRadius: 13)
                        .overlay(RoundedRectangle(cornerRadius: 13).stroke(line))
                    }
                }
                .padding(20)
            }
            .background(Color.clear)
        }
    }

    private func historyThumbnail(_ image: NSImage?, label: String) -> some View {
        ZStack(alignment: .bottomLeading) {
            if let image {
                Image(nsImage: image)
                    .resizable()
                    .interpolation(.medium)
                    .scaledToFit()
            } else {
                Color.secondary.opacity(0.10)
                Image(systemName: "photo")
                    .foregroundStyle(Color.secondary)
            }
            Text(label)
                .font(.system(size: 9, weight: .bold))
                .padding(.horizontal, 6).padding(.vertical, 3)
                .background(.black.opacity(0.58))
                .foregroundStyle(.white)
        }
        .frame(maxWidth: .infinity)
        .frame(height: 112)
        .background(Color(nsColor: .textBackgroundColor))
        .clipShape(RoundedRectangle(cornerRadius: 8))
    }
}

private struct DeepLSettingsSheet: View {
    @ObservedObject var model: TranslatorViewModel
    @Binding var isPresented: Bool
    @State private var apiKey: String
    private let purple = MacVisualTokens.accent

    init(model: TranslatorViewModel, isPresented: Binding<Bool>) {
        self.model = model
        self._isPresented = isPresented
        self._apiKey = State(initialValue: SecureKeyStore.loadDeepLKey())
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 18) {
            HStack {
                VStack(alignment: .leading, spacing: 5) {
                    Text("设置 DeepL API Free")
                        .font(.system(size: 19, weight: .semibold))
                    Text("密钥只保存在这台电脑的系统钥匙串中。")
                        .font(.system(size: 12))
                        .foregroundStyle(Color.secondary)
                }
                Spacer()
                Button("取消") { isPresented = false }
                    .buttonStyle(.borderless)
            }

            SecureField("粘贴 DeepL API Key", text: $apiKey)
                .textFieldStyle(.roundedBorder)
                .font(.system(size: 14))

            if let usage = model.deepLUsage {
                VStack(alignment: .leading, spacing: 8) {
                    HStack {
                        Text("本月用量")
                            .font(.system(size: 12, weight: .semibold))
                        Spacer()
                        Text("\(usage.characterCount) / \(usage.characterLimit) 字符")
                            .font(.system(size: 11))
                            .foregroundStyle(Color.secondary)
                    }
                    ProgressView(value: Double(usage.characterCount), total: Double(max(usage.characterLimit, 1)))
                        .tint(usage.characterCount > usage.characterLimit * 9 / 10 ? Color.red : purple)
                    if usage.characterCount > usage.characterLimit * 9 / 10 {
                        Text("免费额度即将用完（已用 \(usage.usedPercentText)%），请留意")
                            .font(.system(size: 11))
                            .foregroundStyle(Color.red)
                    } else {
                        Text("已使用 \(usage.usedPercentText)%，免费额度每月 50 万字符")
                            .font(.system(size: 11))
                            .foregroundStyle(Color.secondary)
                    }
                }
                .padding(12)
                .glassSurface(cornerRadius: 10)
            } else if model.hasDeepLKey {
                HStack(spacing: 8) {
                    ProgressView().controlSize(.small)
                    Text("正在获取用量…")
                        .font(.system(size: 11))
                        .foregroundStyle(Color.secondary)
                }
            }

            Text("使用 DeepL API Free 账号中的密钥；免费版每月有 50 万字符额度。")
                .font(.system(size: 12))
                .foregroundStyle(Color.secondary)

            HStack {
                if model.hasDeepLKey {
                    Button("移除密钥") {
                        if model.saveDeepLKey("") {
                            model.setEngine(.apple)
                            isPresented = false
                        }
                    }
                    .foregroundStyle(Color.red.opacity(0.8))
                    .buttonStyle(.borderless)
                }
                Spacer()
                Button("保存并使用 DeepL") {
                    if model.saveDeepLKey(apiKey) {
                        isPresented = false
                    }
                }
                .buttonStyle(.borderedProminent)
                .tint(purple)
                .disabled(apiKey.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
            }
        }
        .padding(24)
        .frame(width: 500)
        .background {
            AdaptiveGlassBackdrop(materialOpacity: 0.92, tintOpacity: 0.24)
                .ignoresSafeArea()
        }
        .onAppear {
            if model.hasDeepLKey {
                Task { await model.fetchDeepLUsage() }
            }
        }
    }
}

private struct ImagePreviewItem: Identifiable {
    let id = UUID()
    let title: String
    let image: NSImage
}

private struct TranslationImagePane: View {
    let image: NSImage
    let title: String
    let accent: Color
    let onOpen: () -> Void

    var body: some View {
        Button(action: onOpen) {
            ZStack {
                Color(nsColor: .textBackgroundColor)
                Image(nsImage: image)
                    .resizable()
                    .interpolation(.high)
                    .scaledToFit()
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
                VStack {
                    HStack {
                        Text(title)
                            .font(.system(size: 11, weight: .semibold))
                            .padding(.horizontal, 10)
                            .padding(.vertical, 6)
                            .foregroundStyle(.white)
                            .background(.black.opacity(0.62))
                            .clipShape(Capsule())
                        Spacer()
                        Label("点击放大", systemImage: "arrow.up.left.and.arrow.down.right")
                            .font(.system(size: 10, weight: .semibold))
                            .padding(.horizontal, 10)
                            .padding(.vertical, 6)
                            .foregroundStyle(.white)
                            .background(accent.opacity(0.90))
                            .clipShape(Capsule())
                    }
                    Spacer()
                }
                .padding(12)
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity)
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .help("点击放大查看\(title)")
    }
}

private struct ScrollWheelZoomCapture: NSViewRepresentable {
    @Binding var zoom: Double

    func makeNSView(context: Context) -> ScrollWheelZoomCaptureView {
        let view = ScrollWheelZoomCaptureView()
        view.onScroll = adjustZoom
        return view
    }

    func updateNSView(_ nsView: ScrollWheelZoomCaptureView, context: Context) {
        nsView.onScroll = adjustZoom
    }

    private func adjustZoom(
        deltaY: CGFloat,
        isDirectionInvertedFromDevice: Bool
    ) {
        let deviceDeltaY = isDirectionInvertedFromDevice ? -Double(deltaY) : Double(deltaY)
        zoom = ImageZoomPolicy.adjustedZoom(
            current: zoom,
            deviceDeltaY: deviceDeltaY
        )
    }
}

private final class ScrollWheelZoomCaptureView: NSView {
    var onScroll: ((CGFloat, Bool) -> Void)?
    private var eventMonitor: Any?

    override func viewDidMoveToWindow() {
        super.viewDidMoveToWindow()
        stopMonitoring()
        guard window != nil else { return }

        eventMonitor = NSEvent.addLocalMonitorForEvents(matching: .scrollWheel) { [weak self] event in
            guard let self,
                  event.window === self.window,
                  self.bounds.contains(self.convert(event.locationInWindow, from: nil)) else {
                return event
            }

            // A trackpad reports precise deltas. Let the ScrollView keep these events so
            // two-finger gestures pan the enlarged image horizontally and vertically.
            guard ImageZoomPolicy.shouldZoom(
                hasPreciseScrollingDeltas: event.hasPreciseScrollingDeltas
            ) else { return event }

            // Trackpad momentum should not keep changing the zoom after the gesture ends.
            guard event.momentumPhase.isEmpty else { return nil }
            self.onScroll?(
                event.scrollingDeltaY,
                event.isDirectionInvertedFromDevice
            )
            return nil
        }
    }

    override func hitTest(_ point: NSPoint) -> NSView? {
        nil
    }

    deinit {
        stopMonitoring()
    }

    private func stopMonitoring() {
        guard let eventMonitor else { return }
        NSEvent.removeMonitor(eventMonitor)
        self.eventMonitor = nil
    }
}

private struct ImagePreviewSheet: View {
    @Environment(\.dismiss) private var dismiss
    let item: ImagePreviewItem
    @State private var zoom: Double = 1

    var body: some View {
        VStack(spacing: 0) {
            HStack(spacing: 14) {
                VStack(alignment: .leading, spacing: 2) {
                    Text(item.title)
                        .font(.system(size: 18, weight: .bold))
                    Text("鼠标滚轮缩放 · 触控板双指移动")
                        .font(.system(size: 10))
                        .foregroundStyle(Color.secondary)
                }
                Spacer()
                Image(systemName: "minus.magnifyingglass")
                    .foregroundStyle(Color.secondary)
                Slider(value: $zoom, in: 1...3, step: 0.1)
                    .frame(width: 180)
                Image(systemName: "plus.magnifyingglass")
                    .foregroundStyle(Color.secondary)
                Text("\(Int(zoom * 100))%")
                    .font(.system(size: 11, design: .monospaced))
                    .foregroundStyle(Color.secondary)
                    .frame(width: 44, alignment: .trailing)
                Button("完成") { dismiss() }
                    .keyboardShortcut(.cancelAction)
            }
            .padding(.horizontal, 20)
            .frame(height: 58)

            Divider()

            GeometryReader { proxy in
                ScrollView([.horizontal, .vertical]) {
                    Image(nsImage: item.image)
                        .resizable()
                        .interpolation(.high)
                        .scaledToFit()
                        .frame(
                            width: max(proxy.size.width, proxy.size.width * zoom),
                            height: max(proxy.size.height, proxy.size.height * zoom)
                        )
                }
                .background(Color(nsColor: .textBackgroundColor))
                .background(ScrollWheelZoomCapture(zoom: $zoom))
            }
        }
        .frame(minWidth: 760, idealWidth: 980, minHeight: 560, idealHeight: 720)
    }
}

private struct ImageOCRReviewSheet: View {
    @ObservedObject var model: TranslatorViewModel
    let source: NSImage
    let originalBlocks: [RecognizedImageBlock]
    @State private var blocks: [RecognizedImageBlock]
    @State private var selectedID: UUID?
    @FocusState private var focusedID: UUID?

    init(model: TranslatorViewModel, source: NSImage, blocks: [RecognizedImageBlock]) {
        self.model = model
        self.source = source
        self.originalBlocks = blocks
        _blocks = State(initialValue: blocks)
        _selectedID = State(initialValue: blocks.first?.id)
    }

    private var selectedIndex: Int? {
        guard let selectedID else { return nil }
        return blocks.firstIndex { $0.id == selectedID }
    }

    private func textBinding(for id: UUID) -> Binding<String> {
        Binding(
            get: { blocks.first(where: { $0.id == id })?.text ?? "" },
            set: { value in
                guard let index = blocks.firstIndex(where: { $0.id == id }) else { return }
                blocks[index].text = value
            }
        )
    }

    private func deleteSelectedBlock() {
        guard let index = selectedIndex else { return }
        focusedID = nil
        blocks.remove(at: index)
        selectedID = blocks.isEmpty ? nil : blocks[min(index, blocks.count - 1)].id
    }

    private func inlineFontSize(for rect: CGRect) -> CGFloat {
        min(20, max(11, rect.height * 0.42))
    }

    var body: some View {
        VStack(spacing: 0) {
            HStack {
                VStack(alignment: .leading, spacing: 4) {
                    Text(model.isInitialImageOCRReview ? "检查图片识别文字" : "编辑原图文字")
                        .font(.system(size: 20, weight: .bold))
                    Text(model.isInitialImageOCRReview
                         ? "点击图片中的蓝色文字区域，逐处检查、修改或删除后再翻译。"
                         : "点击原图中的文字区域，即可在原位置直接删字、改字或删除整个区域。")
                        .font(.system(size: 12))
                        .foregroundStyle(Color.secondary)
                }
                Spacer()
                Button("恢复识别结果") {
                    blocks = originalBlocks
                    selectedID = originalBlocks.first?.id
                    focusedID = nil
                }
                .disabled(blocks == originalBlocks)
                Button(model.isInitialImageOCRReview ? "取消导入" : "取消") {
                    model.cancelImageOCREditing()
                }
                .keyboardShortcut(.cancelAction)
                Button("确认并翻译") {
                    model.confirmImageOCRBlocks(blocks)
                }
                .buttonStyle(.borderedProminent)
                .keyboardShortcut(.defaultAction)
                .disabled(blocks.allSatisfy { $0.text.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty })
            }
            .padding(.horizontal, 20)
            .frame(height: 72)

            Divider()

            GeometryReader { proxy in
                let fit = aspectFitRect(imageSize: source.size, in: proxy.size)
                ZStack(alignment: .topLeading) {
                    Color(nsColor: .textBackgroundColor)
                    Image(nsImage: source)
                        .resizable()
                        .interpolation(.high)
                        .frame(width: fit.width, height: fit.height)
                        .position(x: fit.midX, y: fit.midY)

                    ForEach(blocks) { block in
                        let box = block.boundingBox
                        let rect = CGRect(
                            x: fit.minX + box.minX * fit.width,
                            y: fit.minY + (1 - box.maxY) * fit.height,
                            width: max(64, box.width * fit.width),
                            height: max(34, box.height * fit.height)
                        )

                        Group {
                            if selectedID == block.id {
                                ZStack(alignment: .topTrailing) {
                                    TextField("输入文字", text: textBinding(for: block.id), axis: .vertical)
                                        .textFieldStyle(.plain)
                                        .font(.system(size: inlineFontSize(for: rect), weight: .medium))
                                        .lineLimit(1...4)
                                        .padding(.leading, 7)
                                        .padding(.trailing, 28)
                                        .padding(.vertical, 5)
                                        .background(.regularMaterial)
                                        .focused($focusedID, equals: block.id)
                                        .onTapGesture {
                                            selectedID = block.id
                                            focusedID = block.id
                                        }
                                    Button(role: .destructive, action: deleteSelectedBlock) {
                                        Image(systemName: "trash.fill")
                                            .font(.system(size: 10, weight: .bold))
                                            .foregroundStyle(.white)
                                            .frame(width: 22, height: 22)
                                            .background(Color.red)
                                            .clipShape(Circle())
                                    }
                                    .buttonStyle(.plain)
                                    .padding(4)
                                    .help("删除整个文字区域")
                                }
                                .overlay(
                                    RoundedRectangle(cornerRadius: 5)
                                        .stroke(Color.blue, lineWidth: 2.5)
                                )
                            } else {
                                Button {
                                    selectedID = block.id
                                    focusedID = block.id
                                } label: {
                                    ZStack {
                                        RoundedRectangle(cornerRadius: 5)
                                            .fill(Color.cyan.opacity(0.035))
                                        RoundedRectangle(cornerRadius: 5)
                                            .stroke(
                                                Color.cyan.opacity(0.95),
                                                style: StrokeStyle(lineWidth: 1.5, dash: [5, 3])
                                            )
                                    }
                                }
                                .buttonStyle(.plain)
                                .help("点击后直接在图片原位置编辑")
                            }
                        }
                        .frame(width: rect.width, height: rect.height)
                        .position(x: rect.midX, y: rect.midY)
                    }

                    VStack {
                        Spacer()
                        HStack(spacing: 8) {
                            Image(systemName: "cursorarrow.click.2")
                            Text("点击图片中的文字即可原地修改；红色按钮删除整个文字区域")
                            Spacer()
                            Text("共 \(blocks.count) 处")
                        }
                        .font(.system(size: 11, weight: .medium))
                        .padding(.horizontal, 12)
                        .frame(height: 34)
                        .background(.regularMaterial)
                    }
                }
            }
        }
        .frame(minWidth: 860, idealWidth: 1080, minHeight: 620, idealHeight: 720)
    }
}

private func aspectFitRect(imageSize: CGSize, in containerSize: CGSize) -> CGRect {
    guard imageSize.width > 0, imageSize.height > 0,
          containerSize.width > 0, containerSize.height > 0 else { return .zero }
    let scale = min(containerSize.width / imageSize.width, containerSize.height / imageSize.height)
    let size = CGSize(width: imageSize.width * scale, height: imageSize.height * scale)
    return CGRect(
        x: (containerSize.width - size.width) / 2,
        y: (containerSize.height - size.height) / 2,
        width: size.width,
        height: size.height
    )
}

private struct ImageTranslationEditorSheet: View {
    @Environment(\.dismiss) private var dismiss
    let source: NSImage
    let resetBlocks: [EditableImageTranslationBlock]
    let onApply: ([EditableImageTranslationBlock]) -> Void
    @State private var blocks: [EditableImageTranslationBlock]
    @State private var selectedID: UUID?
    @State private var dragStartBox: CGRect?

    init(
        source: NSImage,
        blocks: [EditableImageTranslationBlock],
        resetBlocks: [EditableImageTranslationBlock],
        onApply: @escaping ([EditableImageTranslationBlock]) -> Void
    ) {
        self.source = source
        self.resetBlocks = resetBlocks
        self.onApply = onApply
        _blocks = State(initialValue: blocks)
        _selectedID = State(initialValue: blocks.first?.id)
    }

    private var selectedIndex: Int? {
        guard let selectedID else { return nil }
        return blocks.firstIndex { $0.id == selectedID }
    }

    private func binding<Value>(for keyPath: WritableKeyPath<EditableImageTranslationBlock, Value>) -> Binding<Value> {
        Binding(
            get: {
                guard let index = selectedIndex else {
                    preconditionFailure("没有选中译文框")
                }
                return blocks[index][keyPath: keyPath]
            },
            set: { value in
                guard let index = selectedIndex else { return }
                blocks[index][keyPath: keyPath] = value
            }
        )
    }

    private func sizeBinding(isWidth: Bool) -> Binding<Double> {
        Binding(
            get: {
                guard let index = selectedIndex else { return 0.1 }
                let box = blocks[index].boundingBox
                return Double(isWidth ? box.width : box.height)
            },
            set: { value in
                guard let index = selectedIndex else { return }
                var box = blocks[index].boundingBox
                if isWidth {
                    box.size.width = CGFloat(value)
                    box.origin.x = min(box.origin.x, 1 - box.width)
                } else {
                    box.size.height = CGFloat(value)
                    box.origin.y = min(box.origin.y, 1 - box.height)
                }
                blocks[index].boundingBox = box
            }
        )
    }

    var body: some View {
        VStack(spacing: 0) {
            HStack {
                VStack(alignment: .leading, spacing: 3) {
                    Text("编辑图片译文")
                        .font(.system(size: 18, weight: .bold))
                    Text("拖动蓝色译文框改变位置，右侧可修改文字、大小、颜色和对齐。")
                        .font(.system(size: 11))
                        .foregroundStyle(Color.secondary)
                }
                Spacer()
                Button("重置") {
                    let restored = resetBlocks
                    blocks = restored
                    selectedID = restored.first?.id
                }
                Button("取消") { dismiss() }
                    .keyboardShortcut(.cancelAction)
                Button("应用") {
                    onApply(blocks)
                    dismiss()
                }
                .buttonStyle(.borderedProminent)
                .keyboardShortcut(.defaultAction)
            }
            .padding(.horizontal, 20)
            .frame(height: 64)
            Divider()

            HStack(spacing: 0) {
                GeometryReader { proxy in
                    let fit = aspectFitRect(imageSize: source.size, in: proxy.size)
                    ZStack(alignment: .topLeading) {
                        Color(nsColor: .textBackgroundColor)
                        Image(nsImage: source)
                            .resizable()
                            .interpolation(.high)
                            .frame(width: fit.width, height: fit.height)
                            .position(x: fit.midX, y: fit.midY)
                        ForEach(blocks) { block in
                            let box = block.boundingBox
                            let rect = CGRect(
                                x: fit.minX + box.minX * fit.width,
                                y: fit.minY + (1 - box.maxY) * fit.height,
                                width: max(18, box.width * fit.width),
                                height: max(16, box.height * fit.height)
                            )
                            ZStack {
                                RoundedRectangle(cornerRadius: 5)
                                    .fill(Color.blue.opacity(selectedID == block.id ? 0.23 : 0.10))
                                RoundedRectangle(cornerRadius: 5)
                                    .stroke(selectedID == block.id ? Color.blue : Color.white.opacity(0.8), lineWidth: selectedID == block.id ? 2 : 1)
                                Text(block.translatedText)
                                    .font(.system(size: 10, weight: .semibold))
                                    .foregroundStyle(Color.primary)
                                    .lineLimit(2)
                                    .padding(3)
                            }
                            .frame(width: rect.width, height: rect.height)
                            .position(x: rect.midX, y: rect.midY)
                            .contentShape(Rectangle())
                            .gesture(
                                DragGesture(minimumDistance: 0)
                                    .onChanged { value in
                                        if selectedID != block.id {
                                            selectedID = block.id
                                            dragStartBox = block.boundingBox
                                        } else if dragStartBox == nil {
                                            dragStartBox = block.boundingBox
                                        }
                                        guard let start = dragStartBox,
                                              let index = blocks.firstIndex(where: { $0.id == block.id }),
                                              fit.width > 0, fit.height > 0 else { return }
                                        let deltaX = value.translation.width / fit.width
                                        let deltaY = -value.translation.height / fit.height
                                        var moved = start
                                        moved.origin.x = min(max(0, start.minX + deltaX), 1 - start.width)
                                        moved.origin.y = min(max(0, start.minY + deltaY), 1 - start.height)
                                        blocks[index].boundingBox = moved
                                    }
                                    .onEnded { _ in dragStartBox = nil }
                            )
                        }
                    }
                    .clipped()
                }

                Divider()

                VStack(alignment: .leading, spacing: 16) {
                    if selectedIndex != nil {
                        Text("选中的译文框")
                            .font(.system(size: 14, weight: .bold))
                        TextEditor(text: binding(for: \.translatedText))
                            .font(.system(size: 13))
                            .frame(minHeight: 100)
                            .padding(7)
                            .background(Color(nsColor: .textBackgroundColor))
                            .clipShape(RoundedRectangle(cornerRadius: 8))

                        Group {
                            HStack {
                                Text("字号")
                                Slider(value: binding(for: \.fontScale), in: 0.6...2, step: 0.05)
                                Text("\(Int(binding(for: \.fontScale).wrappedValue * 100))%")
                                    .frame(width: 42, alignment: .trailing)
                            }
                            Picker("对齐", selection: binding(for: \.alignment)) {
                                ForEach(ImageOverlayTextAlignment.allCases) { alignment in
                                    Text(alignment.title).tag(alignment)
                                }
                            }
                            .pickerStyle(.segmented)
                            Picker("文字颜色", selection: binding(for: \.textColor)) {
                                ForEach(ImageOverlayTextColor.allCases) { color in
                                    Text(color.title).tag(color)
                                }
                            }
                            HStack {
                                Text("宽度")
                                Slider(value: sizeBinding(isWidth: true), in: 0.04...0.96, step: 0.005)
                            }
                            HStack {
                                Text("高度")
                                Slider(value: sizeBinding(isWidth: false), in: 0.025...0.60, step: 0.005)
                            }
                        }
                        .font(.system(size: 12))

                        Spacer()
                        Button(role: .destructive) {
                            guard let index = selectedIndex else { return }
                            blocks.remove(at: index)
                            selectedID = blocks.first?.id
                        } label: {
                            Label("删除这个译文框", systemImage: "trash")
                        }
                    } else {
                        VStack(spacing: 10) {
                            Image(systemName: "rectangle.dashed")
                                .font(.system(size: 28))
                                .foregroundStyle(Color.secondary)
                            Text("没有译文框")
                                .font(.system(size: 14, weight: .semibold))
                            Text("可点击“重置”恢复识别结果。")
                                .font(.system(size: 11))
                                .foregroundStyle(Color.secondary)
                        }
                        .frame(maxWidth: .infinity, maxHeight: .infinity)
                    }
                }
                .padding(18)
                .frame(width: 310)
            }
        }
        .frame(minWidth: 900, idealWidth: 1080, minHeight: 620, idealHeight: 760)
    }
}

private struct ImageComparisonSheet: View {
    @Environment(\.dismiss) private var dismiss
    let source: NSImage
    let translated: NSImage
    @State private var reveal: Double = 0.5

    var body: some View {
        VStack(spacing: 0) {
            HStack {
                Text("原图与译图对比")
                    .font(.system(size: 18, weight: .bold))
                Spacer()
                Text("拖动滑杆查看")
                    .font(.system(size: 12))
                    .foregroundStyle(Color.secondary)
                Slider(value: $reveal, in: 0...1)
                    .frame(width: 180)
                Button("完成") { dismiss() }
            }
            .padding(.horizontal, 20)
            .frame(height: 58)
            Divider()
            GeometryReader { proxy in
                let fit = aspectFitRect(imageSize: source.size, in: proxy.size)
                ZStack(alignment: .topLeading) {
                    Color(nsColor: .textBackgroundColor)
                    Image(nsImage: source)
                        .resizable()
                        .interpolation(.high)
                        .frame(width: fit.width, height: fit.height)
                        .position(x: fit.midX, y: fit.midY)
                    Image(nsImage: translated)
                        .resizable()
                        .interpolation(.high)
                        .frame(width: fit.width, height: fit.height)
                        .mask(alignment: .leading) {
                            Rectangle().frame(width: fit.width * reveal)
                        }
                        .position(x: fit.midX, y: fit.midY)
                    Rectangle()
                        .fill(Color.blue)
                        .frame(width: 2)
                        .frame(height: fit.height)
                        .position(x: fit.minX + fit.width * reveal, y: fit.midY)
                    Text("译图")
                        .font(.system(size: 11, weight: .bold))
                        .padding(.horizontal, 8).padding(.vertical, 4)
                        .background(.black.opacity(0.58)).foregroundStyle(.white)
                        .clipShape(Capsule())
                        .position(x: fit.minX + 34, y: fit.minY + 22)
                    Text("原图")
                        .font(.system(size: 11, weight: .bold))
                        .padding(.horizontal, 8).padding(.vertical, 4)
                        .background(.blue.opacity(0.78)).foregroundStyle(.white)
                        .clipShape(Capsule())
                        .position(x: fit.maxX - 34, y: fit.minY + 22)
                }
                .contentShape(Rectangle())
                .gesture(DragGesture(minimumDistance: 0).onChanged { value in
                    guard fit.width > 0 else { return }
                    reveal = min(1, max(0, (value.location.x - fit.minX) / fit.width))
                })
            }
        }
        .frame(minWidth: 780, idealWidth: 980, minHeight: 560, idealHeight: 720)
    }
}

private struct SettingsSheet: View {
    @Environment(\.dismiss) private var dismiss
    @ObservedObject var model: TranslatorViewModel
    @AppStorage("fanyi.appearance.mode") private var appearanceMode = "system"
    @AppStorage("fanyi.glass.enabled") private var glassEnabled = true
    @State private var glossarySource = ""
    @State private var glossaryTarget = ""
    @State private var glossarySourceLanguage = "en"
    @State private var glossaryTargetLanguage = "zh-CN"

    private var versionText: String {
        let version = Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? "1.5"
        let build = Bundle.main.object(forInfoDictionaryKey: "CFBundleVersion") as? String ?? ""
        return "V\(version)（Build \(build)）"
    }

    var body: some View {
        VStack(spacing: 0) {
            HStack {
                Text("设置")
                    .font(.system(size: 19, weight: .semibold))
                Spacer()
                Button("完成") { dismiss() }
                    .keyboardShortcut(.cancelAction)
            }
            .padding(.horizontal, 24)
            .frame(height: 62)
            .background {
                AdaptiveGlassBackdrop(materialOpacity: 0.78, tintOpacity: 0.14, regular: false)
            }
            Divider()

            ScrollView {
                VStack(spacing: 18) {
                    settingsCard("翻译与外观", icon: "character.book.closed") {
                        Picker("外观", selection: $appearanceMode) {
                            Text("跟随系统").tag("system")
                            Text("浅色").tag("light")
                            Text("深色").tag("dark")
                        }
                        .pickerStyle(.segmented)
                        Toggle(isOn: $glassEnabled) {
                            VStack(alignment: .leading, spacing: 2) {
                                Text("玻璃质感")
                                Text(glassEnabled ? "使用当前半透明毛玻璃界面" : "使用经典不透明界面")
                                    .font(.system(size: 11))
                                    .foregroundStyle(Color.secondary)
                            }
                        }
                        if let usage = model.deepLUsage {
                            LabeledContent("DeepL 免费额度") {
                                Text("剩余 \(max(0, usage.characterLimit - usage.characterCount)) / \(usage.characterLimit) 字符")
                            }
                        } else {
                            LabeledContent("DeepL 免费额度") { Text(model.hasDeepLKey ? "正在读取" : "尚未设置密钥") }
                        }
                        Toggle("保存翻译历史", isOn: Binding(
                            get: { model.historyRecordingEnabled },
                            set: { model.setHistoryRecording($0) }
                        ))
                        Toggle("保存最近 10 张图片翻译", isOn: Binding(
                            get: { model.imageHistoryRecordingEnabled },
                            set: { model.setImageHistoryRecording($0) }
                        ))
                    }

                    settingsCard("在线朗读", icon: "speaker.wave.2") {
                        HStack {
                            Text("语速")
                            Slider(value: Binding(
                                get: { Double(model.onlineSpeechRatePercent) },
                                set: { model.setOnlineSpeechRatePercent(Int($0.rounded())) }
                            ), in: -30...30, step: 1)
                            Text(model.speechRateDisplayText)
                                .font(.system(size: 12, design: .monospaced))
                                .frame(width: 46, alignment: .trailing)
                        }
                        Picker("音色", selection: Binding(
                            get: { model.onlineVoicePersona },
                            set: { model.setOnlineVoicePersona($0) }
                        )) {
                            ForEach(OnlineVoicePersona.allCases) { persona in
                                Text(persona.title).tag(persona)
                            }
                        }
                        .pickerStyle(.segmented)
                        HStack {
                            LabeledContent("语音缓存") { Text(model.speechCacheSizeText) }
                            Button("清理缓存") { model.clearSpeechCache() }
                        }
                    }

                    settingsCard("权限状态", icon: "lock.shield") {
                        permissionRow("辅助功能", status: model.accessibilityPermissionText) {
                            NoticeAction.openAccessibilitySettings.open()
                        }
                        permissionRow("语音识别", status: model.speechRecognitionPermissionText) {
                            NoticeAction.openSpeechRecognitionSettings.open()
                        }
                        permissionRow("麦克风", status: model.microphonePermissionText) {
                            NoticeAction.openMicrophoneSettings.open()
                        }
                    }

                    settingsCard("自定义词库", icon: "text.book.closed") {
                        HStack(spacing: 10) {
                            TextField("原词，例如 OpenAI", text: $glossarySource)
                            Image(systemName: "arrow.right")
                                .foregroundStyle(Color.secondary)
                            TextField("固定译文，例如 开放人工智能", text: $glossaryTarget)
                        }
                        HStack {
                            languagePicker(selection: $glossarySourceLanguage, excluding: glossaryTargetLanguage)
                            Image(systemName: "arrow.right")
                            languagePicker(selection: $glossaryTargetLanguage, excluding: glossarySourceLanguage)
                            Spacer()
                            Button("添加词条") {
                                model.addGlossaryEntry(
                                    source: glossarySource,
                                    target: glossaryTarget,
                                    sourceLanguage: glossarySourceLanguage,
                                    targetLanguage: glossaryTargetLanguage
                                )
                                glossarySource = ""
                                glossaryTarget = ""
                            }
                            .buttonStyle(.borderedProminent)
                            .disabled(glossarySource.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
                                || glossaryTarget.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
                        }
                        if model.glossary.isEmpty {
                            Text("尚未添加词条。词条会应用到普通翻译和图片翻译。")
                                .font(.system(size: 12))
                                .foregroundStyle(Color.secondary)
                        } else {
                            VStack(spacing: 0) {
                                ForEach(Array(model.glossary.enumerated()), id: \.element.id) { index, entry in
                                    HStack {
                                        Text("\(languageName(entry.sourceLanguage)) · \(entry.source)")
                                        Image(systemName: "arrow.right")
                                            .foregroundStyle(Color.secondary)
                                        Text("\(languageName(entry.targetLanguage)) · \(entry.target)")
                                        Spacer()
                                        Button(role: .destructive) {
                                            model.deleteGlossaryEntries(at: IndexSet(integer: index))
                                        } label: {
                                            Image(systemName: "trash")
                                        }
                                        .buttonStyle(.borderless)
                                    }
                                    .font(.system(size: 12))
                                    .padding(.vertical, 8)
                                    if index < model.glossary.count - 1 { Divider() }
                                }
                            }
                            .padding(.horizontal, 10)
                            .glassSurface(cornerRadius: 10)
                        }
                    }

                    settingsCard("关于与更新", icon: "info.circle") {
                        LabeledContent("当前版本") { Text(versionText) }
                        LabeledContent("开发者") { Text("由 null 打造") }
                        Text("本次更新：新增玻璃质感开关，可在当前毛玻璃界面与经典不透明界面之间切换；并统一主界面、历史记录与完整设置的外观。")
                            .font(.system(size: 12))
                            .foregroundStyle(Color.secondary)
                            .fixedSize(horizontal: false, vertical: true)
                    }
                }
                .padding(22)
            }
        }
        .frame(width: 760, height: 650)
        .background {
            AdaptiveGlassBackdrop(materialOpacity: 0.92, tintOpacity: 0.24)
                .ignoresSafeArea()
        }
        .onAppear {
            model.refreshSpeechCacheSize()
            if model.hasDeepLKey { Task { await model.fetchDeepLUsage() } }
        }
    }

    private func settingsCard<Content: View>(_ title: String, icon: String, @ViewBuilder content: () -> Content) -> some View {
        VStack(alignment: .leading, spacing: 12) {
            Label(title, systemImage: icon)
                .font(.system(size: 15, weight: .semibold))
            content()
        }
        .padding(16)
        .frame(maxWidth: .infinity, alignment: .leading)
        .glassSurface(cornerRadius: 14)
    }

    private func permissionRow(_ title: String, status: String, action: @escaping () -> Void) -> some View {
        HStack {
            Text(title)
            Spacer()
            Label(status, systemImage: status == "已允许" ? "checkmark.circle.fill" : "exclamationmark.circle")
                .foregroundStyle(status == "已允许" ? Color.green : Color.orange)
            Button("系统设置") { action() }
        }
        .font(.system(size: 12))
    }

    private func languagePicker(selection: Binding<String>, excluding: String) -> some View {
        Picker("", selection: selection) {
            ForEach(concreteLanguages.filter { $0 != excluding }, id: \.self) { code in
                Text(languageName(code)).tag(code)
            }
        }
        .labelsHidden()
        .frame(width: 120)
    }
}

private struct TranslatorView: View {
    @StateObject private var model = TranslatorViewModel.shared
    @AppStorage("fanyi.appearance.mode") private var appearanceMode = "system"
    @AppStorage("fanyi.glass.enabled") private var glassEnabled = true
    @Environment(\.colorScheme) private var colorScheme
    @State private var showHistory = false
    @State private var showDeepLSettings = false
    @State private var isImageDropTargeted = false
    @State private var imagePreview: ImagePreviewItem?
    @State private var showImageComparison = false
    @State private var showImageEditor = false
    @State private var showSettings = false
    private let accent = MacVisualTokens.accent
    private let success = Color(nsColor: .systemGreen)
    private var effectiveDarkMode: Bool { colorScheme == .dark }
    private var preferredScheme: ColorScheme? {
        switch appearanceMode {
        case "light": return .light
        case "dark": return .dark
        default: return nil
        }
    }
    private var appearanceModeLabel: String {
        switch appearanceMode {
        case "light": return "浅色"
        case "dark": return "深色"
        default: return "跟随系统"
        }
    }
    private var ink: Color { MacVisualTokens.label }
    private var line: Color { MacVisualTokens.separator.opacity(effectiveDarkMode ? 0.78 : 0.62) }
    private var surface: Color { MacVisualTokens.controlFill }
    /// All non-editor surfaces use the same material strength and tint.  Keeping
    /// this in one place prevents the left/right panes and the header/footer from
    /// drifting into visibly different glass tones.
    // A shared semi-transparent strength for every glass surface.  The same
    // value is used in light and dark mode so the title bar and content panes
    // keep one consistent translucency level.
    private let glassMaterialOpacity = 0.88
    private var glassTint: Color {
        effectiveDarkMode ? Color.black.opacity(0.32) : Color.white.opacity(0.32)
    }
    private var windowTint: Color { glassTint }
    private var editorTint: Color {
        Color.clear
    }

    var body: some View {
        ZStack {
            UnifiedGlassLayer(
                tint: windowTint,
                materialOpacity: glassMaterialOpacity,
                isUltraThin: false,
                isRegular: true
            )
                .ignoresSafeArea()

            VStack(spacing: 0) {
                header
                translatorCard
                    .padding(.horizontal, 24)
                    .padding(.top, 20)
                    .padding(.bottom, 22)
                    .frame(maxHeight: .infinity)
            }
        }
        .frame(minWidth: 900, minHeight: 560)
        .preferredColorScheme(preferredScheme)
        .onAppear {
            migrateAppearanceIfNeeded()
            applyAppearance()
            if model.hasDeepLKey {
                Task { await model.fetchDeepLUsage() }
            }
        }
        .onChange(of: appearanceMode) { _ in applyAppearance() }
        .onReceive(NotificationCenter.default.publisher(for: NSApplication.didBecomeActiveNotification)) { _ in
            if model.hasDeepLKey {
                Task { await model.fetchDeepLUsage() }
            }
        }
        .overlay(alignment: .topLeading) {
            if #available(macOS 15.0, *) {
                AppleTranslationWorker(model: model)
            }
        }
        .sheet(isPresented: $showHistory) {
            HistorySheet(model: model, isPresented: $showHistory)
        }
        .sheet(isPresented: $showDeepLSettings) {
            DeepLSettingsSheet(model: model, isPresented: $showDeepLSettings)
        }
        .sheet(item: $imagePreview) { item in
            ImagePreviewSheet(item: item)
        }
        .sheet(isPresented: $model.isEditingImageOCR) {
            if let source = model.sourceImage {
                ImageOCRReviewSheet(
                    model: model,
                    source: source,
                    blocks: model.imageOCRBlocksForEditing()
                )
            }
        }
        .sheet(isPresented: $showImageComparison) {
            if let source = model.sourceImage, let translated = model.translatedImage {
                ImageComparisonSheet(source: source, translated: translated)
            }
        }
        .sheet(isPresented: $showImageEditor) {
            if let source = model.sourceImage {
                ImageTranslationEditorSheet(
                    source: source,
                    blocks: model.editableImageBlocks,
                    resetBlocks: model.resetImageEdits(),
                    onApply: model.applyImageEdits
                )
            }
        }
        .sheet(isPresented: $showSettings) {
            SettingsSheet(model: model)
        }
        .onReceive(NotificationCenter.default.publisher(for: .translateSelectedText)) { notification in
            let processID = (notification.userInfo?["pid"] as? Int).map(pid_t.init)
            model.translateSelectedText(from: processID)
        }
    }

    private var header: some View {
        HStack {
            HStack(spacing: 12) {
                Image(nsImage: NSApplication.shared.applicationIconImage)
                    .resizable()
                    .scaledToFit()
                    .frame(width: 38, height: 38)
                    .clipShape(RoundedRectangle(cornerRadius: 9, style: .continuous))
                VStack(alignment: .leading, spacing: 2) {
                    Text("Mac翻译")
                        .font(.system(size: 20, weight: .semibold))
                        .foregroundStyle(ink)
                    Label(engineStatusText, systemImage: model.selectedEngine == .apple ? "desktopcomputer" : "checkmark.shield")
                        .font(.system(size: 12, weight: .regular))
                        .foregroundStyle(MacVisualTokens.secondaryLabel)
                }
            }
            Spacer()
            HStack(spacing: 8) {
                Menu {
                    Button {
                        model.setEngine(.apple)
                    } label: {
                        Label("Apple 系统翻译", systemImage: model.selectedEngine == .apple ? "checkmark" : "apple.logo")
                    }
                    Button {
                        if model.hasDeepLKey {
                            model.setEngine(.deepl)
                        } else {
                            showDeepLSettings = true
                        }
                    } label: {
                        Label("DeepL 高质量", systemImage: model.selectedEngine == .deepl ? "checkmark" : "sparkles")
                    }
                    Divider()
                    if let usage = model.deepLUsage {
                        Text("DeepL 用量：\(usage.characterCount) / \(usage.characterLimit) 字符（\(usage.usedPercentText)%）")
                    }
                    Button(model.hasDeepLKey ? "更新 DeepL 密钥…" : "设置 DeepL 密钥…") {
                        showDeepLSettings = true
                    }
                } label: {
                    HStack(spacing: 8) {
                        Circle()
                            .fill(model.selectedEngine == .apple ? accent : success)
                            .frame(width: 7, height: 7)
                        Text(model.selectedEngine.shortName)
                        Image(systemName: "chevron.down")
                            .font(.system(size: 8, weight: .semibold))
                            .foregroundStyle(MacVisualTokens.tertiaryLabel)
                    }
                    .font(.system(size: 14, weight: .medium))
                    .foregroundStyle(ink)
                    .macHoverControl(horizontalPadding: 10, height: 36)
                }
                .menuStyle(.borderlessButton)
                .menuIndicator(.hidden)
                .fixedSize()

                Button(action: { showHistory = true }) {
                    HStack(spacing: 7) {
                        Image(systemName: "clock.arrow.circlepath").font(.system(size: 14, weight: .medium))
                        Text("历史记录")
                    }
                    .font(.system(size: 14, weight: .medium))
                    .foregroundStyle(ink)
                    .macHoverControl(horizontalPadding: 10, height: 36)
                }
                .buttonStyle(.plain)

                Menu {
                    Button {
                        showSettings = true
                    } label: {
                        Label("打开完整设置…", systemImage: "gearshape.2")
                    }
                    Divider()
                    Menu {
                        Button { appearanceMode = "system" } label: {
                            Label("跟随系统", systemImage: appearanceMode == "system" ? "checkmark" : "circle")
                        }
                        Button { appearanceMode = "light" } label: {
                            Label("浅色", systemImage: appearanceMode == "light" ? "checkmark" : "sun.max")
                        }
                        Button { appearanceMode = "dark" } label: {
                            Label("深色", systemImage: appearanceMode == "dark" ? "checkmark" : "moon.fill")
                        }
                    } label: {
                        Label("外观：\(appearanceModeLabel)", systemImage: "paintpalette")
                    }
                    Button {
                        model.requestGlobalShortcutPermission()
                    } label: {
                        Label("划词翻译权限（\(globalShortcutDescription)）…", systemImage: "command")
                    }
                    Divider()
                    Button {
                        model.setHistoryRecording(!model.historyRecordingEnabled)
                    } label: {
                        Label("记录翻译历史", systemImage: model.historyRecordingEnabled ? "checkmark" : "")
                    }
                    Button {
                        model.setImageHistoryRecording(!model.imageHistoryRecordingEnabled)
                    } label: {
                        Label("记录最近 10 张译图", systemImage: model.imageHistoryRecordingEnabled ? "checkmark" : "")
                    }
                    Divider()
                    Button(role: .destructive) {
                        NSApp.terminate(nil)
                    } label: {
                        Label("完全退出", systemImage: "power")
                    }
                } label: {
                    Image(systemName: "gearshape")
                        .font(.system(size: 16, weight: .medium))
                        .foregroundStyle(ink)
                        .macHoverControl(cornerRadius: 9, horizontalPadding: 0, height: 36)
                        .frame(width: 36)
                }
                .menuStyle(.borderlessButton)
                .menuIndicator(.hidden)
                .fixedSize()
                .help("设置")
            }
        }
        .padding(.leading, 48)
        .padding(.trailing, 24)
        .padding(.top, 5)
        .frame(maxWidth: .infinity)
        .frame(height: 78)
        .overlay(alignment: .bottom) {
            Rectangle().fill(line).frame(height: 0.5)
        }
    }

    private var translatorCard: some View {
        VStack(spacing: 0) {
            ZStack {
                HStack {
                    LanguageMenu(color: accent, textColor: ink, selection: model.sourceLanguage, options: sourceLanguagesWithAuto, onChange: model.setSourceLanguage)
                    Spacer()
                    LanguageMenu(color: accent, textColor: ink, selection: model.targetLanguage, onChange: model.setTargetLanguage)
                }

                Button(action: model.swapLanguages) {
                    Image(systemName: "arrow.left.arrow.right")
                        .font(.system(size: 14, weight: .medium))
                        .foregroundStyle(accent)
                        .frame(width: 40, height: 40)
                }
                    .buttonStyle(FloatingGlassButtonStyle())
                    .disabled(model.hasImageTranslation)
                    .opacity(model.hasImageTranslation ? 0.42 : 1)
            }
            .padding(.horizontal, 22)
            .frame(height: 58)

            Rectangle().fill(line).frame(height: 0.5)

            HStack(spacing: 0) {
                VStack(spacing: 0) {
                    ZStack {
                        if let sourceImage = model.sourceImage {
                            TranslationImagePane(image: sourceImage, title: "原图", accent: accent) {
                                imagePreview = ImagePreviewItem(title: "原图", image: sourceImage)
                            }
                            .contextMenu {
                                Button(action: model.editImageSourceText) {
                                    Label("编辑原图文字", systemImage: "text.viewfinder")
                                }
                                .disabled(!model.canEditImageSourceText)
                                Button {
                                    imagePreview = ImagePreviewItem(title: "原图", image: sourceImage)
                                } label: {
                                    Label("放大查看", systemImage: "arrow.up.left.and.arrow.down.right")
                                }
                            }

                            VStack {
                                HStack {
                                    Button(action: model.editImageSourceText) {
                                        Label("编辑原图文字", systemImage: "text.viewfinder")
                                            .font(.system(size: 11, weight: .semibold))
                                            .padding(.horizontal, 10)
                                            .frame(height: 30)
                                            .glassSurface(cornerRadius: 8)
                                    }
                                    .buttonStyle(.plain)
                                    .foregroundStyle(ink)
                                    .disabled(!model.canEditImageSourceText)
                                    Spacer()
                                }
                                Spacer()
                            }
                            .padding(12)
                        } else {
                            SubmitTextEditor(text: Binding(
                                get: { model.sourceText },
                                set: { model.sourceText = String($0.prefix(maxSourceCharacters)) }
                            ), isDarkMode: effectiveDarkMode, onSubmit: model.translate, onImageDrop: model.recognizeAndTranslateImage)
                            .background(editorTint)
                        }

                        if model.isRecognizingImage || model.isCapturingRegion {
                            VStack(spacing: 12) {
                                ProgressView().controlSize(.regular)
                                Text(model.isCapturingRegion
                                     ? (model.screenCaptureCountdown > 0
                                        ? "请切换到目标窗口，\(model.screenCaptureCountdown) 秒后开始框选…"
                                        : "请在屏幕上框选要翻译的区域…")
                                     : "正在识别图片中的文字…")
                                    .font(.system(size: 14, weight: .semibold))
                                Text("支持英语、中文、日语和韩语")
                                    .font(.system(size: 11))
                                    .foregroundStyle(Color.secondary)
                            }
                            .frame(maxWidth: .infinity, maxHeight: .infinity)
                            .background {
                                AdaptiveGlassBackdrop(materialOpacity: 0.96, tintOpacity: 0.18)
                            }
                        } else if model.sourceImage == nil && model.sourceText.isEmpty {
                            VStack(spacing: 9) {
                                Text(inputPlaceholder)
                                    .font(.system(size: 15, weight: .regular))
                                Label(imageDropHint, systemImage: "photo.on.rectangle.angled")
                                    .font(.system(size: 12, weight: .regular))
                                    .foregroundStyle(MacVisualTokens.secondaryLabel)
                            }
                            .foregroundStyle(MacVisualTokens.secondaryLabel)
                            .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .top)
                            .padding(.top, 86)
                            .allowsHitTesting(false)
                        }
                    }
                    HStack {
                        Spacer()
                        Text(model.hasImageTranslation ? "图片原文 · \(model.sourceText.count) 字符" : "\(model.sourceText.count) / \(maxSourceCharacters)")
                            .font(.system(size: 11))
                            .foregroundStyle(MacVisualTokens.tertiaryLabel)
                    }
                    .padding(.horizontal, 28).frame(height: 42)
                }
                .frame(maxWidth: .infinity)
                .onDrop(of: [UTType.fileURL.identifier], isTargeted: $isImageDropTargeted, perform: handleImageDrop)
                .overlay {
                    if isImageDropTargeted {
                        ZStack {
                            RoundedRectangle(cornerRadius: 16)
                                .fill(accent.opacity(0.09))
                            RoundedRectangle(cornerRadius: 16)
                                .stroke(accent, style: StrokeStyle(lineWidth: 1.5, dash: [7, 5]))
                            VStack(spacing: 10) {
                                Image(systemName: "photo.badge.arrow.down")
                                    .font(.system(size: 34, weight: .medium))
                                Text("松开鼠标，开始翻译图片")
                                    .font(.system(size: 15, weight: .medium))
                                Text("支持英语、中文、日语和韩语")
                                    .font(.system(size: 12))
                            }
                            .foregroundStyle(accent)
                        }
                        .padding(10)
                        .allowsHitTesting(false)
                    }
                }

                Rectangle()
                    .fill(glassEnabled
                        ? AnyShapeStyle(LinearGradient(
                            colors: [Color.white.opacity(effectiveDarkMode ? 0.36 : 0.90),
                                     MacVisualTokens.separator.opacity(0.40),
                                     Color.white.opacity(effectiveDarkMode ? 0.08 : 0.20)],
                            startPoint: .top,
                            endPoint: .bottom
                        ))
                        : AnyShapeStyle(line))
                    .frame(width: 0.8)

                VStack(spacing: 0) {
                    Group {
                        if model.isLoading {
                            VStack(spacing: 12) { ProgressView().controlSize(.regular); Text("正在理解并翻译…") }
                                .font(.system(size: 12)).foregroundStyle(Color.secondary).frame(maxWidth: .infinity, maxHeight: .infinity)
                        } else if let notice = model.notice {
                            VStack(spacing: 12) {
                                Image(systemName: notice.kind.icon)
                                    .font(.system(size: 18, weight: .semibold))
                                    .foregroundStyle(notice.kind.color)
                                    .frame(width: 34, height: 34)
                                    .background(notice.kind.color.opacity(0.12))
                                    .clipShape(Circle())
                                Text(notice.message).font(.system(size: 12)).foregroundStyle(Color.secondary)
                                    .multilineTextAlignment(.center)
                                    .frame(maxWidth: 360)
                                HStack(spacing: 12) {
                                    if let action = notice.action {
                                        Button(action.buttonTitle) { action.open() }.buttonStyle(.bordered)
                                    }
                                    if notice.kind == .error {
                                        Button("重新尝试", action: model.translate).buttonStyle(.bordered)
                                    }
                                }
                            }.frame(maxWidth: .infinity, maxHeight: .infinity)
                        } else if let translatedImage = model.translatedImage {
                            TranslationImagePane(image: translatedImage, title: "译文图片 · \(model.imageRenderStyle.title)", accent: accent) {
                                imagePreview = ImagePreviewItem(title: "译文图片 · \(model.imageRenderStyle.title)", image: translatedImage)
                            }
                        } else if model.translatedText.isEmpty {
                            VStack(spacing: 12) {
                                ZStack {
                                    RoundedRectangle(cornerRadius: 13, style: .continuous)
                                        .fill(.thinMaterial)
                                        .frame(width: 54, height: 54)
                                        .macGlassBorder(cornerRadius: 13)
                                    Image(systemName: "character.book.closed")
                                        .font(.system(size: 21, weight: .regular))
                                        .foregroundStyle(accent)
                                }
                                Text(model.hasImageTranslation ? "译文图片会显示在这里" : "译文会显示在这里")
                                    .font(.system(size: 12)).foregroundStyle(MacVisualTokens.secondaryLabel)
                            }.frame(maxWidth: .infinity, maxHeight: .infinity)
                        } else {
                            ScrollView { Text(model.highlightedTranslatedText).font(.system(size: 15)).lineSpacing(5).foregroundStyle(ink).frame(maxWidth: .infinity, alignment: .topLeading).padding(24).textSelection(.enabled) }
                        }
                    }
                }
                .frame(maxWidth: .infinity)
            }
            .frame(minHeight: 300, maxHeight: .infinity)
            .background {
                AdaptiveGlassBackdrop(materialOpacity: 0.96, tintOpacity: 0.18)
            }
            .overlay {
                Rectangle()
                    .stroke(glassEnabled
                        ? AnyShapeStyle(LinearGradient(
                            colors: [Color.white.opacity(effectiveDarkMode ? 0.40 : 0.94),
                                     MacVisualTokens.separator.opacity(0.38),
                                     Color.white.opacity(effectiveDarkMode ? 0.10 : 0.22)],
                            startPoint: .topLeading,
                            endPoint: .bottomTrailing
                        ))
                        : AnyShapeStyle(line),
                        lineWidth: 0.8)
                    .allowsHitTesting(false)
            }

            Rectangle().fill(line).frame(height: 0.5)

            HStack(spacing: 12) {
                HStack(spacing: 10) {
                    Button(action: model.toggleListening) {
                        Label(model.isListening ? "停止录音" : "语音输入", systemImage: model.isListening ? "stop.circle.fill" : "mic")
                    }
                    .foregroundStyle(model.isListening ? Color(nsColor: .systemRed) : ink)
                    Divider().frame(height: 18)
                    Button(action: chooseImage) {
                        Label("翻译图片", systemImage: "photo")
                    }
                    .disabled(model.isRecognizingImage || model.isCapturingRegion)
                    Divider().frame(height: 18)
                    Button(action: model.captureScreenRegionAndTranslate) {
                        Label("截图翻译", systemImage: "viewfinder")
                    }
                    .disabled(model.isRecognizingImage || model.isCapturingRegion || model.isLoading)
                    .help("点击后有 3 秒切换到目标窗口，然后拖动框选")
                    Divider().frame(height: 18)
                    Button(action: model.clear) {
                        Label("清空", systemImage: "trash")
                    }
                    .disabled(model.sourceText.isEmpty && model.translatedText.isEmpty && !model.hasImageTranslation)
                }
                .font(.system(size: 12, weight: .medium))
                .buttonStyle(.borderless)
                .foregroundStyle(ink)
                .frame(maxWidth: .infinity, alignment: .leading)

                Button(action: model.translate) {
                    HStack(spacing: 12) {
                        Text(model.isLoading ? "翻译中" : (model.hasImageTranslation ? "重新翻译图片" : "开始翻译"))
                        Text("Enter")
                            .font(.system(size: 9.5, weight: .medium))
                            .padding(.horizontal, 6)
                            .padding(.vertical, 3)
                            .background(Color.white.opacity(0.12))
                            .clipShape(RoundedRectangle(cornerRadius: 5, style: .continuous))
                            .overlay(RoundedRectangle(cornerRadius: 5, style: .continuous).stroke(Color.white.opacity(0.42), lineWidth: 0.5))
                    }
                        .font(.system(size: 13, weight: .semibold))
                        .frame(width: 174, height: 40)
                        .foregroundStyle(model.canTranslate ? Color.white : MacVisualTokens.secondaryLabel)
                        .background {
                            let shape = RoundedRectangle(cornerRadius: 11, style: .continuous)
                            ZStack {
                                if glassEnabled {
                                    shape.fill(.regularMaterial).opacity(0.94)
                                }
                                shape.fill(
                                    model.canTranslate
                                        ? (glassEnabled
                                            ? AnyShapeStyle(LinearGradient(
                                                colors: [accent.opacity(0.70),
                                                         accent.opacity(0.44)],
                                                startPoint: .topLeading,
                                                endPoint: .bottomTrailing
                                            ))
                                            : AnyShapeStyle(accent))
                                        : AnyShapeStyle(surface.opacity(effectiveDarkMode ? 0.30 : 0.46))
                                )
                            }
                        }
                        .clipShape(RoundedRectangle(cornerRadius: 11, style: .continuous))
                        .overlay {
                            RoundedRectangle(cornerRadius: 11, style: .continuous)
                                .stroke(glassEnabled
                                    ? AnyShapeStyle(LinearGradient(
                                        colors: [Color.white.opacity(model.canTranslate
                                            ? (effectiveDarkMode ? 0.40 : 0.82)
                                            : (effectiveDarkMode ? 0.20 : 0.38)),
                                                 accent.opacity(model.canTranslate ? 0.34 : 0.10)],
                                        startPoint: .topLeading,
                                        endPoint: .bottomTrailing
                                    ))
                                    : AnyShapeStyle(line),
                                    lineWidth: 0.8)
                        }
                        .shadow(
                            color: glassEnabled ? accent.opacity(model.canTranslate ? 0.18 : 0) : Color.clear,
                            radius: glassEnabled ? 10 : 0,
                            y: glassEnabled ? 4 : 0
                        )
                }
                .buttonStyle(.plain).disabled(!model.canTranslate)
                .keyboardShortcut(.return, modifiers: [])

                HStack(spacing: 10) {
                    if model.hasImageTranslation {
                        Menu {
                            Section("译图效果") {
                                ForEach(ImageTranslationRenderStyle.allCases) { style in
                                    Button {
                                        model.setImageRenderStyle(style)
                                    } label: {
                                        Label(style.title, systemImage: model.imageRenderStyle == style ? "checkmark" : style.icon)
                                    }
                                }
                            }
                            Divider()
                            Button {
                                showImageComparison = true
                            } label: {
                                Label("原图 / 译图对比", systemImage: "slider.horizontal.below.rectangle")
                            }
                            .disabled(model.translatedImage == nil)
                            Button(action: model.editImageSourceText) {
                                Label("编辑原图文字", systemImage: "text.viewfinder")
                            }
                            .disabled(!model.canEditImageSourceText)
                            Button {
                                showImageEditor = true
                            } label: {
                                Label("编辑译文框", systemImage: "rectangle.and.pencil.and.ellipsis")
                            }
                            .disabled(model.translatedImage == nil || model.editableImageBlocks.isEmpty)
                            Divider()
                            Button(action: model.copyTranslatedImage) {
                                Label(model.imageActionLabel, systemImage: "photo.on.rectangle")
                            }
                            .disabled(model.translatedImage == nil)
                            Button(action: model.saveTranslatedImage) {
                                Label("保存图片…", systemImage: "square.and.arrow.down")
                            }
                            .disabled(model.translatedImage == nil)
                        } label: {
                            Label("图片工具", systemImage: "ellipsis.circle")
                                .foregroundStyle(ink)
                        }
                        .menuStyle(.borderlessButton)
                        .fixedSize()
                    } else {
                        if let status = model.speechStatusMessage {
                            Text(status).lineLimit(1)
                            Divider().frame(height: 18)
                        }
                        Menu {
                            ForEach(OnlineVoicePersona.allCases) { persona in
                                Button {
                                    model.setOnlineVoicePersona(persona)
                                } label: {
                                    if model.onlineVoicePersona == persona {
                                        Label(persona.title, systemImage: "checkmark")
                                    } else {
                                        Text(persona.title)
                                    }
                                }
                            }
                        } label: {
                            HStack(spacing: 5) {
                                Image(systemName: model.onlineVoicePersona.icon)
                                Text(model.onlineVoicePersona.title)
                                Image(systemName: "chevron.down").font(.system(size: 8, weight: .bold))
                            }
                            .foregroundStyle(ink)
                        }
                        .menuStyle(.borderlessButton)
                        .fixedSize()
                        Divider().frame(height: 18)
                        Button(action: model.speakResult) {
                            Label(resultSpeechButtonTitle, systemImage: resultSpeechButtonIcon)
                        }
                        .disabled(model.translatedText.isEmpty)
                        Divider().frame(height: 18)
                        Button(action: model.copyResult) {
                            Label(model.copyLabel, systemImage: "doc.on.doc")
                        }
                        .disabled(model.translatedText.isEmpty)
                    }
                }
                .font(.system(size: 12, weight: .medium))
                .buttonStyle(.borderless)
                .foregroundStyle(ink)
                .frame(maxWidth: .infinity, alignment: .trailing)
            }
            .padding(.horizontal, 20)
            .frame(height: 64)
        }
        .background {
            AdaptiveGlassBackdrop(materialOpacity: 0.84, tintOpacity: 0.07, regular: false)
        }
        .clipShape(RoundedRectangle(cornerRadius: MacVisualTokens.panelRadius, style: .continuous))
        .overlay {
            RoundedRectangle(cornerRadius: MacVisualTokens.panelRadius, style: .continuous)
                .stroke(glassEnabled
                    ? AnyShapeStyle(LinearGradient(
                        colors: [Color.white.opacity(effectiveDarkMode ? 0.36 : 0.92),
                                 Color.white.opacity(effectiveDarkMode ? 0.12 : 0.30),
                                 MacVisualTokens.separator.opacity(0.46)],
                        startPoint: .topLeading,
                        endPoint: .bottomTrailing
                    ))
                    : AnyShapeStyle(line),
                    lineWidth: 0.9)
        }
        .shadow(
            color: glassEnabled ? Color.black.opacity(effectiveDarkMode ? 0.20 : 0.10) : Color.black.opacity(0.05),
            radius: glassEnabled ? 18 : 4,
            y: glassEnabled ? 8 : 1
        )
        .frame(maxWidth: 1120, maxHeight: .infinity)
    }

    private func chooseImage() {
        let panel = NSOpenPanel()
        panel.title = "选择要翻译的图片"
        panel.message = "支持英语、中文、日语和韩语"
        panel.prompt = "翻译图片"
        panel.allowedContentTypes = [.image]
        panel.allowsMultipleSelection = false
        panel.canChooseDirectories = false
        guard panel.runModal() == .OK, !panel.urls.isEmpty else { return }
        model.recognizeAndTranslateImage(at: panel.urls[0])
    }

    private func handleImageDrop(_ providers: [NSItemProvider]) -> Bool {
        guard let provider = providers.first(where: { $0.hasItemConformingToTypeIdentifier(UTType.fileURL.identifier) }) else {
            return false
        }
        provider.loadItem(forTypeIdentifier: UTType.fileURL.identifier, options: nil) { item, _ in
            let url: URL?
            if let directURL = item as? URL {
                url = directURL
            } else if let nsURL = item as? NSURL {
                url = nsURL as URL
            } else if let data = item as? Data {
                url = URL(dataRepresentation: data, relativeTo: nil)
            } else {
                url = nil
            }

            Task { @MainActor in
                guard let url else {
                    model.notice = AppNotice(kind: .error, message: "无法读取拖入的图片，请使用“识别图片”按钮重试")
                    return
                }
                model.recognizeAndTranslateImage(at: url)
            }
        }
        return true
    }

    private var resultSpeechButtonTitle: String {
        if model.isPreparingSpeech { return "停止准备" }
        if model.isResultSpeechPaused { return "继续" }
        if model.isSpeakingResult { return "暂停" }
        return "朗读"
    }

    private var resultSpeechButtonIcon: String {
        if model.isPreparingSpeech { return "xmark.circle" }
        if model.isResultSpeechPaused { return "play.fill" }
        if model.isSpeakingResult { return "pause.fill" }
        return "speaker.wave.2"
    }

    private var engineStatusText: String {
        switch model.selectedEngine {
        case .apple: return "Apple 系统本机翻译"
        case .deepl: return "内容由 DeepL 在线处理"
        }
    }

    private var inputPlaceholder: String {
        switch model.sourceLanguage {
        case "auto": return "输入任意语言，自动检测…"
        case "en": return "在这里输入英文…"
        case "zh-CN": return "在这里输入中文…"
        case "ja": return "在这里输入日文…"
        case "ko": return "在这里输入韩文…"
        default: return "在这里输入文字…"
        }
    }

    private var imageDropHint: String {
        switch model.sourceLanguage {
        case "auto": return "也可以把图片拖到这里"
        case "en": return "也可以把英文图片拖到这里"
        case "zh-CN": return "也可以把中文图片拖到这里"
        case "ja": return "也可以把日文图片拖到这里"
        case "ko": return "也可以把韩文图片拖到这里"
        default: return "也可以把图片拖到这里"
        }
    }

    private func shortLanguage(_ code: String) -> String {
        switch code {
        case "en": return "EN"
        case "zh-CN": return "中"
        case "ja": return "日"
        case "ko": return "韩"
        default: return code
        }
    }

    private func applyAppearance() {
        switch appearanceMode {
        case "light":
            NSApp.appearance = NSAppearance(named: .aqua)
        case "dark":
            NSApp.appearance = NSAppearance(named: .darkAqua)
        default:
            NSApp.appearance = nil
        }
    }

    private func migrateAppearanceIfNeeded() {
        let defaults = UserDefaults.standard
        guard defaults.string(forKey: "fanyi.appearance.mode") == nil,
              defaults.object(forKey: "fanyi.appearance.dark") != nil else { return }
        appearanceMode = defaults.bool(forKey: "fanyi.appearance.dark") ? "dark" : "light"
    }
}

@main
struct TranslationApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) private var appDelegate

    var body: some Scene {
        Window("Mac翻译", id: "main") {
            TranslatorView()
        }
        .windowStyle(.hiddenTitleBar)
        .defaultSize(width: 1060, height: 600)
        .commands {
            CommandGroup(replacing: .newItem) { }
        }
    }
}
