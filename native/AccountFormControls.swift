import SwiftUI

/// Keep the account form proportional inside the standard settings window.
struct AccountFormScale: ViewModifier {
    @State private var contentHeight: CGFloat = 560
    private let scale: CGFloat = 0.8
    func body(content: Content) -> some View {
        content
            .frame(width: 520)
            .fixedSize(horizontal: false, vertical: true)
            .background(GeometryReader { geometry in
                Color.clear
                    .onAppear { contentHeight = geometry.size.height }
                    .onChange(of: geometry.size.height) { contentHeight = $0 }
            })
            .scaleEffect(scale, anchor: .top)
            .frame(width: 520 * scale, height: contentHeight * scale, alignment: .top)
            .frame(maxWidth: .infinity, alignment: .top)
    }
}

struct QuotaUsageView: View {
    let title: String
    let used: Int
    let total: Int
    private var fraction: Double { min(1, max(0, Double(used) / Double(max(1, total)))) }
    var body: some View {
        VStack(alignment: .leading, spacing: 9) {
            HStack {
                Text(title).fontWeight(.medium)
                Spacer()
                Text("\(max(0, used).formatted()) / \(max(0, total).formatted()) 字符")
                    .foregroundStyle(.secondary).monospacedDigit()
            }.font(.system(size: 11))
            HStack(spacing: 12) {
                ProgressView(value: fraction).tint(fraction >= 0.9 ? .red : .accentColor)
                Text("已用 \((fraction * 100).formatted(.number.precision(.fractionLength(1))))%")
                    .font(.system(size: 11)).foregroundStyle(.secondary).monospacedDigit().fixedSize()
            }
        }.accessibilityElement(children: .combine)
    }
}

struct AccountFormInput: View {
    let title: String
    let hint: String
    let symbol: String
    @Binding var text: String
    var secure = false
    @State private var revealed = false
    @FocusState private var focused: Bool
    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text(title).font(.system(size: 12, weight: .medium)).foregroundStyle(.secondary)
            HStack(spacing: 12) {
                Image(systemName: symbol).font(.system(size: 17)).foregroundStyle(Color.accentColor.opacity(0.65)).frame(width: 21)
                Group {
                    if secure && !revealed { SecureField(hint, text: $text) }
                    else { TextField(hint, text: $text) }
                }.textFieldStyle(.plain).font(.system(size: 14)).focused($focused)
                    .accessibilityLabel(title)
                if !text.isEmpty {
                    Button { text = ""; focused = true } label: {
                        Image(systemName: "xmark.circle.fill").foregroundStyle(.tertiary)
                    }.buttonStyle(.plain).accessibilityLabel("清空" + title)
                }
                if secure {
                    Button { revealed.toggle(); focused = true } label: {
                        Image(systemName: revealed ? "eye.slash" : "eye").font(.system(size: 15)).foregroundStyle(.secondary)
                    }.buttonStyle(.plain).accessibilityLabel(revealed ? "隐藏密码" : "显示密码")
                }
            }.padding(.horizontal, 15).frame(height: 46)
                .background(Color(nsColor: .textBackgroundColor).opacity(0.55), in: RoundedRectangle(cornerRadius: 11))
                .overlay(RoundedRectangle(cornerRadius: 11).stroke(focused ? Color.accentColor.opacity(0.65) : Color.primary.opacity(0.10), lineWidth: 1))
                .shadow(color: focused ? Color.accentColor.opacity(0.08) : .clear, radius: 3)
        }
    }
}

struct AccountPrimaryButton: ButtonStyle {
    @Environment(\.isEnabled) private var enabled
    func makeBody(configuration: Configuration) -> some View {
        HStack(spacing: 14) {
            configuration.label
            Image(systemName: "arrow.right").font(.system(size: 15, weight: .medium))
        }.font(.system(size: 14, weight: .semibold)).foregroundStyle(.white)
            .frame(maxWidth: .infinity).frame(height: 44)
            .background(LinearGradient(colors: [Color(red: 0.25, green: 0.39, blue: 0.87), Color(red: 0.20, green: 0.31, blue: 0.72)], startPoint: .leading, endPoint: .trailing), in: RoundedRectangle(cornerRadius: 11))
            .opacity(enabled ? (configuration.isPressed ? 0.8 : 1) : 0.40)
    }
}
