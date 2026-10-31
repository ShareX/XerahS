# XIP0090 Privacy Mode Sensitive Data Redaction Before Upload

**Status**: Implemented (version 1; capability limits below)

**Created**: 2026-10-13  
**Updated**: 2026-10-13  
**Area**: Desktop | Core | AI Integration | Image Editor | Uploaders | Privacy  
**Goal**: Add an upload-dependent Privacy mode checkbox that uses the active AI connection to identify sensitive screenshot content, automatically blur selected regions, and offer review and annotation before sharing.  
**Related**: [XIP0071 Spotlight Assistant](XIP0071-xerahs-spotlight-assistant.md), [XIP0074 AfterCapture OCR](XIP0074-aftercapture-ocr-integration.md), [XIP0068 Re-Editing Saved Annotations](XIP0068-re-editing-saved-annotations.md)  
**Publication**: Local draft requested by the user; no GitHub issue created. Reconcile the provisional number before publication and preserve this draft before running GitHub-to-local sync.

---

## Version 1 implementation

The upload checkbox, active-provider vision requests, local OCR/QR evidence, automatic redaction, preview/editor review, flattened derivatives and guards for both capture and file uploads are implemented. See [Privacy mode usage](../../privacy-mode.md).

The shared `RegionRedactionRenderer` lives in `XerahS.Core/Services/Privacy` and delegates blur to the existing ImageEditor `BlurImageEffect`. MCP region annotations reuse it. This keeps the service reusable without changing the ImageEditor submodule revision or adding a dependency from Core to the MCP executable.

Version 1 uses the active Assistant API connection, with explicit per-scan consent. It does not add subscription OAuth or infer API access from a chat subscription. Linux Tesseract and unrotated Windows OCR expose optional word geometry; missing OCR geometry is shown as a limitation. Local encoded-data detection covers QR codes; other barcode formats depend on AI/manual review. Oversized, animated or multipage inputs are blocked. The automatic renderer validates normalized bounds in the original image; it does not silently downscale or claim precision from tiled transforms. Unknown/incomplete vision output requires manual review.

Approval is transient and tied separately to final encoded bytes, decoded preview pixels, privacy policy, provider/model/endpoint/credentials and destination settings. Plugins consume a private read-only copy of approved bytes. Destination fallback prompts for fresh approval. Imported originals are preserved; capture source/sidecar retention is deliberately omitted in this version so uploading captures save only flattened derivatives. Headless jobs surface Review required and remain blocked rather than persisting approval.

The sections below retain the broader design and acceptance goals for future extensions. Account-specific subscription integration, non-QR barcode geometry, transformed/tiled analysis and optional original retention need their own verified capabilities before activation.

## Overview

Screenshots can expose personal details and credentials in documents, messages, browser tabs, account pages, terminals and application chrome. Add **Privacy mode — scan and blur sensitive data before upload** next to **Upload image to host** in workflow settings and the After Capture window. When both privacy mode and an actual image upload are active, pause sharing, analyze the final screenshot, prepare automatic redactions, and show a warning with a preview and an **Annotate / review** action before any destination receives the image.

The active AI subscription means the user's AI connection selected in XerahS, using its supported authentication and model capabilities. Reuse that connection and its credential store; do not introduce a separate privacy-provider account. At draft time, Assistant adapters used API keys and text requests, so working image analysis and any subscription-backed connection support are explicit implementation work, not capabilities implied by the checkbox.

Automatic region blur is feasible with the existing code. The missing pieces are a shared renderer API, sensitivity detection with trustworthy coordinates, a review UI, and a gate covering both capture uploads and existing-file uploads. Keep analysis, local bitmap modification and destination upload separate. Cloud analysis itself sends content to the selected AI provider and needs informed consent before that transmission; masking afterward cannot undo that disclosure. Detection and blur reduce exposure but cannot guarantee that every secret is found or that blurred content cannot be inferred.

## User experience and activation rules

### Checkbox and settings

- Add `PrivacyModeEnabled`, default `false`, to a dedicated privacy settings object on `TaskSettings`, with default-task settings and per-workflow overrides following existing settings patterns.
- Disable the checkbox when **Upload image to host** is off, retaining its stored preference. Help text: **Runs only when this workflow uploads a still image. Uses your active AI connection; review before sharing.**
- Re-evaluate upload intent after After Capture choices and quick actions. Turning upload off, choosing copy/save only, or cancelling must prevent AI calls, scanning and automatic blur, even if privacy mode was previously checked.
- For a direct upload of an existing image, upload intent comes from the actual upload task; it does not depend on an after-capture flag being present. Apply the effective default/workflow privacy policy to these tasks too.
- Resolve provider, model, endpoint and policy once per scan. Show the selected connection in the UI. A provider or policy change invalidates any pending scan consent and approval.
- Version 1 covers still screenshot images and imported still images, including images routed through a file uploader. Video, audio, PDFs and archives are outside this feature; label that scope clearly. Animated or multipage image inputs must be identified and blocked from a privacy-protected image upload until all frames/pages are supported, rather than scanning only the first frame.

| Effective state | Required behavior |
|---|---|
| Privacy off | Existing workflow behavior; no privacy scan. |
| Privacy on, upload off | No AI request, no privacy prompt, no automatic blur. |
| Privacy on, upload on, supported still image | Analyze, redact, review, then upload only the approved image. |
| Provider unavailable, scan incomplete, or review cancelled | Upload stays paused; offer annotation or local-only handling. |
| No sensitive regions detected | Show review with **No sensitive content detected; detection can miss details**, plus annotation and upload actions. |

### Review before sharing

1. Obtain cloud-image consent through the existing privacy guard before sending pixels or extracted sensitive text. Identify the provider and endpoint and explain that the image may contain unredacted content. A local-only model avoids cloud transmission; determine locality from the actual endpoint and adapter, not the provider's display name.
2. Show progress with cancel support. Do not start a destination upload in parallel with the scan.
3. Prepare an automatically blurred preview with numbered findings, category and confidence. Display category/count summaries, not copied secrets, in the warning: **Sensitive content found: 2 email addresses, 1 name, 1 payment detail. Review the redactions before uploading.**
4. Offer **Annotate / review**, **Upload redacted image**, **Keep local only**, and **Cancel**. The default focus is review. No countdown, timeout or closing the window may approve sharing.
5. Annotation opens the existing editor with the proposed regions selected or preloaded. Users can add regions, expand masks, correct false positives, or choose a stronger effect. Saving returns a new flattened candidate to the review; cancelling the editor returns to review without uploading.
6. **Keep local only** removes upload intent for this run and allows the normal local tasks to continue without further AI calls. Do not apply an unaccepted automatic preview to the local image; retain any edits the user already accepted. **Cancel** aborts the workflow. Once redactions have been accepted, save, clipboard and history previews use the accepted image.
7. If analysis is unavailable, **Annotate manually** can produce a flattened image for explicit approval as **Manually reviewed; AI scan unavailable**. It must not mark an error as a clean AI scan or silently upload the original.

Cloud consent and destination approval are distinct. Turning on the checkbox alone must not bypass `AssistantPrivacyGuard`'s image-sharing confirmation. Any future remembered consent must be an explicit, separately scoped preference for a provider/endpoint and data scope, never consent to uploading an unreviewed screenshot.

## Sensitive content coverage

Provide configurable categories, enabled by default for common personal data, payment details and credentials. Custom phrases and local patterns supplement AI analysis. These are a broad baseline, not a promise of exhaustive detection.

| Category | Examples to detect and offer to redact |
|---|---|
| Identity and contact | Real names, email addresses, phone/fax numbers, contact cards, identifying usernames/handles, account avatars and faces. |
| Location | Street and postal addresses, apartment numbers, postcode/ZIP, GPS coordinates, map pins, live location and identifying address-bar/map text. |
| Government and identity documents | Passport/driver licence/national ID numbers, tax identifiers, social security numbers, Medicare or equivalent identifiers, document photos, dates of birth and signatures. |
| Cards and banking | Cardholder names, full/partial card numbers where identifying, expiry dates, CVV, bank account numbers, IBAN, BSB, routing/sort codes and payment screenshots. |
| Credentials and recovery | Passwords, PINs, one-time codes, recovery codes, API keys, bearer/OAuth tokens, JWTs, cookies, session IDs, private keys and cryptocurrency seed phrases. |
| Medical and insurance | Patient names/IDs, diagnoses, prescriptions, lab results, appointments, disability information, insurance membership/policy/claim details. |
| Personal and household | Family relationships, personal photos, vehicle registration plates, identifying schedules, home/access/alarm codes, and identifiable information about religion, political views, sexual orientation or union membership. |
| Employment, education and legal | Payslips, salary, employee/student IDs, grades, information about minors, HR records, legal case numbers and identifiable confidential records. |
| Private communication | Chat and email participants, subjects, message contents, contact lists, private notifications and confidential attachment previews. |
| Business and customer data | Customer/client names, account/order/invoice numbers, confidential project/repository names, unreleased financials, private URLs and internal dashboards. |
| Devices and infrastructure | Identifying home-directory paths, IP/MAC addresses, hostnames, device serials/IMEI, Wi-Fi names/passwords and network settings, according to the selected policy. |
| Encoded and access-bearing content | QR codes/barcodes containing contact data, login/2FA setup, tickets/boarding passes, payment data, Wi-Fi credentials, tracking IDs or signed/token-bearing links. |
| Custom sensitive content | User-defined names, organizations, domains, phrases, regular expressions and document/region patterns. |

Detection must handle international formats, Unicode names, multilingual text, wrapped lines and OCR mistakes. Use contextual classification for ambiguous names, dates and numbers. Local checks such as email patterns and card checksums provide evidence, not permission to declare unmatched data safe. Validate custom regular expressions with bounded execution time. Decode QR/barcodes locally when supported; never follow their links. Offer whole-code masking when a sensitive code cannot be interpreted reliably.

Blur is the requested default effect. Also offer pixelation and opaque masking, with opaque masking recommended for credentials, recovery material, card security codes and small text where blur may still expose information. Explain this in the effect picker without promising irreversible secrecy from blur or pixelation.

## Existing infrastructure and feasibility findings

Findings below preserve the pre-implementation feasibility review. See the version 1 implementation above for current behavior.

| Existing component | What is reusable and what is missing |
|---|---|
| [SkiaAnnotationRenderer](../../../src/tools/XerahS.McpServer/Runtime/SkiaAnnotationRenderer.cs) | `ApplyAnnotations` handles `blur` and `pixelate` directly on an `SKBitmap`; `ApplyBlur` clamps a region and uses Skia's blur filter. Automatic pixel processing already exists, but this internal MCP implementation is not a shared privacy service. |
| [MCP annotation runtime](../../../src/tools/XerahS.McpServer/Runtime/XerahSMcpRuntime.cs) | `AnnotateImageAsync` decodes an image, applies annotations and saves a new bitmap. Reuse the local processing approach, not a nested MCP call or a new upload route. |
| [BlurAnnotation](../../../ShareX.ImageEditor/src/ShareX.ImageEditor/Core/Annotations/Effects/BlurAnnotation.cs) and [BlurImageEffect](../../../ShareX.ImageEditor/src/ShareX.ImageEditor/Core/ImageEffects/Filters/BlurImageEffect.cs) | Existing editor effects and edge-padding behavior support interactive review and shared local rendering. A whole-image blur preset alone cannot target detected sensitive regions. |
| [EditorCore](../../../ShareX.ImageEditor/src/ShareX.ImageEditor/Core/Editor/EditorCore.cs) | `Render` and `GetSnapshot` draw/copy the source without annotations. They are not safe flattening APIs for privacy output. Use the editor session's rendered result or the shared bitmap renderer. |
| [IUIService](../../../src/platform/XerahS.Platform.Abstractions/IUIService.cs) and [ImageEditorSessionResult](../../../ShareX.ImageEditor/src/ShareX.ImageEditor/Hosting/ImageEditorSessionResult.cs) | `ShowEditorSessionAsync` accepts initial annotations and returns `RenderedImage`. Reuse this for manual review; `SourceImage` and editable state can retain unredacted pixels. |
| [Assistant settings resolver](../../../src/desktop/app/XerahS.Assistant/Configuration/AssistantProviderSettings.cs) and [privacy guard](../../../src/desktop/app/XerahS.Assistant/Services/AssistantPrivacyGuard.cs) | Active provider selection, credential storage and `CloudImage` consent already exist. Share their policy through an injected adapter, avoiding a Core-to-Assistant dependency cycle. |
| [Assistant contracts](../../../src/desktop/app/XerahS.Assistant/Models/AssistantContracts.cs) and [HTTP adapters](../../../src/desktop/app/XerahS.Assistant/Providers/AssistantModelProviders.cs) | Catalog entries declare image support, but `AssistantMessage` contains text and the request builders serialize text only. `AllowImageContent` does not provide an image payload. Actual multimodal requests, model capability checks and structured region results are required. |
| [IOcrService](../../../src/platform/XerahS.Platform.Abstractions/IOcrService.cs) | Local recognition is reusable, but `OcrResult` currently contains text/status without word or line bounds. Coordinate-aware OCR must be added for reliable token-to-region mapping. |
| [CaptureJobProcessor](../../../src/desktop/core/XerahS.Core/Tasks/Processors/CaptureJobProcessor.cs) and [UploadJobProcessor](../../../src/desktop/core/XerahS.Core/Tasks/Processors/UploadJobProcessor.cs) | Capture processing performs effects/annotation before save, clipboard and direct plugin upload. Upload jobs have a separate dispatch path. Guard both paths with the same privacy service; adding only a pipeline stage or only an upload-processor hook leaves a bypass. |

**Conclusion:** no new blur engine is needed. Make automatic privacy redaction possible by exposing a typed, shared region-rendering API and connecting AI/OCR findings to it before either upload path can dispatch bytes.

## Prerequisites

- Reuse the centrally managed .NET, SkiaSharp and ImageEditor dependencies. Do not add a second imaging stack.
- Enable image analysis only for a configured provider/model whose adapter and authentication actually support it. Extend the existing connection layer for supported subscription-backed access if needed; do not assume a paid chat subscription supplies an API key or reuse unrelated browser/session credentials.
- Keep the manual annotation route available when vision, local OCR or network access is unavailable. Capability labels alone do not establish readiness.

## Implementation phases

### Phase 1: Settings, contracts and effective upload intent

Add `TaskSettingsPrivacy` with enabled state, category policy and redaction settings, plus view-model bindings and help text. Reuse existing settings serialization and task cloning, including `WatchFolderManager.CloneTaskSettings`; do not assume a `TaskSettings.Clone()` method exists.

Define a Core-owned `IPrivacyAnalysisProvider` contract and typed scan/review results. Represent `Complete`, `Incomplete`, `Unsupported`, `Error` and `Cancelled` separately. Findings contain an ID, category, confidence, normalized bounds and optional local OCR token references; raw secret values are unnecessary. Add a transient, task-owned upload approval tied to image bytes, policy version and provider identity. Never serialize it as a permanent approval.

**Key files:** `src/desktop/core/XerahS.Core/Models/TaskSettings.cs`, proposed `Models/TaskSettingsPrivacy.cs` and `Services/Privacy/PrivacyContracts.cs`, `src/desktop/app/XerahS.UI/ViewModels/TaskSettingsViewModel.AfterCapture.cs`, `Views/TaskSettingsPanel.axaml`, and `Views/AfterCaptureWindow.axaml`.

### Phase 2: Local analysis and active AI connection

Implement `PrivacyScanService` in Core and `AssistantPrivacyAnalysisProvider` in Assistant. Inject the adapter from the UI composition root. Core must not reference the Assistant project, whose existing dependency points toward Core. CLI/MCP hosts without a compatible provider or review channel return a pending-review result instead of bypassing protection.

- Extend `OcrResult` with optional word/line geometry, source dimensions and confidence while preserving existing text consumers. Platform engines return geometry mapped back from their OCR scale to original screenshot pixels. Missing geometry is an explicit limitation, not a successful region scan.
- Supplement OCR with bounded local patterns and supported QR/barcode decoding. A local-only detector may create candidates, but does not silently replace the requested AI analysis or call a failed AI scan complete.
- Add typed image attachments to Assistant requests and real image serialization to compatible adapters. Support a bounded region-result schema and enough response capacity for realistic screenshots; the existing short text-response budget is unsuitable.
- Prefer local OCR token IDs for text localization and verified vision bounds for non-text content. Preserve explicit mappings for downscaled, tiled, rotated or letterboxed images so model coordinates cannot mask the wrong location.
- Treat screenshot text as untrusted input. The detector cannot invoke tools, follow visible URLs, execute instructions or choose an upload destination.
- Validate category, dimensions, finite coordinates, confidence and bounds; reject malformed or truncated results. Empty findings mean clean only when the entire requested analysis completed. Retry within bounded limits, then require manual review or local-only handling.

**Key files:** proposed `src/desktop/core/XerahS.Core/Services/Privacy/PrivacyScanService.cs` and `src/desktop/app/XerahS.Assistant/Services/AssistantPrivacyAnalysisProvider.cs`; existing Assistant contracts/adapters/settings/guard; `src/platform/XerahS.Platform.Abstractions/IOcrService.cs` and platform OCR implementations.

### Phase 3: Shared automatic region redaction

Expose a UI-independent `RegionRedactionRenderer` in the ImageEditor library's Core layer, using existing Skia blur/pixelation code and editor effect behavior. Refactor MCP region effects to delegate to it. Core already references ImageEditor; it must not depend on the MCP executable. Shared-library changes follow its own instructions and revision workflow.

The API accepts a bitmap plus typed regions/effects and returns an owned, flattened bitmap. Convert normalized bounds with outward rounding, add a configurable margin for glyph edges and wrapped text, intersect with image bounds, and handle overlapping regions consistently. Reject invalid or empty required regions rather than reporting them as applied. Preserve geometry and pixels outside the redaction area.

Reuse padded/clamped blur processing. Explicitly replace target pixels so transparency or source-over blending cannot leave the original text visible through a blur's edges. Opaque masks must overwrite both color and alpha. Check allocation/filter failures and dispose native resources. Review-only annotation objects must never be mistaken for flattened upload pixels.

**Key files:** proposed `ShareX.ImageEditor/src/ShareX.ImageEditor/Core/Redaction/RegionRedactionRenderer.cs`; existing `BlurAnnotation.cs`, `BlurImageEffect.cs`, and `src/tools/XerahS.McpServer/Runtime/SkiaAnnotationRenderer.cs`.

### Phase 4: Warning, editor review and output hygiene

Add `PrivacyReviewView` / `PrivacyReviewViewModel` in UI, reached through a UI-independent review contract. Show original/redacted previews locally with findings overlays, effect controls and the actions described above. Use `ShowEditorSessionAsync` with proposed annotations; consume its `RenderedImage`. If a hosted editor cannot preload findings or return verified flattened output, use the existing XerahS editor for this review.

Keep original pixels and intermediate OCR/model content in memory where practical. Do not log sensitive snippets, base64 image payloads, raw model responses or decoded secrets. Strip metadata, embedded thumbnails and comments from the upload derivative. Audit generated upload filenames, window titles, descriptions, tags and request metadata too: a blurred image must not share a real name, email or token through its filename or associated text. Use existing naming helpers with privacy-safe naming inputs for the derivative without changing the user's permanent naming settings.

Do not upload editable annotation sidecars or a session's unredacted `SourceImage`; retained local originals/sidecars require a separate, clearly labelled retention choice. Generate thumbnails, OCR history indexing and completion previews from the approved derivative, avoiding searchable copies of removed secrets.

**Key files:** proposed UI review view/view-model and Core review interface; `IUIService.cs`, `CaptureJobProcessor.cs` sidecar/history handling, and `src/desktop/app/XerahS.UI/Services/CompositionRoot.cs`.

### Phase 5: Integrate both upload paths and verify final bytes

Implement one `PrivacyUploadGuard` used by both processors, including capture's direct `TryUploadWithPluginSystem` path. For screenshot capture, resolve After Capture choices, apply existing effects and ordinary annotation, then run privacy processing before saving/copying/uploading. This prevents saving the original first and accidentally reusing its stale `info.FilePath` after redaction.

Prepare and atomically encode the approved flattened derivative using existing naming/storage helpers. Replace both `info.Metadata.Image` and the upload file reference consistently. For an existing-file upload, create a separate derivative; do not overwrite the user's source file. Bind approval to the exact final encoded bytes, and have the uploader consume those same verified bytes or an immutable read handle, preventing a file-change race between validation and dispatch.

Immediately before plugin dispatch, check upload intent, policy and content approval again, including generated outbound filename/metadata. Edits, resizing, effects, changed destinations/policies or changed file contents invalidate approval. Capture-save and upload checks reuse the same completed scan; do not show duplicate prompts for identical content. No retry, queue resume, cross-category fallback, manual file upload, assistant action, CLI/MCP call or later History upload may reuse a stale approval or bypass the gate. Apply equivalent checking at separate image-publishing boundaries if they do not use these processors.

With privacy enabled, background jobs unable to obtain consent/review stay pending and surface **Review required**. Cancellation, timeout, authentication failure, unsupported vision, incomplete geometry, malformed output, failed rendering or encoding leaves destination upload blocked. Explicit manual review of a new flattened candidate is the supported recovery path.

**Key files:** proposed `src/desktop/core/XerahS.Core/Services/Privacy/PrivacyUploadGuard.cs`; existing `CaptureJobProcessor.cs`, `UploadJobProcessor.cs`, `Models/TaskInfo.cs`, `Services/UploadQueueService.cs`, and shared host service registration.

## Non-negotiable rules

1. Run only when privacy is enabled and a supported image is actually being uploaded. Save/copy-only captures trigger no AI/privacy processing.
2. Disclose AI transmission and obtain cloud consent before sending content; obtain approval for the final destination image separately.
3. Use the active supported AI connection and existing secrets storage. Never infer subscription access, switch providers silently, or bypass capability/authentication checks.
4. Reuse existing upload routing, task settings, editor and blur infrastructure. Do not introduce a parallel uploader or place UI/provider-specific logic in Core/platform capture code.
5. Upload only verified, flattened, approved bytes. Original pixels, editable layers, metadata and thumbnails must not leak through alternate outputs.
6. Scan failure is not a clean scan. Closing a warning or running headlessly is not upload approval.
7. Keep region coordinates tied to the exact image analyzed and invalidate stale approvals after changes.
8. Treat the screenshot and model output as untrusted data; keep all detection/redaction local except explicitly consented AI inference.
9. Describe blur and AI detection as risk reduction, not a guarantee. Offer stronger opaque masking for secrets.

## Deliverables

1. Upload-dependent Privacy mode checkbox, persisted policy and clear supported-media/provider status.
2. Active-provider image analysis with structured findings and compatible coordinate-aware OCR.
3. Shared headless region renderer reused by privacy mode, editor integration and MCP annotation.
4. Pre-upload warning/preview and existing-editor annotation with manual fallback.
5. A shared guard covering capture and existing-image upload paths, retries and background jobs.
6. Safe flattened derivatives, consistent file/image references, metadata cleanup and retention controls.
7. Focused tests and user documentation covering capabilities, cloud disclosure and redaction limitations.

## Verification and acceptance criteria

- With privacy off, existing behavior remains intact. With privacy on but upload off, assert zero privacy analyzer/AI calls and no image changes caused by privacy processing, including After Capture toggles, copy-only quick actions and cancelled tasks. Ordinary configured effects/annotation/OCR remain independent.
- Exercise capture's direct plugin path and `UploadJobProcessor` independently. Include existing-file, clipboard, History, queued/retried, assistant, CLI and MCP image uploads, plus image-to-file category fallback. An upload spy receives zero bytes before approval and on every failure/cancel path.
- Use synthetic multilingual fixtures with email/name/address/card/credential examples, wrapped text, faces and QR codes. Never commit actual personal information or credentials as fixtures. Test invalid/truncated provider responses, unsupported models, authentication/rate-limit errors and prompt injection in visible text.
- Test bounds across DPI/scaling, cropping, rotation and tiles, including out-of-bounds regions, overlapping masks and allocation failures. Compare pixels inside/outside target areas after encoding/decoding; test alpha edges so raw source text cannot show through. Opaque-mask tests verify underlying pixels are overwritten.
- Test no-provider and manual-only review, editor cancel/save, local-only handling, background review requirements, stale approvals and source-file changes. Confirm the uploader never receives an old `info.FilePath` or a source-only editor snapshot.
- Verify generated filenames/metadata, thumbnails, sidecars, logs, OCR indexes and history previews contain no deliberately removed synthetic secrets. Preserve the user's original imported file.
- Validate provider-specific image serialization and structured-result handling with mocked transports; run optional live-provider checks only with explicit consent and synthetic images.
- Run the relevant Core, Assistant, ImageEditor and MCP tests on supported desktop platforms, then `dotnet build src/desktop/XerahS.sln` with warnings-as-errors retained. Documentation drafting alone does not require an application build.

## Affected components

- **XerahS.Core**: task settings/info, privacy scan/review/upload contracts and services, both task processors, queue integration, sidecar and history handling.
- **XerahS.Assistant**: active-connection adapter, image request contracts/serialization, structured findings, capability checks and privacy-consent policy.
- **XerahS.Platform.Abstractions / platform OCR services**: optional coordinate-aware OCR results; no platform-specific upload logic.
- **ShareX.ImageEditor**: shared region renderer and compatibility with preloaded review annotations and flattened exports.
- **XerahS.UI**: workflow/After Capture checkbox, review view, editor round trip and composition-root registration.
- **XerahS.Bootstrap / CLI / MCP**: shared guard registration and pending-review behavior; MCP reuses the shared renderer.
- **Existing image publishing/export boundaries**: enforce the same policy where external sharing bypasses the normal task processors.

## Architecture summary

```text
Screenshot / existing still image + effective task settings
                         |
              Privacy enabled AND upload scheduled?
                 no -----+----- yes
                 |               |
          Existing behavior    Cloud consent, if needed
                                 |
                       Local OCR / patterns + active AI adapter
                                 |
                       Validated regions in source coordinates
                                 |
                       Shared local region redaction renderer
                                 |
                       Warning / preview <-> existing editor
                                 |
                       Approved flattened derivative
                                 |
                       Final byte/policy approval check
                                 |
                       Existing uploader / publishing boundary

No consent, incomplete scan, failed rendering or no review:
pause upload -> annotate manually / keep local only / cancel
```
