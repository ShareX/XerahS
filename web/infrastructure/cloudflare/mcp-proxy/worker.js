const MCP_PATH = "/mcp/";
const EVENTS_PATH = "/mcp/events/";
const HEALTH_PATH = "/health";
const MANIFEST_PATH = "/.well-known/mcp/manifest.json";

const CORS_HEADERS = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers": "Authorization, Content-Type, Accept, Mcp-Session-Id, MCP-Protocol-Version",
  "Access-Control-Allow-Methods": "GET, POST, OPTIONS"
};

const MCP_MANIFEST = {
  name: "xerahs",
  version: "0.22.0",
  description: "XerahS MCP server for capture, annotation, upload, history, workflows, and safe settings access.",
  endpoint: "https://mcp.xerahs.com/mcp/",
  endpoint_events: "https://mcp.xerahs.com/mcp/events/",
  authentication: {
    type: "bearer",
    description: "API key from XerahS settings (Settings -> Integration -> MCP API Key)"
  },
  documentation_url: "https://xerahs.com/features/mcp-server.html",
  capabilities: {
    tools: {},
    resources: {},
    prompts: {}
  }
};

export default {
  async fetch(request, env) {
    const url = new URL(request.url);

    if (url.pathname === MANIFEST_PATH && request.method === "GET") {
      return withCors(jsonResponse(200, MCP_MANIFEST));
    }

    if (request.method === "OPTIONS" && isHandledPath(url.pathname)) {
      return new Response(null, {
        status: 204,
        headers: CORS_HEADERS
      });
    }

    if (url.pathname === HEALTH_PATH) {
      return withCors(
        jsonResponse(200, {
          status: "ok",
          server: "xerahs-mcp-worker"
        })
      );
    }

    if (!isHandledPath(url.pathname)) {
      return withCors(jsonResponse(404, { error: "Not found" }));
    }

    const backendUrl = getBackendUrl(env, url.pathname, url.search);
    if (!backendUrl) {
      return withCors(
        jsonResponse(500, {
          error: "MCP backend is not configured"
        })
      );
    }

    const authError = validateBearerToken(request, env);
    if (authError) {
      return withCors(authError);
    }

    const upstreamResponse = await fetch(backendUrl, buildProxyRequest(request, url, backendUrl), {
      cf: { cacheEverything: false }
    });

    return withCors(copyResponse(upstreamResponse));
  }
};

function isHandledPath(pathname) {
  return pathname === MCP_PATH || pathname === EVENTS_PATH || pathname === MANIFEST_PATH;
}

function getBackendUrl(env, pathname, search) {
  const backendOrigin = env.MCP_BACKEND_URL?.trim();
  if (!backendOrigin) {
    return null;
  }

  return new URL(pathname + search, ensureTrailingSlash(backendOrigin)).toString();
}

function validateBearerToken(request, env) {
  const authHeader = request.headers.get("Authorization");
  if (!authHeader) {
    return jsonResponse(401, { error: "Missing Authorization header" });
  }

  const match = authHeader.match(/^Bearer\s+(.+)$/i);
  if (!match) {
    return jsonResponse(401, { error: "Authorization must use Bearer auth" });
  }

  const token = match[1].trim();
  if (!token) {
    return jsonResponse(401, { error: "Bearer token is empty" });
  }

  const expectedToken = env.MCP_BEARER_TOKEN?.trim();
  if (expectedToken && token !== expectedToken) {
    return jsonResponse(401, { error: "Invalid bearer token" });
  }

  return null;
}

function buildProxyRequest(request, url, backendUrl) {
  const headers = new Headers(request.headers);
  headers.delete("Host");
  headers.set("X-Forwarded-Host", url.host);
  headers.set("X-Forwarded-Proto", url.protocol.replace(":", ""));

  return new Request(backendUrl, {
    method: request.method,
    headers,
    body: request.method === "GET" || request.method === "HEAD" ? undefined : request.body,
    redirect: "manual"
  });
}

function copyResponse(response) {
  const headers = new Headers(response.headers);
  headers.set("Cache-Control", headers.get("Cache-Control") ?? "no-store");

  return new Response(response.body, {
    status: response.status,
    statusText: response.statusText,
    headers
  });
}

function withCors(response) {
  const headers = new Headers(response.headers);
  for (const [key, value] of Object.entries(CORS_HEADERS)) {
    headers.set(key, value);
  }

  return new Response(response.body, {
    status: response.status,
    statusText: response.statusText,
    headers
  });
}

function jsonResponse(status, payload) {
  return new Response(JSON.stringify(payload, null, 2), {
    status,
    headers: {
      "Content-Type": "application/json",
      "Cache-Control": "no-store"
    }
  });
}

function ensureTrailingSlash(value) {
  return value.endsWith("/") ? value : value + "/";
}
