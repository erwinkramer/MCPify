using System.Text.Json;
using System.Text.Json.Nodes;
using MCPify.Core;
using MCPify.Core.Session;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace MCPify.Hosting;

/// <summary>
/// Decorates an McpServerTool to ensure a Session Context exists before execution.
/// This is crucial for Stdio transport where ASP.NET Core middleware does not run.
/// </summary>
public class SessionAwareToolDecorator : McpServerTool
{
    private readonly McpServerTool _innerTool;
    private readonly IServiceProvider _serviceProvider;

    public SessionAwareToolDecorator(McpServerTool innerTool, IServiceProvider serviceProvider)
    {
        _innerTool = innerTool;
        _serviceProvider = serviceProvider;
    }

    // Delegate ProtocolTool property to the inner tool but inject sessionId into schema
    public override Tool ProtocolTool
    {
        get
        {
            var original = _innerTool.ProtocolTool;

            // Skip modification for 'connect' tool as it doesn't need a session ID
            if (original.Name.Equals("connect", StringComparison.OrdinalIgnoreCase))
            {
                return original;
            }
            
            // If the schema is empty or not an object, just return original to be safe
            if (original.InputSchema.ValueKind == JsonValueKind.Undefined || 
                original.InputSchema.ValueKind == JsonValueKind.Null)
            {
                 return original;
            }

            try
            {
                // Parse the original schema to a mutable Node
                var jsonNode = JsonNode.Parse(original.InputSchema.GetRawText());
                if (jsonNode is JsonObject jsonObj)
                {
                    // Ensure 'type' is set to 'object' (Required for valid MCP schema)
                    if (!jsonObj.ContainsKey("type"))
                    {
                        jsonObj["type"] = "object";
                    }

                    // Ensure 'properties' object exists
                    if (!jsonObj.ContainsKey("properties"))
                    {
                        jsonObj["properties"] = new JsonObject();
                    }
                    
                    var properties = jsonObj["properties"] as JsonObject;
                    if (properties != null && !properties.ContainsKey("sessionId"))
                    {
                        // Inject sessionId property
                        properties["sessionId"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "The Session ID to maintain context across requests. Retrieve this via 'connect' tool."
                        };
                    }
                    
                    // We return a NEW Tool object with the modified schema
                    return new Tool
                    {
                        Name = original.Name,
                        Description = original.Description,
                        InputSchema = JsonSerializer.SerializeToElement(jsonObj)
                    };
                }
            }
            catch
            {
                // Fallback if parsing fails
            }

            return original;
        }
    }

    // Delegate Metadata to the inner tool
    public override IReadOnlyList<object> Metadata => _innerTool.Metadata;

    public override async ValueTask<CallToolResult> InvokeAsync(RequestContext<CallToolRequestParams> context, CancellationToken token)
    {
        // Bypass for 'connect' tool - it establishes the session
        if (_innerTool.ProtocolTool.Name.Equals("connect", StringComparison.OrdinalIgnoreCase))
        {
            return await _innerTool.InvokeAsync(context, token);
        }

        // Ensure the RequestContext has Services (it should, but safety first)
        var services = context.Services ?? _serviceProvider;
        var accessor = services.GetService<IMcpContextAccessor>();
        var httpContextAccessor = services.GetService<IHttpContextAccessor>();

        if (accessor != null)
        {
            // 1. Try to get from arguments first (Client MUST provide this)
            if (context.Params?.Arguments != null)
            {
                // Case-insensitive lookup. FirstOrDefault returns default(KeyValuePair) if not found.
                var argEntry = context.Params.Arguments.FirstOrDefault(x => x.Key.Equals("sessionId", StringComparison.OrdinalIgnoreCase));
                
                // Check if we actually found a key (Key will be non-null)
                if (argEntry.Key != null)
                {
                    if (argEntry.Value.ValueKind == JsonValueKind.String)
                    {
                        accessor.SessionId = argEntry.Value.GetString();
                    }
                    else
                    {
                         // Fallback for other types (e.g. number/boolean) if user sent weird data
                         accessor.SessionId = argEntry.Value.ToString();
                    }
                }
            }

            // 2. HTTP Fallback: Check Cookies/Headers (for web clients)
            if (string.IsNullOrEmpty(accessor.SessionId) && httpContextAccessor?.HttpContext != null)
            {
                // Accessor might already be populated by Middleware
            }
            
            // 3. STRICT ENFORCEMENT for Non-HTTP (Stdio)
            if (string.IsNullOrEmpty(accessor.SessionId) && httpContextAccessor?.HttpContext == null)
            {
                 return new CallToolResult 
                 { 
                     IsError = true,
                     Content = new[] { new TextContentBlock { Text = "Session ID is required. You must call the 'connect' tool first to obtain a Session ID, and then provide it in the 'sessionId' argument for all subsequent calls." } }
                 };
            }
            
            // Resolve the actual Principal if we have a Handle (for Lazy Auth support)
            var sessionMap = services.GetService<ISessionMap>();
            if (sessionMap != null && !string.IsNullOrEmpty(accessor.SessionId))
            {
                accessor.SessionId = sessionMap.ResolvePrincipal(accessor.SessionId);
            }

            // Ensure AsyncLocal is populated so deep services can access it
            McpContextAccessor.CurrentContext = new McpContextAccessor.McpContext
            {
                SessionId = accessor.SessionId,
                ConnectionId = accessor.ConnectionId,
                AccessToken = accessor.AccessToken
            };
        }

        try
        {
            return await _innerTool.InvokeAsync(context, token);
        }
        finally
        {
            McpContextAccessor.CurrentContext = null;
        }
    }
}