import Foundation

private struct TestFailure: Error, CustomStringConvertible {
    let description: String
}

private func expect(_ condition: @autoclosure () -> Bool, _ message: String) throws {
    guard condition() else { throw TestFailure(description: message) }
}

@main
struct TranslationCoreTests {
    static func main() throws {
        try testTextChunking()
        try testRequestGateRejectsStaleResults()
        try testImageZoomPolicy()
        try testKeychainRoundTrip()
        print("TranslationCoreTests: 4 tests passed")
    }

    private static func testTextChunking() throws {
        let chunks = textChunks("第一段。第二段比较长。第三段", maximumLength: 8)
        try expect(!chunks.isEmpty, "文本分段不应为空")
        try expect(chunks.allSatisfy { $0.count <= 8 }, "每段都不应超过上限")
        try expect(textChunks("   ", maximumLength: 8).isEmpty, "空白文本不应产生分段")
    }

    private static func testRequestGateRejectsStaleResults() throws {
        var gate = TranslationRequestGate()
        let first = gate.begin()
        let second = gate.begin()
        try expect(!gate.accepts(first), "新请求开始后必须拒绝旧结果")
        try expect(gate.accepts(second), "必须接受当前请求结果")
        gate.cancel()
        try expect(!gate.accepts(second), "取消后必须拒绝在途结果")
    }

    private static func testImageZoomPolicy() throws {
        try expect(
            ImageZoomPolicy.shouldZoom(hasPreciseScrollingDeltas: false),
            "普通鼠标滚轮应调整图片大小"
        )
        try expect(
            !ImageZoomPolicy.shouldZoom(hasPreciseScrollingDeltas: true),
            "触控板双指事件应交给图片画布移动"
        )

        let enlarged = ImageZoomPolicy.adjustedZoom(
            current: 1,
            deviceDeltaY: 1
        )
        try expect(enlarged == 1.1, "滚轮向上应放大图片")

        let reduced = ImageZoomPolicy.adjustedZoom(
            current: enlarged,
            deviceDeltaY: -1
        )
        try expect(reduced == 1, "滚轮向下应缩小图片")

        let maximum = ImageZoomPolicy.adjustedZoom(
            current: 3,
            deviceDeltaY: 10
        )
        try expect(maximum == 3, "缩放不应超过 300%")

        let minimum = ImageZoomPolicy.adjustedZoom(
            current: 1,
            deviceDeltaY: -10
        )
        try expect(minimum == 1, "缩放不应低于 100%")
    }

    private static func testKeychainRoundTrip() throws {
        let store = KeychainTextStore(
            service: "com.yijian.translator.tests.\(UUID().uuidString)",
            account: "round-trip"
        )
        defer { try? store.delete() }
        try store.save("test-secret")
        let firstValue = try store.load()
        try expect(firstValue == "test-secret", "钥匙串应能读回保存值")
        try store.save("updated-secret")
        let updatedValue = try store.load()
        try expect(updatedValue == "updated-secret", "钥匙串应能更新已有值")
        try store.delete()
        let deletedValue = try store.load()
        try expect(deletedValue == nil, "删除后钥匙串不应再返回值")
    }
}
