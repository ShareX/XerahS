//
//  LoadingScreen.swift
//  XerahS Mobile (Swift)
//
//  Copyright (c) 2007-2026 ShareX Team.
//

import SwiftUI

struct LoadingScreen: View {
    var onInitComplete: () -> Void

    var body: some View {
        VStack(spacing: 24) {
            Text("XerahS")
                .font(.largeTitle)
            ProgressView()
                .scaleEffect(1.5)
            Text("Initializing XerahS…")
                .font(.body)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .onAppear {
            DispatchQueue.main.asyncAfter(deadline: .now() + 0.8) {
                onInitComplete()
            }
        }
    }
}
