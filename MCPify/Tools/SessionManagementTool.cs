using MCPify.Core;
using MCPify.Core.Session;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.Text.Json;

namespace MCPify.Tools;

public class SessionManagementTool : McpServerTool
{
    public override Tool ProtocolTool => new()
    {
        Name = "connect",
        Description = "Establish a new session with the server. Returns a unique Session ID. You MUST provide this Session ID in the 'sessionId' argument for ALL subsequent tool calls (e.g., login, fetch_secret) to maintain your authentication state.",
        InputSchema = JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {}
        }
        """).RootElement
    };

    public override IReadOnlyList<object> Metadata => Array.Empty<object>();

    public override async ValueTask<CallToolResult> InvokeAsync(RequestContext<CallToolRequestParams> context, CancellationToken token)
    {
        var accessor = context.Services?.GetService<IMcpContextAccessor>();
        
        // Always generate a NEW session on connect to ensure clean state
        string sessionId = Guid.NewGuid().ToString("N");

        if (accessor != null)
        {
             accessor.SessionId = sessionId;
        }

        return new CallToolResult
        {
            Content = new[] { new TextContentBlock { Text = sessionId } }
        };
    }

    private static CallToolResult Error(string message) => new()
    {
        IsError = true,
        Content = new[] { new TextContentBlock { Text = message } }
    };
}
