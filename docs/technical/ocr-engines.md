# OCR engines

XerahS reads text from images with one of three engines, behind one interface (`IOcrService` in
`XerahS.Platform.Abstractions`).

| Engine | Id | Where it comes from | Reads |
|---|---|---|---|
| PaddleOCR | `paddleocr` | Bundled with the app (`XerahS.OCR`): PP-OCRv6 small detection and recognition models, run with ONNX Runtime | Latin, Greek, Chinese and Japanese text, in one pass with no language to choose |
| System | `system` | Windows: Windows.Media.Ocr. Linux: the `tesseract` program, if installed. macOS: none | The languages installed for that engine |
| AI Assistant | `ai` | The user's own AI provider (`XerahS.Assistant/Ai/AiOcrEngine`). Listed only when the "OCR: AI Assistant engine" feature is on and the provider's model reads images | Any language or script the model reads, with no language to choose. Returns text only, no word boxes |

The user picks the engine in the OCR window. Each engine reports an `OcrEngineInfo` with `IsRemote`; the AI Assistant
is the one remote engine, because the image leaves the computer and the request may be billed. The choice is saved as `OCROptions.Engine` in the default task
settings, so After Capture OCR, the Assistant and the history text index use the same one.

## Automatic

`auto` is the default. PaddleOCR reads the image unless both of these hold:

- the chosen language is written in a script its model does not know (Cyrillic, Korean, Arabic, Hebrew, Thai and
  others), and
- the system engine has that language.

The check is `PaddleOcrEngine.CanRead` on the language's own name ("русский"), against the character list in the
recognition model's `inference.yml`. It follows the model, so replacing the model needs no change to a language table.

Automatic never picks the AI Assistant. It is used only when its id (`ai`) is asked for.

## AI Assistant and `LocalOnly`

`OcrOptions.LocalOnly` keeps the image on this computer. When it is set, a remote engine is not used, even if
`OcrOptions.Engine` names it, and the request runs on the default local engine instead. Work the user did not start
for that image sets it:

- `OcrIndexingService` (the history text index) sets `LocalOnly = true`.
- `PrivacyScanService.AddLocalEvidenceAsync` sets `LocalOnly = true`.

Any new background or automatic use of OCR must do the same. The AI Assistant also needs the provider and key from
Settings > Assistant. See [AI framework](ai-framework.md).

## How the pieces fit

- `XerahS.OCR/PaddleOcrEngine` is the engine, ported from `ShareX.OCR` in ShareX. Keep it close to that source so
  later fixes there can be carried over. It loads the models once per process, on first use.
- `XerahS.OCR/OcrEngineService` (renamed from `PaddleOcrService`) implements `IOcrService` and `IOcrEngineHost`. It
  wraps the platform's service and routes each request.
- Other parts of the app add engines through `IOcrEngineHost.AddEngine(IOcrEngine)`. An `IOcrEngine` gives its
  `OcrEngineInfo`, `IsAvailable`, languages and `RecognizeAsync`. An engine that is not available is not listed. If
  neither PaddleOCR nor the system engine works on a platform, the first available added engine is the default.
- `XerahS.Assistant/Ai/AiIntegrations.Register()` adds `AiOcrEngine`. `Program.cs` calls it right after
  `OcrEngineService.Register()`.
- `Program.cs` in `XerahS.App` calls `OcrEngineService.Register()` after the platform has registered its own service.
  Only the desktop app does this. The CLI, the watch-folder daemon and the MCP server keep the system engine, so
  they do not each carry a copy of the ONNX runtime.
- The models are in `XerahS.OCR/Models` (31 MB) and are copied to `PaddleOCR/` beside the app.

## Limits

- The AI Assistant engine returns text only. `OcrResult.Words` is empty, so it cannot supply geometry.
- PaddleOCR reports a box per piece of text, not per word. `OcrResult.Words` has one entry per piece, grouped into
  lines by `LineId`. The privacy scan redacts whole lines, so this is enough for it.
- `OcrOptions.ScaleFactor` does not apply to PaddleOCR; the detector sizes the image itself.
- ONNX Runtime has no Intel Mac (osx-x64) build. PaddleOCR is not offered there, and macOS has no system engine yet.
- A full 1920 x 2150 screenshot with about 180 lines of text took around 12 seconds on a 2020 iMac CPU. Small
  regions take well under a second after the first run.

## Licences

The PP-OCRv6 models are from PaddleOCR, Apache License 2.0 (`XerahS.OCR/Models/LICENSE.txt`). ONNX Runtime is MIT.
