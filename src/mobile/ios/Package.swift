// swift-tools-version: 6.0

// SwiftPM description of XerahSMobile for xtool (Linux/Windows) builds.
// XerahSMobile.xcodeproj remains the source of truth for Xcode and App Store builds;
// keep XtoolSupport/ShareExtensionSources in sync with the ShareExtension Sources build phase.

import PackageDescription

let package = Package(
    name: "XerahSMobile",
    platforms: [.iOS(.v17)],
    products: [
        .library(name: "XerahSMobile", targets: ["XerahSMobile"]),
        .library(name: "ShareExtension", targets: ["ShareExtension"]),
    ],
    targets: [
        .target(
            name: "XerahSMobile",
            path: "XerahSMobile",
            exclude: [
                "Info.plist",
                "XerahSMobile.entitlements",
                "PrivacyInfo.xcprivacy",
                "Assets.xcassets",
            ],
            swiftSettings: [.swiftLanguageMode(.v5)]
        ),
        .target(
            name: "ShareExtension",
            // Symlinks to ShareExtension/ plus the app files the extension also compiles:
            // SwiftPM does not allow a file in two targets.
            path: "XtoolSupport/ShareExtensionSources",
            swiftSettings: [.swiftLanguageMode(.v5)],
            linkerSettings: [
                // iOS loads NSExtensionPrincipalClass by name and the extension links with -dead_strip,
                // so keep ShareViewController (and everything it uses) alive explicitly.
                .unsafeFlags(["-Xlinker", "-u", "-Xlinker", "_OBJC_CLASS_$__TtC14ShareExtension19ShareViewController"]),
            ]
        ),
    ]
)
