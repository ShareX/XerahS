# XerahS MCP Server Usage Guide

This guide describes the current `xerahs-mcp` implementation. It matches XIP0064 and the server in `src/tools/XerahS.McpServer`.

## What It Does

`xerahs-mcp` exposes XerahS over the Model Context Protocol (MCP). It boots the real XerahS runtime headlessly and provides tools for:

- screen capture
- headless annotation rendering
- file and clipboard upload
- history search and item details
- workflow discovery
- MCP-safe settings access

The MCP server is not a shell wrapper around the CLI.

## Runtime Scope

The current MCP server is a user-desktop runtime. It must run on the same machine whose screen, clipboard, history, settings, and uploader configuration are being controlled.

Cloudflare, GitHub Pages, or any other static/edge host can publish discovery metadata or proxy requests to a reachable `xerahs-mcp` process, but they do not run XerahS themselves. A public hostname such as `mcp.xerahs.com` is only usable when it is backed by a real `xerahs-mcp --transport http` process running on a desktop session or a dedicated desktop/VM host.

For most users and MCP hosts, local stdio mode is the intended integration path.

For the contributor-facing setup checklist, see `developers/guidelines/MCP_CONFIGURATION_GUIDELINE.html`.

## Prerequisites

- A built XerahS tree or a released build that includes `xerahs-mcp`.
- A logged-in desktop session on the machine that will run captures.
- OS permissions for screen capture, window capture, clipboard, and accessibility features as required by the platform.
- Configured uploader destinations if you want `upload_file` or `upload_clipboard` to return upload URLs.

## Build

From the repository root:

```powershell
dotnet build .\src\tools\XerahS.McpServer\XerahS.McpServer.csproj -c Release
```

For development, you can run without publishing:

```powershell
dotnet run --project .\src\tools\XerahS.McpServer\XerahS.McpServer.csproj -- --mcp
```

For packaged builds, use the `xerahs-mcp` executable produced by the project.

## Local Stdio Mode

Use stdio mode for local MCP hosts such as editor integrations or desktop agents running on the same machine:

```powershell
xerahs-mcp --mcp
```

Equivalent:

```powershell
xerahs-mcp --mcp-server --transport stdio
```

Each stdin line is one JSON-RPC 2.0 request. Each stdout line is one JSON-RPC 2.0 response.

Example MCP client configuration:

```json
{
  "mcpServers": {
    "xerahs": {
      "command": "C:/Program Files/XerahS/xerahs-mcp.exe",
      "args": ["--mcp"]
    }
  }
}
```

Use the installed `xerahs-mcp` path for packaged builds. For development builds, use the executable under `src/tools/XerahS.McpServer/bin/<Configuration>/net11.0/`.

## HTTP and SSE Mode

Use HTTP mode when an MCP client connects over the network to a user-controlled machine that is running XerahS:

```powershell
xerahs-mcp --mcp-server --transport http --port 7890
```

Endpoints:

- `POST /mcp/` for JSON-RPC requests
- `GET /mcp/events/` for SSE notifications and heartbeats
- `GET /health` for health checks

HTTP transport requires bearer authentication:

```http
Authorization: Bearer <mcp-api-key>
```

The API key is stored in `ApplicationConfig.McpApiKey`. It can be copied or regenerated from XerahS application settings under `Integration -> MCP Server`. If no key exists, the MCP runtime generates and saves one on first startup.

HTTP mode does not turn XerahS into a hosted cloud service. The process still needs an interactive desktop session and the same OS capture, clipboard, and accessibility permissions required by the desktop app.

## Public Discovery Manifest

The public manifest is:

```text
https://xerahs.com/.well-known/mcp/manifest.json
```

It advertises the optional public HTTP endpoint:

```text
https://mcp.xerahs.com/mcp/
https://mcp.xerahs.com/mcp/events/
```

GitHub Pages or Cloudflare hosts the manifest and documentation. It is not the MCP execution host.

The public endpoint is a deployment slot, not proof that a shared hosted XerahS runtime exists. It only works when the operator has configured the endpoint to proxy to a running `xerahs-mcp` HTTP backend. If no backend is configured, clients should use local stdio mode instead.

## Basic JSON-RPC Examples

Initialize:

```json
{ "jsonrpc": "2.0", "id": 1, "method": "initialize", "params": {} }
```

List tools:

```json
{ "jsonrpc": "2.0", "id": 2, "method": "tools/list", "params": {} }
```

Call a tool:

```json
{
  "jsonrpc": "2.0",
  "id": 3,
  "method": "tools/call",
  "params": {
    "name": "capture_full_screen",
    "arguments": {
      "monitor": 0
    }
  }
}
```

Read a resource:

```json
{
  "jsonrpc": "2.0",
  "id": 4,
  "method": "resources/read",
  "params": {
    "uri": "xerahs://settings/general"
  }
}
```

Get a prompt:

```json
{
  "jsonrpc": "2.0",
  "id": 5,
  "method": "prompts/get",
  "params": {
    "name": "upload_workflow",
    "arguments": {
      "user_request_describing_what_to_capture_and_annotate": "Capture the browser window",
      "destination_id_or_default": "default"
    }
  }
}
```

## Tools

### `capture_region`

Opens the XerahS region selector overlay, waits for the user to choose a region, saves the capture, and returns the saved path.

Arguments:

```json
{
  "workflow_id": "optional workflow id",
  "monitor": 0
}
```

Notes:

- `workflow_id` applies workflow capture settings when supplied.
- `monitor` constrains the selected region to that monitor.
- User cancellation returns an MCP user-cancelled error.

### `capture_window`

Captures the foreground window or the first window whose title contains `window_title`.

Arguments:

```json
{
  "window_title": "Firefox",
  "include_decoration": true
}
```

Notes:

- If `window_title` is omitted, the foreground window is captured.
- `include_decoration=false` captures only the client area.

### `capture_full_screen`

Captures all monitors, or a single monitor when `monitor` is supplied.

Arguments:

```json
{
  "monitor": 0
}
```

### `capture_scrolling`

Runs XerahS scrolling capture against the active window when supported by the platform.

Arguments:

```json
{
  "scroll_direction": "down",
  "max_frames": 50
}
```

Notes:

- `scroll_direction` and `max_frames` are accepted and returned for contract stability.
- The current scrolling capture engine is not hard-capped by `max_frames`.

### `annotate_image`

Applies annotations to an existing image and writes a new annotated file.

Arguments:

```json
{
  "image_path": "C:/path/to/image.png",
  "annotations": [
    {
      "type": "rectangle",
      "params": {
        "x": 10,
        "y": 20,
        "width": 200,
        "height": 120,
        "color": "#ff3b30",
        "thickness": 4
      }
    }
  ],
  "auto_save": true
}
```

Supported annotation types:

- `arrow`
- `rectangle`
- `ellipse`
- `line`
- `text`
- `freehand`
- `blur`
- `pixelate`
- `step`

Notes:

- This is a headless renderer.
- It does not launch the interactive image editor.
- `auto_save` is accepted for compatibility; MCP annotation always writes the output file.

### `upload_file`

Uploads a file through configured XerahS uploader instances.

Arguments:

```json
{
  "file_path": "C:/path/to/file.png",
  "destination": "optional instance id, provider id, or display name"
}
```

If `destination` is omitted, XerahS resolves the default destination for the file category.

### `upload_clipboard`

Uploads current clipboard data when it contains a supported image, text, or file drop list.

Arguments:

```json
{
  "destination": "optional instance id, provider id, or display name"
}
```

### `query_history`

Searches the XerahS history database.

Arguments:

```json
{
  "query": "optional free text",
  "from_date": "2026-04-01",
  "to_date": "2026-04-11",
  "file_type": "all",
  "limit": 20
}
```

Supported `file_type` values:

- `image`
- `video`
- `text`
- `file`
- `all`

### `get_history_item`

Returns detailed metadata for one history item.

Arguments:

```json
{
  "id": "123"
}
```

History IDs are SQLite row IDs serialized as strings. They are not UUIDs.

### `list_workflows`

Lists configured workflows with their job, capture mode, after-capture actions, after-upload actions, enabled state, and tray pin state.

Arguments:

```json
{}
```

### `get_settings`

Returns MCP-safe settings.

Arguments:

```json
{
  "category": "integration"
}
```

Supported categories:

- `capture`
- `upload`
- `history`
- `general`
- `integration`

If `category` is omitted, all MCP-safe categories are returned. Secrets are not returned.

## Resources

Supported resource URIs:

- `xerahs://history/{id}`
- `xerahs://history/thumb/{id}`
- `xerahs://history/search?q={query}`
- `xerahs://capture/latest`
- `xerahs://workflows`
- `xerahs://workflows/{id}`
- `xerahs://settings/capture`
- `xerahs://settings/upload`
- `xerahs://settings/history`
- `xerahs://settings/general`
- `xerahs://settings/integration`
- `xerahs://destinations`
- `xerahs://monitors`

History summary/detail payloads include `thumbnail_resource` when clients need the MCP blob URI for an item. `xerahs://history/thumb/{id}` currently returns a base64 blob of the local thumbnail file when one exists, otherwise the stored history item file. It is not a separate thumbnail-rendering pipeline.

## Prompts

Supported prompts:

- `capture_and_annotate`
- `batch_capture_report`
- `upload_workflow`

Use `prompts/list` to discover prompt metadata and `prompts/get` to render a prompt with arguments.

## Troubleshooting

- If HTTP requests return `401`, verify the bearer token from `Integration -> MCP Server`.
- If upload tools fail, verify uploader instances and defaults are configured in XerahS.
- If capture tools fail, verify the server is running inside an interactive desktop session with capture permissions.
- If `capture_scrolling` fails, verify the current platform and active window support scrolling capture.
- If local MCP hosts cannot discover tools, verify the configured `xerahs-mcp` executable path and arguments.
- If the public manifest resolves but remote requests fail, verify that `mcp.xerahs.com` points at a running `xerahs-mcp` HTTP backend. A Cloudflare Worker by itself can only proxy requests; it cannot execute XerahS capture, clipboard, history, uploader, or settings operations.
