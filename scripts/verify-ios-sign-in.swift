import Foundation
import Vision

guard CommandLine.arguments.count == 2 else {
    print("Usage: verify-ios-sign-in SCREENSHOT")
    exit(2)
}

let screenshot = URL(fileURLWithPath: CommandLine.arguments[1])
let request = VNRecognizeTextRequest()
request.recognitionLanguages = ["en-US"]

do {
    try VNImageRequestHandler(url: screenshot, options: [:]).perform([request])
} catch {
    print("Unable to read simulator screenshot: \(error.localizedDescription)")
    exit(2)
}

let visibleLabels = Set((request.results ?? []).compactMap { observation in
    observation.topCandidates(1).first?.string.lowercased()
        .split(whereSeparator: \.isWhitespace).joined(separator: " ")
})
let missingLabels = ["sign in", "username", "password"].filter { !visibleLabels.contains($0) }
guard missingLabels.isEmpty else {
    print("Missing sign-in labels: \(missingLabels.joined(separator: ", "))")
    exit(1)
}

print("Egroo sign-in labels are visible on the iOS simulator.")
