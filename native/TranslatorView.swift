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

struct TranslatorView: View {
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
        .frame(minWidth: 934, minHeight: 560)
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
                    Text("Yike")
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
            Button(model.hasDeepLKey ? "更新 DeepL 密钥" : "设置 DeepL 密钥") {
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

                Button { showSettings = true } label: {
                    Image(systemName: "gearshape")
                        .font(.system(size: 16, weight: .medium))
                        .foregroundStyle(ink)
                        .macHoverControl(cornerRadius: 9, horizontalPadding: 0, height: 36)
                        .frame(width: 36)
                }
                .buttonStyle(.plain)
                .accessibilityLabel("设置")
                .accessibilityIdentifier("openSettings")
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
                            VStack(spacing: 12) {
                                ProgressView().controlSize(.regular)
                                Text(model.translationProgress)
                                Button("取消翻译", action: model.cancelTranslation)
                            }
                                .font(.system(size: 12)).foregroundStyle(Color.secondary).frame(maxWidth: .infinity, maxHeight: .infinity)
                        } else if let diagnostic = model.notice?.diagnostic {
                            ScrollView {
                                VStack(alignment: .leading, spacing: 18) {
                                    TranslationDiagnosticView(diagnostic: diagnostic)
                                    HStack(spacing: 12) {
                                        Button("重新尝试", action: model.translate)
                                            .disabled(!model.canTranslate)
                                        if diagnostic.engine == .deepl || !model.hasDeepLKey {
                                            Button("设置 DeepL 密钥") { showDeepLSettings = true }
                                        }
                                    }
                                    Button(model.selectedEngine == .apple ? "切换到 DeepL" : "切换到 Apple 翻译") {
                                        if model.selectedEngine == .apple && !model.hasDeepLKey {
                                            showDeepLSettings = true
                                        } else {
                                            model.setEngine(model.selectedEngine == .apple ? .deepl : .apple)
                                            model.translate()
                                        }
                                    }
                                }
                                .buttonStyle(.bordered)
                                .padding(24)
                            }
                            .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
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
                                        if model.hasImageTranslation {
                                            Button("换引擎重试") {
                                                model.setEngine(model.selectedEngine == .apple ? .deepl : .apple)
                                                model.translate()
                                            }.buttonStyle(.bordered)
                                        }
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
                            ScrollView { TypewriterTranslationText(text: model.translatedText, highlightedText: model.highlightedTranslatedText, isSpeaking: model.isSpeakingResult).font(.system(size: 15)).lineSpacing(5).foregroundStyle(ink).frame(maxWidth: .infinity, alignment: .topLeading).padding(24).textSelection(.enabled) }
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
