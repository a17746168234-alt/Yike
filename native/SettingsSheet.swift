import SwiftUI
import AppKit

private enum SettingsSection: String, CaseIterable, Identifiable {
    case appearance, account, deepl, speech, shortcuts, permissions, history, glossary, about
    var id: String { rawValue }
    var title: String {
        switch self {
        case .appearance: return "外观"
        case .account: return "账号与安全"
        case .deepl: return "DeepL 密钥与帮助"
        case .speech: return "在线朗读"
        case .shortcuts: return "快捷键与划词"
        case .history: return "历史记录"
        case .permissions: return "权限状态"
        case .glossary: return "自定义词库"
        case .about: return "关于与更新"
        }
    }
    var symbol: String {
        switch self {
        case .appearance: return "paintpalette"
        case .account: return "person.crop.circle.badge.checkmark"
        case .deepl: return "key.horizontal"
        case .speech: return "waveform"
        case .shortcuts: return "keyboard"
        case .history: return "clock.arrow.circlepath"
        case .permissions: return "lock.shield"
        case .glossary: return "text.book.closed"
        case .about: return "info.circle"
        }
    }
    var subtitle: String {
        switch self {
        case .appearance: return "让 Yike 符合你的使用习惯"
        case .account: return "登录与管理你的 Yike 账号"
        case .deepl: return "从申请账号到填写密钥，一步步完成"
        case .speech: return "调整声音、语速与本地缓存"
        case .shortcuts: return "自定义快捷键，随时呼出翻译悬浮窗"
        case .history: return "管理文字与图片的本机记录"
        case .permissions: return "检查语音识别和麦克风的访问权限"
        case .glossary: return "让人名、术语与常用表达保持一致"
        case .about: return "认识 Yike，了解本次更新"
        }
    }
}

struct SettingsSheet: View {
    @Environment(\.dismiss) private var dismiss
    @ObservedObject var model: TranslatorViewModel
    var openAccount = false
    @AppStorage("fanyi.appearance.mode") private var appearanceMode = "system"
    @AppStorage("fanyi.glass.enabled") private var glassEnabled = true
    @State private var selectedSection: SettingsSection = .appearance
    @State private var glossarySource = ""
    @State private var showDeepLSettings = false
    @State private var keyButtonHighlighted = false
    @State private var appleButtonHighlighted = false
    @State private var glossaryTarget = ""
    @State private var glossarySourceLanguage = "en"
    @State private var glossaryTargetLanguage = "zh-CN"

    private var versionText: String {
        let version = Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? "1.5"
        let build = Bundle.main.object(forInfoDictionaryKey: "CFBundleVersion") as? String ?? ""
        return "V\(version)（Build \(build)）"
    }

    var body: some View {
        HStack(spacing: 0) {
            VStack(alignment: .leading, spacing: 22) {
                HStack(spacing: 10) {
                    Image(nsImage: NSApplication.shared.applicationIconImage)
                        .resizable().scaledToFit().frame(width: 34, height: 34)
                        .clipShape(RoundedRectangle(cornerRadius: 8))
                    VStack(alignment: .leading, spacing: 3) {
                        Text("Yike").font(.system(size: 17, weight: .semibold))
                        Text("设置").font(.system(size: 11)).foregroundStyle(.secondary)
                    }
                }
                .padding(.horizontal, 10).padding(.top, 10)

                VStack(spacing: 5) {
                    ForEach(SettingsSection.allCases) { section in
                        Button {
                            selectedSection = section
                        } label: {
                            HStack(spacing: 10) {
                                Image(systemName: section.symbol)
                                    .font(.system(size: 15, weight: .medium))
                                    .frame(width: 28, height: 28)
                                    .foregroundStyle(selectedSection == section ? Color.accentColor : Color.secondary)
                                Text(section.title).font(.system(size: 13, weight: selectedSection == section ? .semibold : .regular))
                                Spacer(minLength: 0)
                            }
                            .padding(.horizontal, 9).padding(.vertical, 5)
                            .frame(maxWidth: .infinity, alignment: .leading)
                            .contentShape(Rectangle())
                            .background(selectedSection == section ? Color.accentColor.opacity(0.13) : Color.clear)
                            .clipShape(RoundedRectangle(cornerRadius: 9))
                        }
                        .buttonStyle(.plain)
                        .accessibilityLabel(section.title)
                        .accessibilityIdentifier("settings." + section.rawValue)
                        .accessibilityAddTraits(selectedSection == section ? [.isSelected] : [])
                    }
                }
                Spacer()
                Text(versionText).font(.system(size: 10)).foregroundStyle(.tertiary)
                    .padding(.horizontal, 10).padding(.bottom, 8)
            }
            .padding(14)
            .frame(width: 210)
            .background(.thinMaterial)
            Divider()
            VStack(spacing: 0) {
                HStack(alignment: .top) {
                    VStack(alignment: .leading, spacing: 6) {
                        Text(selectedSection.title).font(.system(size: 21, weight: .semibold))
                        Text(selectedSection.subtitle).font(.system(size: 12)).foregroundStyle(.secondary)
                    }
                    Spacer()
                    Button("完成") { dismiss() }
                        .keyboardShortcut(.cancelAction)
                }
                .padding(.horizontal, 26).padding(.vertical, 23)
                Divider()
                ScrollView {
                    VStack(alignment: .leading, spacing: 16) {
                        sectionContent
                    }
                    .padding(24)
                    .frame(maxWidth: .infinity, alignment: .topLeading)
                }
                .id(selectedSection)
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity)
        }
        .frame(width: 860, height: 600)
        .background {
            AdaptiveGlassBackdrop(materialOpacity: 0.92, tintOpacity: 0.16)
                .ignoresSafeArea()
        }
        .onAppear {
            if openAccount { selectedSection = .account }
            model.refreshSpeechCacheSize()
            if model.hasDeepLKey { Task { await model.fetchDeepLUsage() } }
        }
        .sheet(isPresented: $showDeepLSettings) {
            DeepLSettingsSheet(model: model, isPresented: $showDeepLSettings)
        }
        .task(id: keyButtonHighlighted) {
            guard keyButtonHighlighted else { return }
            do { try await Task.sleep(nanoseconds: 2_000_000_000) }
            catch { return }
            keyButtonHighlighted = false
        }
        .task(id: appleButtonHighlighted) {
            guard appleButtonHighlighted else { return }
            do { try await Task.sleep(nanoseconds: 300_000_000) }
            catch { return }
            model.setEngine(.apple)
            dismiss()
        }
    }

    @ViewBuilder private var sectionContent: some View {
        switch selectedSection {
        case .appearance:
        settingsCard("显示效果", icon: "paintpalette") {
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
        }
        case .history:
        settingsCard("本机历史记录", icon: "clock.arrow.circlepath") {
            Text("按需保留翻译记录，方便之后查看与复用。").font(.system(size: 12)).foregroundStyle(.secondary)
            Toggle("保存翻译历史", isOn: Binding(
                get: { model.historyRecordingEnabled },
                set: { model.setHistoryRecording($0) }
            ))
            Toggle("保存最近 10 张图片翻译", isOn: Binding(
                get: { model.imageHistoryRecordingEnabled },
                set: { model.setImageHistoryRecording($0) }
            ))
        }
        case .account:
        TrialAccountSettings(model: model)
            .modifier(AccountFormScale())
        case .deepl:
        if let feedback = model.deepLKeyFeedback { KeySaveFeedbackView(notice: feedback) }
        settingsCard("DeepL 密钥与帮助", icon: "key") {
            if let usage = model.deepLUsage {
                LabeledContent("DeepL 免费额度") {
                    Text("剩余 \(max(0, usage.characterLimit - usage.characterCount)) / \(usage.characterLimit) 字符")
                }
            } else {
                LabeledContent("DeepL 免费额度") { Text(model.hasDeepLKey ? "正在读取" : "尚未设置密钥") }
            }
            Divider()
            DeepLKeyHelp()
            HStack {
                Button(model.hasDeepLKey ? "更新 DeepL 密钥" : "填写 DeepL 密钥") {
                    keyButtonHighlighted = true
                    showDeepLSettings = true
                }
                .buttonStyle(SettingsFeedbackButtonStyle(highlighted: keyButtonHighlighted))
                Button("使用免密钥的 Apple 翻译") {
                    appleButtonHighlighted = true
                }
                    .buttonStyle(SettingsFeedbackButtonStyle(highlighted: appleButtonHighlighted))
                    .disabled(ProcessInfo.processInfo.operatingSystemVersion.majorVersion < 15)
            }
        }
        case .speech:
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
        case .permissions:
        settingsCard("权限状态", icon: "lock.shield") {
            permissionRow("语音识别", status: model.speechRecognitionPermissionText) {
                NoticeAction.openSpeechRecognitionSettings.open()
            }
            permissionRow("麦克风", status: model.microphonePermissionText) {
                NoticeAction.openMicrophoneSettings.open()
            }
        }
        case .shortcuts:
            SelectionShortcutSettings(model: model)
        case .glossary:
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
        case .about:
        settingsCard("关于与更新", icon: "info.circle") {
            LabeledContent("当前版本") { Text(versionText) }
            LabeledContent("开发者") { Text("本工具由null团队打造") }
            Text("本次更新：全新分类设置、DeepL 密钥快捷帮助、中英文自动互译、逐字显示，以及菜单栏截图与语音悬浮翻译。")
                .font(.system(size: 12))
                .foregroundStyle(Color.secondary)
                .fixedSize(horizontal: false, vertical: true)
        }
        settingsCard("退出应用", icon: "power") {
            HStack {
                Text("关闭所有 Yike 窗口，停止菜单栏与全局快捷键。")
                    .font(.system(size: 12)).foregroundStyle(.secondary)
                Spacer()
                Button("退出 Yike") {
                    dismiss()
                    DispatchQueue.main.asyncAfter(deadline: .now() + 0.25) {
                        NSApp.terminate(nil)
                    }
                }
                    .accessibilityIdentifier("settings.quit")
                    .buttonStyle(.bordered)
            }
        }
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

private struct SettingsFeedbackButtonStyle: ButtonStyle {
    @Environment(\.isEnabled) private var isEnabled
    var highlighted: Bool

    func makeBody(configuration: Configuration) -> some View {
        let active = isEnabled && (highlighted || configuration.isPressed)
        configuration.label
            .font(.system(size: 12, weight: .medium))
            .padding(.horizontal, 12)
            .padding(.vertical, 8)
            .foregroundStyle(active ? Color.blue : Color.primary)
            .background(active ? Color.blue.opacity(0.15) : Color.primary.opacity(0.04))
            .clipShape(RoundedRectangle(cornerRadius: 7))
            .overlay {
                RoundedRectangle(cornerRadius: 7)
                    .strokeBorder(active ? Color.blue.opacity(0.5) : Color.primary.opacity(0.12), lineWidth: 1)
            }
            .contentShape(RoundedRectangle(cornerRadius: 7))
            .opacity(isEnabled ? 1 : 0.45)
    }
}
