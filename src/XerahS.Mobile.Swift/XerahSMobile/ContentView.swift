//
//  ContentView.swift
//  XerahS Mobile (Swift)
//
//  Copyright (c) 2007-2026 ShareX Team.
//

import SwiftUI

struct ContentView: View {
    var body: some View {
        VStack {
            Text("XerahS")
                .font(.largeTitle)
            Text("Share & Upload")
                .font(.subheadline)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }
}

#Preview {
    ContentView()
}
