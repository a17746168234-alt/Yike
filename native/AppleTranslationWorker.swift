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

@available(macOS 15.0, *)
struct AppleTranslationWorker: View {
    @ObservedObject var model: TranslatorViewModel
    @State private var configuration: TranslationSession.Configuration?
    @State private var activeTextRequest: AppleTranslationRequest?
    @State private var activeImageRequest: AppleImageTranslationRequest?

    var body: some View {
        Color.clear
            .frame(width: 1, height: 1)
            .onChange(of: model.appleTranslationRequest) { request in
                if request == nil {
                    activeTextRequest = nil
                    if model.appleImageTranslationRequest == nil { configuration = nil }
                    return
                }
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
                if request == nil {
                    activeImageRequest = nil
                    if model.appleTranslationRequest == nil { configuration = nil }
                    return
                }
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
