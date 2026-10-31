# Privacy mode before image upload

Enable **Privacy mode: scan and redact before upload** in a workflow's After Capture settings, or in the After Capture window. The checkbox keeps its preference when upload is off. Copy/save-only captures do not run privacy scanning or call AI. Existing-image upload jobs use their effective upload workflow's privacy setting.

When an image upload is active:

1. XerahS identifies the active Assistant provider and asks permission to send this unredacted screenshot to its configured endpoint. Cloud analysis can expose the original information to that provider and may incur API usage charges. Declining uses local manual review.
2. A compatible vision model looks for names, emails, phones, addresses/location, identity documents, payment/bank details, credentials/recovery material, medical/private communications, customer records, device/account identifiers, and encoded/confidential data. Local OCR patterns and QR detection add candidates when available.
3. A local preview applies blur by default. Choose **Pixelate** or **Mask**, or **Annotate / review manually** to add/edit redactions in the existing editor. Mask is the stronger choice for credentials, small text and payment details. AI and blur are risk reduction, not guarantees.
4. Choose **Approve image and upload**, **Keep locally**, or **Cancel**. No destination receives pixels without approval. An unavailable/incomplete scan requires a completed manual editor review before the Upload button enables. Closing either dialog does not approve an upload.

The upload is a new flattened PNG with a random `private-…` filename. Imported originals are preserved. Source layers, annotation sidecars, original window/process titles, OCR text and image metadata are excluded from this derivative. Save/copy/history operations in an uploading capture use the derivative; subsequent OCR runs on its pixels. A changed image/file, policy, AI connection or destination invalidates approval. A fallback destination needs its own approval, without repeating the cloud scan.

The active Assistant connection currently uses configured API credentials for OpenAI Responses, Kimi-compatible Chat Completions, Gemini or Anthropic vision models. A chat subscription by itself does not supply API credentials. No browser credentials or subscription sessions are reused. MiniMax/Ollama catalog entries currently lack vision support and fall back to manual review. Provider/model errors also require manual review.

Local OCR geometry is available through Tesseract on Linux and the native Windows OCR engine for unrotated text. Unsupported languages, rotated Windows OCR, missing engines and missing geometry are disclosed as limitations. Optional local rules use .NET regex syntax, one per line (up to 16 rules, 500 characters each, with execution time limits); invalid rules require manual review. These rules are not sent to the AI provider. Always inspect the whole preview, including faces, QR codes, wrapped text and small text.

Version 1 covers still raster images. Animated/multipage files are blocked from protected image upload. Videos, PDFs and archives are outside this checkbox's scope. Headless CLI/MCP/background hosts without a review service report **Review required** and do not upload. Use the tray menu’s **Review pending uploads** action to resume queued items in XerahS. Their source files are retained while review is pending. Restart/retry does not preserve an old approval.
