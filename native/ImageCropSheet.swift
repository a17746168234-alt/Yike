import SwiftUI
import AppKit
import ImageIO

struct ImageCropItem: Identifiable {
    let id = UUID()
    let image: CGImage
    init?(url: URL) {
        guard let source = CGImageSourceCreateWithURL(url as CFURL, nil),
              let properties = CGImageSourceCopyPropertiesAtIndex(source, 0, nil) as? [CFString: Any] else { return nil }
        let dimension = max(properties[kCGImagePropertyPixelWidth] as? Int ?? 1, properties[kCGImagePropertyPixelHeight] as? Int ?? 1)
        guard let image = CGImageSourceCreateThumbnailAtIndex(source, 0, [
            kCGImageSourceCreateThumbnailFromImageAlways: true,
            kCGImageSourceCreateThumbnailWithTransform: true,
            kCGImageSourceThumbnailMaxPixelSize: dimension
        ] as CFDictionary) else { return nil }
        self.image = image
    }
}

struct ImageCropSheet: View {
    let item: ImageCropItem
    let cancel: () -> Void
    let confirm: (CGImage) -> Void
    @State private var selection: CGRect?
    private var size: CGSize { CGSize(width: item.image.width, height: item.image.height) }
    var body: some View {
        VStack(spacing: 16) {
            HStack {
                VStack(alignment: .leading, spacing: 5) {
                    Text("裁剪翻译区域").font(.title2.bold())
                    Text("拖动框选要翻译的内容，减少无关文字。也可以直接使用整张图片。")
                        .font(.callout).foregroundStyle(.secondary)
                }
                Spacer()
            }
            GeometryReader { geometry in
                let fitted = aspectFitRect(imageSize: size, in: geometry.size)
                ZStack(alignment: .topLeading) {
                    Color.black.opacity(0.08)
                    Image(nsImage: NSImage(cgImage: item.image, size: size))
                        .resizable().frame(width: fitted.width, height: fitted.height)
                        .position(x: fitted.midX, y: fitted.midY)
                    if let selection {
                        Rectangle().fill(Color.accentColor.opacity(0.12))
                            .overlay(Rectangle().stroke(Color.accentColor, lineWidth: 2))
                            .frame(width: selection.width * fitted.width, height: selection.height * fitted.height)
                            .position(x: fitted.minX + selection.midX * fitted.width, y: fitted.minY + selection.midY * fitted.height)
                    }
                }
                .contentShape(Rectangle())
                .gesture(DragGesture(minimumDistance: 3).onChanged { value in
                    func normalized(_ point: CGPoint) -> CGPoint {
                        CGPoint(x: min(1, max(0, (point.x - fitted.minX) / fitted.width)),
                                y: min(1, max(0, (point.y - fitted.minY) / fitted.height)))
                    }
                    let start = normalized(value.startLocation), end = normalized(value.location)
                    selection = CGRect(x: min(start.x, end.x), y: min(start.y, end.y), width: abs(end.x-start.x), height: abs(end.y-start.y))
                })
            }
            HStack {
                Button("取消", action: cancel).keyboardShortcut(.cancelAction)
                Button("重新框选") { selection = nil }.disabled(selection == nil)
                Spacer()
                Button("使用整张图片") { confirm(item.image) }
                Button("裁剪并识别") {
                    guard let selection else { return }
                    let rect = CGRect(x: selection.minX * size.width, y: selection.minY * size.height,
                                      width: selection.width * size.width, height: selection.height * size.height).integral
                    if let cropped = item.image.cropping(to: rect) { confirm(cropped) }
                }
                .buttonStyle(.borderedProminent)
                .disabled((selection?.width ?? 0) * size.width < 8 || (selection?.height ?? 0) * size.height < 8)
            }.buttonStyle(.bordered)
        }.padding(24).frame(width: 780, height: 600)
    }
}
