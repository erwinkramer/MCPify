using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.Text.Json;

namespace MCPify.Tools;

public class SimpleMathTool : McpServerTool
{
    public override Tool ProtocolTool => new()
    {
        Name = "test_add",
        Description = "A test tool that adds two integers together.",
        InputSchema = GetSchema()
    };

    public override IReadOnlyList<object> Metadata => Array.Empty<object>();

    public override ValueTask<CallToolResult> InvokeAsync(
        RequestContext<CallToolRequestParams> context,
        CancellationToken token)
    {
        var args = context.Params?.Arguments;

        if (args is null ||
            !args.TryGetValue("a", out var aJson) ||
            !args.TryGetValue("b", out var bJson))
        {
            return new ValueTask<CallToolResult>(new CallToolResult
            {
                IsError = true,
                Content = new[] { new TextContentBlock { Text = "Missing arguments 'a' or 'b'." } }
            });
        }

        try
        {
            int a = aJson.GetInt32();
            int b = bJson.GetInt32();
            int result = a + b;

            return new ValueTask<CallToolResult>(new CallToolResult
            {
                Content = new[] { new TextContentBlock { Text = $"The sum is: {result}" } }
            });
        }
        catch
        {
            return new ValueTask<CallToolResult>(new CallToolResult
            {
                IsError = true,
                Content = new[] { new TextContentBlock { Text = "Invalid arguments. expected integers." } }
            });
        }
    }

    private static JsonElement GetSchema()
    {
        var schema = new
        {
            type = "object",
            properties = new
            {
                a = new { type = "integer", description = "First number" },
                b = new { type = "integer", description = "Second number" }
            },
            required = new[] { "a", "b" }
        };
        return JsonSerializer.SerializeToElement(schema);
    }
}