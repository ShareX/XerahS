# mcp.xerahs.com Worker

`xerahs-mcp-worker` serves the public MCP manifest (`/.well-known/mcp/manifest.json`), `/health`, CORS preflights, and proxies `/mcp/` and `/mcp/events/` to the backend named by `MCP_BACKEND_URL`.
It does not run XerahS: the backend is a user-controlled desktop running `xerahs-mcp --mcp-server --transport http`.

- `MCP_BACKEND_URL` (variable): origin of the backend. While it is unset, `/mcp/` answers 503 `MCP backend is not configured` and points clients at local stdio (`xerahs-mcp --mcp`).
- `MCP_BEARER_TOKEN` (secret, optional): when set, the Worker rejects any other Bearer token at the edge. The backend still enforces its own API key.

Deploy from this folder with `npx wrangler deploy` (or `npx wrangler versions upload` then `versions deploy`, which leaves routes untouched).
Keep `MCP_MANIFEST.capabilities` in `worker.js` in line with `docs/.well-known/mcp/manifest.json` and with what `src/tools/XerahS.McpServer` advertises.
