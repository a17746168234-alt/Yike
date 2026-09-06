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

struct SettingsSheet: View {
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
                        Text("本次更新：OCR 候选检查、方向纠正、补框与拆分合并；长译文自动缩小字号；Apple 语言包准备状态与更明确的翻译错误提示。")
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
