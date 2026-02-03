# Implementing ShareX-Style CustomUploader Support in XerahS
*A comprehensive, end-to-end technical breakdown based on ShareX source code*

---

## 1. Purpose of CustomUploader in ShareX

The **CustomUploader** system in ShareX allows users to define arbitrary HTTP-based uploaders without writing code. These uploaders support:

- Image uploads
- File uploads
- Text uploads
- URL shorteners
- URL sharing services

Each uploader is defined using a `.sxcu` JSON file and can be imported/exported. Once imported, the uploader becomes a first-class destination in ShareX workflows.

**Key design goal:**  
Convert a declarative JSON definition into a fully-executed HTTP request and parse its response dynamically.

---

## 2. High-Level Architecture

.sxcu (JSON)  
→ CustomUploaderItem  
→ Custom*Uploader (Image/File/Text/URL)  
→ HTTP Request Builder  
→ HTTP Response  
→ Syntax Parser (functions, JSON/XML/regex)  
→ UploadResult (URL, Thumbnail, Deletion URL, Error)

---

## 3. `.sxcu` File Structure

An `.sxcu` file is a JSON schema that defines everything required to perform an upload.

### Example

```json
{
  "Version": "17.0.0",
  "Name": "Example",
  "DestinationType": "ImageUploader, FileUploader",
  "RequestMethod": "POST",
  "RequestURL": "https://example.com/upload.php",
  "Parameters": {
    "api_key": "ABC123"
  },
  "Headers": {
    "Authorization": "Bearer {inputbox:API Token}"
  },
  "Body": "MultipartFormData",
  "Arguments": {
    "file": "{input}"
  },
  "FileFormName": "file",
  "URL": "{json:url}",
  "ThumbnailURL": "{json:thumb}",
  "DeletionURL": "{json:delete}",
  "ErrorMessage": "{json:error}"
}
```

---

## 4. Core Data Model: `CustomUploaderItem`

**Location:**  
`ShareX.UploadersLib/CustomUploader/CustomUploaderItem.cs`

### Responsibilities

- Deserialize `.sxcu` JSON
- Store all request metadata
- Act as a configuration object for execution

### Key Properties

- DestinationType
- RequestMethod
- RequestURL
- Parameters
- Headers
- Body
- Arguments
- FileFormName
- URL / ThumbnailURL / DeletionURL / ErrorMessage

---

## 5. Execution Entry Points

Each destination type has a thin wrapper that executes the uploader:

- CustomImageUploader  
- CustomFileUploader  
- CustomTextUploader  
- CustomURLShortener  
- CustomURLSharingService  

All delegate to a shared execution pipeline.

---

## 6. HTTP Request Construction

### Components

- URL = RequestURL + Parameters
- Method = RequestMethod
- Headers = Headers
- Body = Body + Arguments
- File = FileFormName

### Supported Body Types

- None
- MultipartFormData
- FormURLEncoded
- JSON
- XML
- Binary

---

## 7. Syntax Engine

**Class:** `ShareXCustomUploaderSyntaxParser`

This parser resolves `{function:args}` expressions at runtime and supports nesting.

---

## 8. Function Discovery

All syntax functions inherit from:

```csharp
CustomUploaderFunction
```

They are discovered via reflection:

```csharp
Helpers.GetInstances<CustomUploaderFunction>();
```

---

## 9. Built-in Syntax Functions

### Input / File

- `{input}`
- `{filename}`

### Encoding / Random

- `{base64:x}`
- `{random:a|b|c}`
- `{select:a|b}`

### User Interaction

- `{inputbox}`
- `{outputbox:text}`

### Response

- `{response}`
- `{responseurl}`
- `{header:Location}`

### Parsing

- `{json:path}`
- `{xml:xpath}`
- `{regex:pattern}`

---

## 10. Response Handling

After execution, ShareX builds a `ResponseInfo` object:

- ResponseText
- ResponseURL
- Headers

Each response field is parsed using the syntax engine.

---

## 11. Upload Result Mapping

Parsed values are mapped into:

```csharp
UploadResult
{
  URL,
  ThumbnailURL,
  DeletionURL,
  Error
}
```

---

## 12. UI Integration

- Import/export `.sxcu`
- Destination auto-selection
- Validation feedback
- User prompts via syntax

---

## 13. Design Principles

1. Declarative configuration
2. Reflection-based extensibility
3. Single execution pipeline
4. Pluggable syntax engine
5. Service-agnostic design

---

## 14. Suggested XerahS Porting Strategy

### Phase 1
- CustomUploaderItem
- HTTP execution engine
- ResponseInfo / UploadResult

### Phase 2
- Syntax parser
- Function registry

### Phase 3
- Destination wrappers
- Workflow integration

### Phase 4
- UI and `.sxcu` import/export

---

## 15. Key Takeaway

ShareX CustomUploader is a general-purpose HTTP execution and parsing framework expressed declaratively.

Preserve the separation between:
- Configuration
- Execution
- Parsing
- UI

when implementing this in XerahS.
