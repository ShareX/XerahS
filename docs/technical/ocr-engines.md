# OCR engines

XerahS reads text from images with one of two engines, behind one interface (`IOcrService` in
`XerahS.Platform.Abstractions`).

| Engine | Id | Where it comes from | Reads |
|---|---|---|---|
| PaddleOCR | `paddleocr` | Bundled with the app (`XerahS.OCR`): PP-OCRv6 small detection and recognition models, run with ONNX Runtime | Latin, Greek, Chinese and Japanese text, in one pass with no language to choose |
| System | `system` | Windows: Windows.Media.Ocr. Linux: the `tesseract` program, if installed. macOS: none | The languages installed for that engine |

The user picks the engine in the OCR window. The choice is saved as `OCROptions.Engine` in the default task
settings, so After Capture OCR, the Assistant and the history text index use the same one.

## Automatic

`auto` is the default. PaddleOCR reads the image unless both of these hold:

- the chosen language is written in a script its model does not know (Cyrillic, Korean, Arabic, Hebrew, Thai and
  others), and
- the system engine has that language.

The check is `PaddleOcrEngine.CanRead` on the language's own name ("русский"), against the character list in the
recognition model's `inference.yml`. It follows the model, so replacing the model needs no change to a language table.

## How the pieces fit

- `XerahS.OCR/PaddleOcrEngine` is the engine, ported from `ShareX.OCR` in ShareX. Keep it close to that source so
  later fixes there can be carried over. It loads the models once per process, on first use.
- `XerahS.OCR/PaddleOcrService` implements `IOcrService`. It wraps the platform's service and routes each request.
- `Program.cs` in `XerahS.App` calls `PaddleOcrService.Register()` after the platform has registered its own service.
  Only the desktop app does this. The CLI, the watch-folder daemon and the MCP server keep the system engine, so
  they do not each carry a copy of the ONNX runtime.
- The models are in `XerahS.OCR/Models` (31 MB) and are copied to `PaddleOCR/` beside the app.

## Limits

- PaddleOCR reports a box per piece of text, not per word. `OcrResult.Words` has one entry per piece, grouped into
  lines by `LineId`. The privacy scan redacts whole lines, so this is enough for it.
- `OcrOptions.ScaleFactor` does not apply to PaddleOCR; the detector sizes the image itself.
- ONNX Runtime has no Intel Mac (osx-x64) build. PaddleOCR is not offered there, and macOS has no system engine yet.
- A full 1920 x 2150 screenshot with about 180 lines of text took around 12 seconds on a 2020 iMac CPU. Small
  regions take well under a second after the first run.

## Licences

The PP-OCRv6 models are from PaddleOCR, Apache License 2.0 (`XerahS.OCR/Models/LICENSE.txt`). ONNX Runtime is MIT.
