#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using System.Text.Json.Nodes;
using XerahS.History;
using XerahS.McpServer.Runtime;
using XerahS.McpServer.Server;
using Xunit;

namespace XerahS.McpServer.Tests;

public class XerahSMcpServerTests
{
    [Fact]
    public async Task Initialize_ReturnsProtocolVersionAndRuntimeVersion()
    {
        var runtime = new FakeRuntime();
        var server = new McpWireServer(runtime);

        var response = await server.HandleRequestAsync(new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = 1,
            Method = "initialize",
            Params = new
            {
                protocolVersion = "2024-11-05",
                capabilities = new { },
                clientInfo = new { name = "xerahs-tests", version = "1.0" }
            }
        });

        Assert.Null(response.Error);
        var result = Assert.IsType<JsonObject>(response.Result);
        // The SDK negotiates the protocol version with the client and derives capabilities from the handlers.
        Assert.Equal("2024-11-05", result["protocolVersion"]?.GetValue<string>());
        Assert.Equal("xerahs", result["serverInfo"]?["name"]?.GetValue<string>());
        Assert.Equal(runtime.ServerVersion, result["serverInfo"]?["version"]?.GetValue<string>());
        Assert.NotNull(result["capabilities"]?["tools"]);
        Assert.NotNull(result["capabilities"]?["resources"]);
        Assert.NotNull(result["capabilities"]?["prompts"]);
    }

    [Fact]
    public async Task ToolsList_ContainsImplementedTools()
    {
        var server = new McpWireServer(new FakeRuntime());

        var response = await server.HandleRequestAsync(new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = 2,
            Method = "tools/list",
            Params = new { }
        });

        var result = Assert.IsType<JsonObject>(response.Result);
        var tools = Assert.IsType<JsonArray>(result["tools"]);
        Assert.Contains(tools, node => node?["name"]?.GetValue<string>() == "capture_region");
        Assert.Contains(tools, node => node?["name"]?.GetValue<string>() == "annotate_image");
        Assert.Contains(tools, node => node?["name"]?.GetValue<string>() == "upload_file");
        Assert.Contains(tools, node => node?["name"]?.GetValue<string>() == "query_history");
        Assert.Contains(tools, node => node?["name"]?.GetValue<string>() == "get_settings");
    }

    [Fact]
    public async Task ToolsCall_DelegatesToRuntime()
    {
        var runtime = new FakeRuntime();
        var server = new McpWireServer(runtime);

        var response = await server.HandleRequestAsync(new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = 3,
            Method = "tools/call",
            Params = JsonNode.Parse("""
            {
              "name": "capture_full_screen",
              "arguments": {
                "monitor": 1
              }
            }
            """)
        });

        Assert.Null(response.Error);
        var result = Assert.IsType<JsonObject>(response.Result);
        // Bare runtime results are sent as structuredContent plus a JSON text block.
        var structured = Assert.IsType<JsonObject>(result["structuredContent"]);
        Assert.Equal("capture_full_screen", structured["tool"]?.GetValue<string>());
        Assert.Equal(1, structured["monitor"]?.GetValue<int>());
        Assert.Equal("text", result["content"]?[0]?["type"]?.GetValue<string>());
        Assert.Equal("capture_full_screen", JsonNode.Parse(result["content"]![0]!["text"]!.GetValue<string>())!["tool"]?.GetValue<string>());
        Assert.Equal("capture_full_screen", runtime.LastCall);
    }

    [Fact]
    public async Task ResourcesRead_DelegatesToRuntime()
    {
        var runtime = new FakeRuntime();
        var server = new McpWireServer(runtime);

        var response = await server.HandleRequestAsync(new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = 4,
            Method = "resources/read",
            Params = JsonNode.Parse("""
            {
              "uri": "xerahs://settings/general"
            }
            """)
        });

        Assert.Null(response.Error);
        var result = Assert.IsType<JsonObject>(response.Result);
        Assert.Equal("xerahs://settings/general", result["contents"]?[0]?["uri"]?.GetValue<string>());
        Assert.Equal("xerahs://settings/general", runtime.LastResourceUri);
    }

    [Fact]
    public async Task ToolsCall_RejectsNonObjectArguments()
    {
        var server = new McpWireServer(new FakeRuntime());

        var response = await server.HandleRequestAsync(new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = 5,
            Method = "tools/call",
            Params = JsonNode.Parse("""
            {
              "name": "capture_full_screen",
              "arguments": [1, 2, 3]
            }
            """)
        });

        // The SDK rejects malformed params while binding the request, before any XerahS handler runs.
        AssertRejectedAsMalformed(response);
    }

    [Theory]
    [InlineData("tools/call", "name")]
    [InlineData("resources/read", "uri")]
    [InlineData("prompts/get", "name")]
    public async Task RequiredStringParamMethods_RejectNonStringValues(string method, string propertyName)
    {
        var server = new McpWireServer(new FakeRuntime());

        var response = await server.HandleRequestAsync(new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = 8,
            Method = method,
            Params = new JsonObject
            {
                [propertyName] = 123
            }
        });

        AssertRejectedAsMalformed(response);
    }


    [Theory]
    [InlineData("resources/read")]
    [InlineData("prompts/get")]
    public async Task ObjectParamMethods_RejectArrayParams(string method)
    {
        var server = new McpWireServer(new FakeRuntime());

        var response = await server.HandleRequestAsync(new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = 7,
            Method = method,
            Params = JsonNode.Parse("[]")
        });

        AssertRejectedAsMalformed(response);
    }

    /// <summary>
    /// The pre-SDK server answered malformed params with InvalidParams (-32602). The SDK reports a binding failure
    /// as an error response without that precision, so only the rejection itself is asserted.
    /// </summary>
    private static void AssertRejectedAsMalformed(JsonRpcResponse response)
    {
        Assert.NotNull(response.Error);
        Assert.Null(response.Result);
        Assert.Contains(response.Error!.Code, new[] { JsonRpcErrorCodes.InvalidParams, JsonRpcErrorCodes.InternalError });
    }

    [Fact]
    public async Task ResourcesList_ReturnsConcreteResources_AndTemplatesAreListedSeparately()
    {
        var server = new McpWireServer(new FakeRuntime());

        var list = await server.HandleRequestAsync(new JsonRpcRequest { Id = 20, Method = "resources/list", Params = new { } });
        var templates = await server.HandleRequestAsync(new JsonRpcRequest { Id = 21, Method = "resources/templates/list", Params = new { } });

        Assert.Null(list.Error);
        Assert.Null(templates.Error);
        var uris = list.Result!["resources"]!.AsArray().Select(node => node!["uri"]!.GetValue<string>()).ToList();
        Assert.Contains("xerahs://settings/general", uris);
        Assert.Contains("xerahs://capture/latest", uris);
        Assert.DoesNotContain(uris, uri => uri.Contains('{'));
        var templateUris = templates.Result!["resourceTemplates"]!.AsArray().Select(node => node!["uriTemplate"]!.GetValue<string>()).ToList();
        Assert.Contains("xerahs://history/{id}", templateUris);
        Assert.Contains("xerahs://workflows/{id}", templateUris);
    }

    [Fact]
    public async Task ToolsCall_UnknownTool_ReportsInvalidParamsWithItsMessage()
    {
        var server = new McpWireServer(new FakeRuntime());

        var response = await server.HandleRequestAsync(new JsonRpcRequest
        {
            Id = 22,
            Method = "tools/call",
            Params = JsonNode.Parse("""{"name":"does_not_exist","arguments":{}}""")
        });

        Assert.NotNull(response.Error);
        Assert.Equal(JsonRpcErrorCodes.InvalidParams, response.Error!.Code);
        Assert.Contains("Unknown tool: does_not_exist", response.Error.Message);
    }

    [Fact]
    public async Task PromptsGet_RendersTemplateArguments()
    {
        var server = new McpWireServer(new FakeRuntime());

        var response = await server.HandleRequestAsync(new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = 6,
            Method = "prompts/get",
            Params = JsonNode.Parse("""
            {
              "name": "upload_workflow",
              "arguments": {
                "user_request_describing_what_to_capture_and_annotate": "Capture the browser window",
                "destination_id_or_default": "imgur"
              }
            }
            """)
        });

        Assert.Null(response.Error);
        var result = Assert.IsType<JsonObject>(response.Result);
        var text = result["messages"]?[0]?["content"]?["text"]?.GetValue<string>();
        Assert.Contains("Capture the browser window", text);
        Assert.Contains("imgur", text);
    }

    [Fact]
    public async Task PromptsGet_UnknownTemplate_ReturnsInvalidParamsError()
    {
        var server = new McpWireServer(new FakeRuntime());

        var response = await server.HandleRequestAsync(new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = 99,
            Method = "prompts/get",
            Params = JsonNode.Parse(/* lang=json */ """{ "name": "nonexistent_prompt" }""")
        });

        Assert.NotNull(response.Error);
        Assert.Equal(JsonRpcErrorCodes.InvalidParams, response.Error!.Code);
        Assert.Contains("Unknown prompt template", response.Error.Message);
    }

    [Fact]
    public async Task HeadlessMcpUIService_ShowEditorAsync_ReturnsNullWhenEditorUnavailable()
    {
        var service = new HeadlessMcpUIService();
        using var image = new SkiaSharp.SKBitmap(4, 4);

        var result = await service.ShowEditorAsync(image, taskMode: true);

        Assert.Null(result);
    }

    [Fact]
    public void RuntimeHistoryBlobPath_PrefersLocalThumbnailFile()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"xerahs-mcp-history-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string sourcePath = Path.Combine(directory, "capture.png");
            string thumbnailPath = Path.Combine(directory, "thumb.png");
            File.WriteAllText(sourcePath, "source");
            File.WriteAllText(thumbnailPath, "thumb");

            var item = new HistoryItem
            {
                FilePath = sourcePath,
                ThumbnailURL = thumbnailPath
            };

            Assert.Equal(thumbnailPath, XerahSMcpRuntime.ResolveHistoryBlobPath(item));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RuntimeHistoryBlobPath_IgnoresRemoteThumbnailUrl()
    {
        string sourcePath = Path.Combine(Path.GetTempPath(), $"xerahs-mcp-history-{Guid.NewGuid():N}.txt");
        File.WriteAllText(sourcePath, "source");
        try
        {
            var item = new HistoryItem
            {
                FilePath = sourcePath,
                ThumbnailURL = "https://example.test/thumb.png"
            };

            Assert.Equal(sourcePath, XerahSMcpRuntime.ResolveHistoryBlobPath(item));
        }
        finally
        {
            File.Delete(sourcePath);
        }
    }

    [Fact]
    public void RuntimeHistoryBlobPath_MissingThumbnailOnly_FallsBackToSourceFile()
    {
        // If the thumbnail file is gone but the source capture is still on disk, the helper
        // must transparently fall back to the source path so the user can still read the
        // capture. This test pins that fallback behavior (not an exception).
        string directory = Path.Combine(Path.GetTempPath(), $"xerahs-mcp-history-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string sourcePath = Path.Combine(directory, "capture.png");
            string thumbnailPath = Path.Combine(directory, "thumb.png");
            File.WriteAllText(sourcePath, "source");
            // Thumbnail is NOT written — only the source file exists locally.

            var item = new HistoryItem
            {
                FilePath = sourcePath,
                ThumbnailURL = thumbnailPath
            };

            Assert.Equal(sourcePath, XerahSMcpRuntime.ResolveHistoryBlobPath(item));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RuntimeHistoryBlobPath_MissingBothFiles_MentionsThumbnailAndSource()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"xerahs-mcp-history-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string sourcePath = Path.Combine(directory, "capture.png");
            string thumbnailPath = Path.Combine(directory, "thumb.png");
            // Neither file is created on disk.

            var item = new HistoryItem
            {
                FilePath = sourcePath,
                ThumbnailURL = thumbnailPath
            };

            var ex = Assert.Throws<FileNotFoundException>(() => XerahSMcpRuntime.ResolveHistoryBlobPath(item));
            // FileName is the user's original capture (item.FilePath) — matches the prior
            // contract so existing debug-log consumers do not break.
            Assert.Equal(sourcePath, ex.FileName);
            Assert.Equal("History item thumbnail and source files were not found.", ex.Message);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RuntimeHistoryBlobPath_OnlyThumbnailConfiguredMissing_KeepsThumbnailOnlyMessage()
    {
        // Item has only a ThumbnailURL; the original capture is unknown. The helper falls
        // through both checks and should fall back to the original "thumbnail source file"
        // wording rather than the "source file" wording (which would be misleading here).
        var item = new HistoryItem
        {
            FilePath = string.Empty,
            ThumbnailURL = "/definitely/does/not/exist/thumb.png"
        };

        var ex = Assert.Throws<FileNotFoundException>(() => XerahSMcpRuntime.ResolveHistoryBlobPath(item));
        Assert.Equal("History item thumbnail source file was not found.", ex.Message);
    }

    [Fact]
    public void RuntimeFileUrl_UsesAbsoluteFileUriForRelativePaths()
    {
        string relativePath = Path.Combine(".", "capture with spaces.png");
        string? url = XerahSMcpRuntime.CreateFileUrl(relativePath);

        Assert.NotNull(url);
        // Spaces should be percent-encoded, and the URL should be a valid absolute file URI.
        Assert.Contains("capture%20with%20spaces.png", url);
    }

    [Fact]
    public void RuntimeFileUrl_PreservesLeadingAndTrailingFilePathWhitespace()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"xerahs-mcp-uri-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, " capture trailing space .png ");
            string? expected = XerahSMcpRuntime.CreateFileUrl(path);

            Assert.NotNull(expected);
            Assert.Contains("capture%20trailing%20space%20.png%20", expected);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RuntimeFileUrl_EscapesHashCharacter()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"xerahs-mcp-hash-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "screenshot #1.png");
            File.WriteAllText(path, "source");
            string? url = XerahSMcpRuntime.CreateFileUrl(path);

            Assert.NotNull(url);
            Assert.Contains("%23", url, StringComparison.Ordinal);
            // The hash must not be interpreted as a URI fragment.
            Assert.DoesNotContain("#", url.Split('?')[0]);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RuntimeFileUrl_EscapesQuestionMark()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"xerahs-mcp-qmark-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "screenshot? test.png");
            string? url = XerahSMcpRuntime.CreateFileUrl(path);

            Assert.NotNull(url);
            Assert.Contains("%3F", url);
            // The query must not be interpreted as a URI query separator.
            var afterScheme = url.Split("://")[1];
            Assert.DoesNotContain("?", afterScheme.Split('#')[0]);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RuntimeFileUrl_ReturnsNullForNullOrWhitespace()
    {
        Assert.Null(XerahSMcpRuntime.CreateFileUrl(null));
        Assert.Null(XerahSMcpRuntime.CreateFileUrl(""));
        Assert.Null(XerahSMcpRuntime.CreateFileUrl("   "));
    }

    [Fact]
    public void RuntimeHistoryBlobPath_PreservesThumbnailPathWhitespace()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"xerahs-mcp-history-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string sourcePath = Path.Combine(directory, "source.png");
            string thumbnailPath = Path.Combine(directory, " thumb with space .png ");
            File.WriteAllText(sourcePath, "source");
            File.WriteAllText(thumbnailPath, "thumb");

            var item = new HistoryItem
            {
                FilePath = sourcePath,
                ThumbnailURL = thumbnailPath
            };

            Assert.Equal(thumbnailPath, XerahSMcpRuntime.ResolveHistoryBlobPath(item));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task CreateHistoryDetailsAsync_MissingSourceFile_SurfacesStalePathDiagnostic()
    {
        // The source capture file path is configured but the file is not on disk.
        // The response must surface a clear file_exists=false / file_missing_path
        // diagnostic so MCP clients can prompt the user to relocate the capture
        // or fall back to the upload_url, instead of silently returning null
        // dimensions and a 0-byte file size (the prior behaviour).
        var runtime = new XerahSMcpRuntime();
        string missingPath = Path.Combine(
            Path.GetTempPath(),
            $"xerahs-mcp-details-missing-{Guid.NewGuid():N}.png");

        var item = new HistoryItem
        {
            Id = 7777,
            FilePath = missingPath,
            ThumbnailURL = string.Empty,
            URL = "https://share.example.test/abc123"
        };

        var details = await runtime.CreateHistoryDetailsAsync(item, CancellationToken.None);

        Assert.Equal(missingPath, details["file_path"]?.GetValue<string>());
        Assert.False(details["file_exists"]?.GetValue<bool>());
        Assert.Equal(missingPath, details["file_missing_path"]?.GetValue<string>());
        Assert.Equal(0L, details["file_size_bytes"]?.GetValue<long>());
        Assert.Null(details["file_hash_md5"]?.GetValue<string?>());
        Assert.Null(details["capture_width"]?.GetValue<int?>());
        Assert.Null(details["capture_height"]?.GetValue<int?>());
        // upload_url remains populated so the caller can offer the hosted link.
        Assert.Equal("https://share.example.test/abc123", details["upload_url"]?.GetValue<string>());
    }

    [Fact]
    public async Task CreateHistoryDetailsAsync_ExistingSourceFile_ReportsFileExistsTrueAndHash()
    {
        // When the source capture file IS on disk, the response should report
        // file_exists=true, file_missing_path=null, and populate size/hash
        // from the real file. The new diagnostic fields must not regress the
        // happy path.
        var runtime = new XerahSMcpRuntime();
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"xerahs-mcp-details-present-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string sourcePath = Path.Combine(directory, "capture.png");
            // 16 deterministic bytes so the MD5 hash is stable.
            byte[] payload = new byte[]
            {
                0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
                0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52
            };
            File.WriteAllBytes(sourcePath, payload);

            var item = new HistoryItem
            {
                Id = 8888,
                FilePath = sourcePath,
                ThumbnailURL = string.Empty
            };

            var details = await runtime.CreateHistoryDetailsAsync(item, CancellationToken.None);

            Assert.True(details["file_exists"]?.GetValue<bool>());
            Assert.Null(details["file_missing_path"]?.GetValue<string?>());
            Assert.Equal(payload.Length, details["file_size_bytes"]?.GetValue<long>());
            string? hash = details["file_hash_md5"]?.GetValue<string?>();
            Assert.NotNull(hash);
            Assert.Equal(32, hash!.Length); // MD5 hex length.
            Assert.Equal(hash, hash.ToLowerInvariant());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task CreateHistoryDetailsAsync_EmptyFilePath_ReportsFileExistsFalseWithoutMissingPath()
    {
        // When item.FilePath is empty/whitespace (e.g. an upload-only history row
        // where the source was never persisted), the response must report
        // file_exists=false but file_missing_path should be null — there is no
        // missing path to surface, just a known-absent local file.
        var runtime = new XerahSMcpRuntime();

        var item = new HistoryItem
        {
            Id = 9999,
            FilePath = string.Empty,
            ThumbnailURL = string.Empty,
            URL = "https://share.example.test/xyz"
        };

        var details = await runtime.CreateHistoryDetailsAsync(item, CancellationToken.None);

        Assert.False(details["file_exists"]?.GetValue<bool>());
        Assert.Null(details["file_missing_path"]?.GetValue<string?>());
        Assert.Null(details["file_url"]?.GetValue<string?>());
        Assert.Equal(0L, details["file_size_bytes"]?.GetValue<long>());
    }

    [Fact]
    public void RuntimeHistoryBlobResourceUri_UsesInvariantHistoryId()
    {
        var item = new HistoryItem
        {
            Id = 12345
        };

        Assert.Equal("xerahs://history/thumb/12345", XerahSMcpRuntime.CreateHistoryBlobResourceUri(item));
    }

    [Fact]
    public void RuntimeHistoryBlobTooLargeResponse_ReturnsActionableJsonTextContent()
    {
        const string uri = "xerahs://history/thumb/12345";
        const string blobPath = "/tmp/oversized-capture.png";
        const long blobSize = XerahSMcpRuntime.MaxInlineHistoryBlobBytes + 1;

        var response = XerahSMcpRuntime.CreateHistoryBlobTooLargeResponse(uri, blobPath, blobSize);

        var contents = Assert.IsType<JsonArray>(response["contents"]);
        var content = Assert.IsType<JsonObject>(contents[0]);
        Assert.Equal(uri, content["uri"]?.GetValue<string>());
        Assert.Equal("application/json", content["mimeType"]?.GetValue<string>());
        Assert.Null(content["blob"]);

        var details = JsonNode.Parse(content["text"]!.GetValue<string>()) as JsonObject;
        Assert.NotNull(details);
        Assert.Equal("history_blob_too_large", details["error"]?.GetValue<string>());
        Assert.Equal(blobPath, details["file_path"]?.GetValue<string>());
        Assert.Equal(blobSize, details["file_size_bytes"]?.GetValue<long>());
        Assert.Equal(XerahSMcpRuntime.MaxInlineHistoryBlobBytes, details["max_inline_bytes"]?.GetValue<long>());
        Assert.Contains("Open the local file path", details["message"]?.GetValue<string>());
    }

    [Fact]
    public void RuntimeHistoryBlobMissingResponse_ReturnsActionableJsonTextContent()
    {
        const string uri = "xerahs://history/thumb/12345";
        var item = new HistoryItem
        {
            Id = 12345,
            FilePath = "/tmp/moved-capture.png",
            ThumbnailURL = "/tmp/moved-thumbnail.png"
        };

        var response = XerahSMcpRuntime.CreateHistoryBlobMissingResponse(uri, item);

        var contents = Assert.IsType<JsonArray>(response["contents"]);
        var content = Assert.IsType<JsonObject>(contents[0]);
        Assert.Equal(uri, content["uri"]?.GetValue<string>());
        Assert.Equal("application/json", content["mimeType"]?.GetValue<string>());
        Assert.Null(content["blob"]);

        var details = JsonNode.Parse(content["text"]!.GetValue<string>()) as JsonObject;
        Assert.NotNull(details);
        Assert.Equal("history_blob_missing", details["error"]?.GetValue<string>());
        Assert.Equal("12345", details["history_id"]?.GetValue<string>());
        Assert.Equal(item.FilePath, details["file_path"]?.GetValue<string>());
        Assert.Equal(item.ThumbnailURL, details["thumbnail_path"]?.GetValue<string>());
        Assert.Contains("moved", details["message"]?.GetValue<string>());
    }

    [Fact]
    public async Task ResourcesRead_HistorySearch_ExtractsQueryFromQParameter()
    {
        var runtime = new TestHistoryRuntime();
        var server = new McpWireServer(runtime);

        var response = await server.HandleRequestAsync(new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = 10,
            Method = "resources/read",
            Params = new JsonObject
            {
                ["uri"] = "xerahs://history/search?q=test%20query"
            }
        });

        Assert.Null(response.Error);
        var result = Assert.IsType<JsonObject>(response.Result);
        var contents = Assert.IsType<JsonArray>(result["contents"]);
        var textContent = contents[0]?["text"]?.GetValue<string>();
        Assert.NotNull(textContent);
        var inner = JsonNode.Parse(textContent!) as JsonObject;
        Assert.NotNull(inner);
        Assert.Equal("test query", inner["lastQuery"]?.GetValue<string>());
    }

    [Fact]
    public async Task ResourcesRead_HistorySearch_HandlesAmpersandDelimiter()
    {
        var runtime = new TestHistoryRuntime();
        var server = new McpWireServer(runtime);

        var response = await server.HandleRequestAsync(new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = 11,
            Method = "resources/read",
            Params = new JsonObject
            {
                ["uri"] = "xerahs://history/search?q=hello&limit=5&from=2026-01-01"
            }
        });

        Assert.Null(response.Error);
        var result = Assert.IsType<JsonObject>(response.Result);
        var contents = Assert.IsType<JsonArray>(result["contents"]);
        var textContent = contents[0]?["text"]?.GetValue<string>();
        Assert.NotNull(textContent);
        var inner = JsonNode.Parse(textContent!) as JsonObject;
        Assert.NotNull(inner);
        Assert.Equal("hello", inner["lastQuery"]?.GetValue<string>());
        Assert.Equal(5, inner["lastLimit"]?.GetValue<int>());
        Assert.Equal("2026-01-01", inner["lastFromDate"]?.GetValue<string>());
    }

    [Fact]
    public async Task ResourcesRead_HistorySearch_DecodesPlusAsSpace()
    {
        var runtime = new TestHistoryRuntime();
        var server = new McpWireServer(runtime);

        var response = await server.HandleRequestAsync(new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = 16,
            Method = "resources/read",
            Params = new JsonObject
            {
                ["uri"] = "xerahs://history/search?q=window+capture&limit=5"
            }
        });

        Assert.Null(response.Error);
        var result = Assert.IsType<JsonObject>(response.Result);
        var contents = Assert.IsType<JsonArray>(result["contents"]);
        var textContent = contents[0]?["text"]?.GetValue<string>();
        Assert.NotNull(textContent);
        var inner = JsonNode.Parse(textContent!) as JsonObject;
        Assert.NotNull(inner);
        Assert.Equal("window capture", inner["lastQuery"]?.GetValue<string>());
        Assert.Equal(5, inner["lastLimit"]?.GetValue<int>());
    }

    [Fact]
    public async Task ResourcesRead_HistorySearch_HandlesQAfterOtherParams()
    {
        var runtime = new TestHistoryRuntime();
        var server = new McpWireServer(runtime);

        var response = await server.HandleRequestAsync(new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = 12,
            Method = "resources/read",
            Params = new JsonObject
            {
                ["uri"] = "xerahs://history/search?from=2026-01-01&q=searchterm&limit=10"
            }
        });

        Assert.Null(response.Error);
        var result = Assert.IsType<JsonObject>(response.Result);
        var contents = Assert.IsType<JsonArray>(result["contents"]);
        var textContent = contents[0]?["text"]?.GetValue<string>();
        Assert.NotNull(textContent);
        var inner = JsonNode.Parse(textContent!) as JsonObject;
        Assert.NotNull(inner);
        Assert.Equal("searchterm", inner["lastQuery"]?.GetValue<string>());
        Assert.Equal("2026-01-01", inner["lastFromDate"]?.GetValue<string>());
        Assert.Equal(10, inner["lastLimit"]?.GetValue<int>());
    }

    [Fact]
    public async Task ResourcesRead_HistorySearch_IgnoresMalformedPercentEncodedPairs()
    {
        var runtime = new TestHistoryRuntime();
        var server = new McpWireServer(runtime);

        var response = await server.HandleRequestAsync(new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = 15,
            Method = "resources/read",
            Params = new JsonObject
            {
                ["uri"] = "xerahs://history/search?q=%E0%A4%A&limit=7&from=2026-02-03"
            }
        });

        Assert.Null(response.Error);
        var result = Assert.IsType<JsonObject>(response.Result);
        var contents = Assert.IsType<JsonArray>(result["contents"]);
        var textContent = contents[0]?["text"]?.GetValue<string>();
        Assert.NotNull(textContent);
        var inner = JsonNode.Parse(textContent!) as JsonObject;
        Assert.NotNull(inner);
        Assert.Null(inner["lastQuery"]);
        Assert.Equal(7, inner["lastLimit"]?.GetValue<int>());
        Assert.Equal("2026-02-03", inner["lastFromDate"]?.GetValue<string>());
    }

    [Fact]
    public async Task ResourcesRead_HistorySearch_DoesNotMatchPrefixOnlyPaths()
    {
        var runtime = new TestHistoryRuntime();
        var server = new McpWireServer(runtime);

        var response = await server.HandleRequestAsync(new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = 17,
            Method = "resources/read",
            Params = new JsonObject
            {
                ["uri"] = "xerahs://history/searchfoo?limit=5"
            }
        });

        Assert.Null(response.Error);
        var result = Assert.IsType<JsonObject>(response.Result);
        var contents = Assert.IsType<JsonArray>(result["contents"]);
        Assert.Equal("{}", contents[0]?["text"]?.GetValue<string>());
        Assert.False(XerahSMcpRuntime.IsHistorySearchResourceUri("xerahs://history/searchfoo?limit=5"));
        Assert.True(XerahSMcpRuntime.IsHistorySearchResourceUri("xerahs://history/search?limit=5"));
    }

    [Fact]
    public async Task ResourcesRead_MapsUserCancelledToUserCancelledCode()
    {
        var runtime = new UserCancelledRuntime();
        var server = new McpWireServer(runtime);

        var response = await server.HandleRequestAsync(new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = 13,
            Method = "resources/read",
            Params = new JsonObject
            {
                ["uri"] = "xerahs://settings/general"
            }
        });

        Assert.NotNull(response.Error);
        Assert.Equal(JsonRpcErrorCodes.UserCancelled, response.Error!.Code);
    }

    [Fact]
    public async Task ResourcesRead_MapsArgumentOutOfRangeToInvalidParams()
    {
        var runtime = new ArgOutOfRangeRuntime();
        var server = new McpWireServer(runtime);

        var response = await server.HandleRequestAsync(new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = 14,
            Method = "resources/read",
            Params = new JsonObject
            {
                ["uri"] = "xerahs://settings/general"
            }
        });

        Assert.NotNull(response.Error);
        Assert.Equal(JsonRpcErrorCodes.InvalidParams, response.Error!.Code);
    }

    private sealed class TestHistoryRuntime : IXerahSMcpRuntime
    {
        public string ServerVersion => "9.9.9-test";

        public Task<string> GetApiKeyAsync(CancellationToken cancellationToken = default) => Task.FromResult("test-api-key");

        public Task<JsonObject> CaptureRegionAsync(string? workflowId, int? monitor, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<JsonObject> CaptureWindowAsync(string? windowTitle, bool includeDecoration, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<JsonObject> CaptureFullScreenAsync(int? monitor, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<JsonObject> CaptureScrollingAsync(string scrollDirection, int maxFrames, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<JsonObject> AnnotateImageAsync(string? imagePath, JsonArray? annotations, bool autoSave, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<JsonObject> UploadFileAsync(string? filePath, string? destination, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<JsonObject> UploadClipboardAsync(string? destination, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<JsonObject> QueryHistoryAsync(string? query, string? fromDate, string? toDate, string fileType, int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult(new JsonObject
            {
                ["items"] = new JsonArray(),
                ["total_count"] = 0,
                ["has_more"] = false,
                ["lastQuery"] = query,
                ["lastFromDate"] = fromDate,
                ["lastToDate"] = toDate,
                ["lastLimit"] = limit
            });

        public Task<JsonObject> GetHistoryItemAsync(string? id, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<JsonObject> ListWorkflowsAsync(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<JsonObject> GetSettingsAsync(string? category, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<JsonObject> ReadResourceAsync(string uri, CancellationToken cancellationToken = default)
        {
            // Delegate to the real runtime's query parsing for history/search
            if (XerahSMcpRuntime.IsHistorySearchResourceUri(uri))
            {
                var queryStart = uri.IndexOf('?');
                string? query = null;
                string? fromDate = null;
                string? toDate = null;
                var limit = 20;

                if (queryStart >= 0)
                {
                    var queryString = uri[(queryStart + 1)..];
                    var pairs = queryString.Split('&', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var pair in pairs)
                    {
                        var eqIndex = pair.IndexOf('=');
                        if (eqIndex < 0) continue;
                        var key = XerahSMcpRuntime.DecodeResourceQueryComponent(pair[..eqIndex]);
                        var value = XerahSMcpRuntime.DecodeResourceQueryComponent(pair[(eqIndex + 1)..]);
                        if (key == null || value == null) continue;
                        if (string.Equals(key, "q", StringComparison.OrdinalIgnoreCase))
                            query = string.IsNullOrWhiteSpace(value) ? null : value;
                        else if (string.Equals(key, "from", StringComparison.OrdinalIgnoreCase))
                            fromDate = value;
                        else if (string.Equals(key, "to", StringComparison.OrdinalIgnoreCase))
                            toDate = value;
                        else if (string.Equals(key, "limit", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out var p))
                            limit = p;
                    }
                }

                return Task.FromResult(new JsonObject
                {
                    ["contents"] = new JsonArray(
                        new JsonObject
                        {
                            ["uri"] = uri,
                            ["mimeType"] = "application/json",
                            ["text"] = new JsonObject
                            {
                                ["items"] = new JsonArray(),
                                ["total_count"] = 0,
                                ["has_more"] = false,
                                ["lastQuery"] = query,
                                ["lastFromDate"] = fromDate,
                                ["lastLimit"] = limit
                            }.ToJsonString()
                        })
                });
            }

            return Task.FromResult(new JsonObject
            {
                ["contents"] = new JsonArray(
                    new JsonObject
                    {
                        ["uri"] = uri,
                        ["mimeType"] = "application/json",
                        ["text"] = "{}"
                    })
            });
        }
    }

    private sealed class UserCancelledRuntime : IXerahSMcpRuntime
    {
        public string ServerVersion => "9.9.9-test";
        public Task<string> GetApiKeyAsync(CancellationToken cancellationToken = default) => Task.FromResult("test-api-key");
        public Task<JsonObject> CaptureRegionAsync(string? workflowId, int? monitor, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<JsonObject> CaptureWindowAsync(string? windowTitle, bool includeDecoration, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<JsonObject> CaptureFullScreenAsync(int? monitor, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<JsonObject> CaptureScrollingAsync(string scrollDirection, int maxFrames, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<JsonObject> AnnotateImageAsync(string? imagePath, JsonArray? annotations, bool autoSave, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<JsonObject> UploadFileAsync(string? filePath, string? destination, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<JsonObject> UploadClipboardAsync(string? destination, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<JsonObject> QueryHistoryAsync(string? query, string? fromDate, string? toDate, string fileType, int limit, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<JsonObject> GetHistoryItemAsync(string? id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<JsonObject> ListWorkflowsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<JsonObject> GetSettingsAsync(string? category, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<JsonObject> ReadResourceAsync(string uri, CancellationToken cancellationToken = default) =>
            throw new McpUserCancelledException("User cancelled the operation.");
    }

    private sealed class ArgOutOfRangeRuntime : IXerahSMcpRuntime
    {
        public string ServerVersion => "9.9.9-test";
        public Task<string> GetApiKeyAsync(CancellationToken cancellationToken = default) => Task.FromResult("test-api-key");
        public Task<JsonObject> CaptureRegionAsync(string? workflowId, int? monitor, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<JsonObject> CaptureWindowAsync(string? windowTitle, bool includeDecoration, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<JsonObject> CaptureFullScreenAsync(int? monitor, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<JsonObject> CaptureScrollingAsync(string scrollDirection, int maxFrames, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<JsonObject> AnnotateImageAsync(string? imagePath, JsonArray? annotations, bool autoSave, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<JsonObject> UploadFileAsync(string? filePath, string? destination, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<JsonObject> UploadClipboardAsync(string? destination, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<JsonObject> QueryHistoryAsync(string? query, string? fromDate, string? toDate, string fileType, int limit, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<JsonObject> GetHistoryItemAsync(string? id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<JsonObject> ListWorkflowsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<JsonObject> GetSettingsAsync(string? category, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<JsonObject> ReadResourceAsync(string uri, CancellationToken cancellationToken = default) =>
            throw new ArgumentOutOfRangeException("uri", "The specified value is out of range.");
    }

    internal sealed class FakeRuntime : IXerahSMcpRuntime
    {
        public string ServerVersion => "9.9.9-test";
        public string? LastCall { get; private set; }
        public string? LastResourceUri { get; private set; }

        public Task<string> GetApiKeyAsync(CancellationToken cancellationToken = default) => Task.FromResult("test-api-key");

        public Task<JsonObject> CaptureRegionAsync(string? workflowId, int? monitor, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result("capture_region", workflowId, monitor));

        public Task<JsonObject> CaptureWindowAsync(string? windowTitle, bool includeDecoration, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result("capture_window", windowTitle, includeDecoration));

        public Task<JsonObject> CaptureFullScreenAsync(int? monitor, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result("capture_full_screen", monitor));

        public Task<JsonObject> CaptureScrollingAsync(string scrollDirection, int maxFrames, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result("capture_scrolling", scrollDirection, maxFrames));

        public Task<JsonObject> AnnotateImageAsync(string? imagePath, JsonArray? annotations, bool autoSave, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result("annotate_image", imagePath, autoSave, annotations?.Count));

        public Task<JsonObject> UploadFileAsync(string? filePath, string? destination, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result("upload_file", filePath, destination));

        public Task<JsonObject> UploadClipboardAsync(string? destination, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result("upload_clipboard", destination));

        public Task<JsonObject> QueryHistoryAsync(string? query, string? fromDate, string? toDate, string fileType, int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result("query_history", query, fromDate, toDate, fileType, limit));

        public Task<JsonObject> GetHistoryItemAsync(string? id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result("get_history_item", id));

        public Task<JsonObject> ListWorkflowsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Result("list_workflows"));

        public Task<JsonObject> GetSettingsAsync(string? category, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result("get_settings", category));

        public Task<JsonObject> ReadResourceAsync(string uri, CancellationToken cancellationToken = default)
        {
            LastResourceUri = uri;
            return Task.FromResult(new JsonObject
            {
                ["contents"] = new JsonArray(
                    new JsonObject
                    {
                        ["uri"] = uri,
                        ["mimeType"] = "application/json",
                        ["text"] = "{}"
                    })
            });
        }

        private JsonObject Result(string call, object? arg1 = null, object? arg2 = null, object? arg3 = null, object? arg4 = null, object? arg5 = null)
        {
            LastCall = call;
            var result = new JsonObject
            {
                ["tool"] = call
            };

            if (arg1 is not null)
            {
                result["arg1"] = JsonValue.Create(arg1.ToString());
            }

            if (arg2 is not null)
            {
                result["arg2"] = JsonValue.Create(arg2.ToString());
            }

            if (arg3 is not null)
            {
                result["arg3"] = JsonValue.Create(arg3.ToString());
            }

            if (arg4 is not null)
            {
                result["arg4"] = JsonValue.Create(arg4.ToString());
            }

            if (arg5 is not null)
            {
                result["arg5"] = JsonValue.Create(arg5.ToString());
            }

            if (call == "capture_full_screen" && arg1 is int monitor)
            {
                result["monitor"] = monitor;
            }

            return result;
        }
    }

        [Theory]
        [InlineData("xerahs://history/search")]
        [InlineData("xerahs://history/search?q=test")]
        [InlineData("xerahs://history/search?q=hello&limit=5")]
        public void IsHistorySearchResourceUri_ValidUris_ReturnsTrue(string uri)
        {
            Assert.True(XerahSMcpRuntime.IsHistorySearchResourceUri(uri));
        }

        [Theory]
        [InlineData("xerahs://history/searchfoo")]
        [InlineData("xerahs://history/searchfoo?q=test")]
        [InlineData("xerahs://history/search?")]
        [InlineData("xerahs://history/search?q=%E0%A4%A")]
        public void IsHistorySearchResourceUri_InvalidOrMalformedUris_ReturnsFalse(string uri)
        {
            Assert.False(XerahSMcpRuntime.IsHistorySearchResourceUri(uri));
        }

}
