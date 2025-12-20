using MCPify.Core;
using MCPify.Core.Session;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace MCPify.Hosting;

public class McpContextMiddleware
{
    private readonly RequestDelegate _next;

    public McpContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext httpContext, IMcpContextAccessor accessor, McpifyOptions options, ISessionMap sessionMap)
    {
        // 1. Try custom resolver from options
        string? sessionHandle = null;
        if (options.SessionIdResolver != null)
        {
            sessionHandle = options.SessionIdResolver(httpContext);
        }

        // 2. If null, try to retrieve sessionId from HttpContext.Items (Standard MCP Transport fallback)
        if (string.IsNullOrEmpty(sessionHandle) && httpContext.Items.TryGetValue("McpSessionId", out var sessionIdObj) && sessionIdObj is string sessionIdItem)
        {
            sessionHandle = sessionIdItem;
        }

        // 3. Cookie Fallback: Check for McpSessionId cookie
        if (string.IsNullOrEmpty(sessionHandle) && httpContext.Request.Cookies.TryGetValue("McpSessionId", out var cookieSessionId))
        {
            sessionHandle = cookieSessionId;
        }
        
        // 4. If still null, generate a Temporary In-Memory Session Handle
        if (string.IsNullOrEmpty(sessionHandle))
        {
             sessionHandle = Guid.NewGuid().ToString("N");
             // Store it back so downstream can see it if needed
             httpContext.Items["McpSessionId"] = sessionHandle;

             // Auto-Issue Cookie for HTTP clients
             httpContext.Response.Cookies.Append("McpSessionId", sessionHandle, new CookieOptions
             {
                 HttpOnly = true,
                 Secure = httpContext.Request.IsHttps,
                 SameSite = SameSiteMode.Lax,
                 Expires = DateTimeOffset.UtcNow.AddDays(7)
             });
        }

        // Store the handle specifically for the Login/Upgrade process
        httpContext.Items["McpSessionHandle"] = sessionHandle;

        // 4. Resolve the actual identity (Principal) from the map
        // If logged in, this returns the User ID. If not, it returns the sessionHandle (Temp ID).
        accessor.SessionId = sessionMap.ResolvePrincipal(sessionHandle);

        if (httpContext.Items.TryGetValue("McpConnectionId", out var connectionIdObj) && connectionIdObj is string connectionId)
        {
            accessor.ConnectionId = connectionId;
        }

        // Set the current context for AsyncLocal to ensure it's available downstream
        McpContextAccessor.CurrentContext = new McpContextAccessor.McpContext
        {
            SessionId = accessor.SessionId,
            ConnectionId = accessor.ConnectionId,
            AccessToken = accessor.AccessToken
        };

        try
        {
            await _next(httpContext);
        }
        finally
        {
            // Clear the context to prevent leakage to other requests
            McpContextAccessor.CurrentContext = null;
        }
    }
}