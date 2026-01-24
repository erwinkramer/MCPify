# MCPify

[![NuGet](https://img.shields.io/nuget/v/MCPify.svg)](https://www.nuget.org/packages/MCPify/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

**MCPify** is a .NET library that bridges the gap between your existing ASP.NET Core APIs (or external OpenAPI/Swagger specs) and the [Model Context Protocol (MCP)](https://modelcontextprotocol.io/). It allows you to expose API operations as MCP tools that can be consumed by AI assistants like Claude Desktop, ensuring seamless integration with your existing services.

> **Latest Release:** v0.0.12 - Now with enhanced OAuth middleware and improved testing infrastructure!

## What's New

### v0.0.12 (Latest - Jan 23, 2026)
-   **Enhanced OAuth Middleware**: Improved OAuth authentication middleware with better error handling and token management (#17)
-   **JWT Token Validation**: Full support for JWT access token validation including expiration, audience, and scope verification (#15)
-   **Per-Tool Scope Requirements**: Define granular scope requirements for specific tools using pattern matching (#15)
-   **Automatic Scope Discovery**: Scopes are automatically extracted from OpenAPI security schemes and enforced during validation (#15)
-   **WWW-Authenticate Header**: Improved WWW-Authenticate header to include scope parameter per MCP spec
-   **LoginBrowserBehavior**: Control browser launch behavior for OAuth login in headless environments
-   **OAuth2Configuration List**: Support for multiple OAuth providers with AuthorizationServers exposure (#13)

## Features

-   **Automatic Tool Generation**: Dynamically converts OpenAPI (Swagger) v2/v3 definitions into MCP tools.
-   **Hybrid Support**: Expose your **local** ASP.NET Core endpoints and **external** public APIs simultaneously.
-   **Seamless Authentication**: Built-in support for OAuth 2.0 Authorization Code Flow with PKCE.
    -   Includes a `login_auth_code_pkce` tool that handles the browser-based login flow automatically.
    -   Securely stores tokens per session using encrypted local storage.
    -   Automatically refreshes tokens when they expire.
-   **MCP Authorization Spec Compliant**: Full compliance with the [MCP Authorization Specification](https://modelcontextprotocol.io/specification/2025-11-25/basic/authorization).
    -   Protected Resource Metadata (`/.well-known/oauth-protected-resource`)
    -   RFC 8707 Resource Parameter support
    -   JWT token validation (expiration, audience, scopes)
    -   403 Forbidden with `insufficient_scope` error
-   **Dual Transport**: Supports both `Stdio` (for local desktop apps like Claude) and `Http` (SSE) transports.
-   **Production Ready**: Robust logging, error handling, and configurable options.

## Supported Frameworks

-   .NET 8, .NET 9, .NET 10

## Quick Start

### 1. Installation

Install the package into your ASP.NET Core project:

```bash
dotnet add package MCPify
```

### 2. Configuration

Add MCPify to your `Program.cs`:

```csharp
using MCPify.Hosting;

var builder = WebApplication.CreateBuilder(args);

// ... Add other services ...

// Add MCPify services
builder.Services.AddMcpify(options =>
{
    // Stdio for local tools (Claude Desktop), Http for remote servers
    options.Transport = McpTransportType.Stdio;

    // Option A: Expose Local Endpoints
    options.LocalEndpoints = new LocalEndpointsOptions
    {
        Enabled = true,
        ToolPrefix = "myapp_",
        BaseUrlOverride = "https://localhost:5001",  // Optional: override base URL
        Filter = op => op.Route.StartsWith("/api"),  // Optional: filter endpoints
        AuthenticationFactory = sp => sp.GetRequiredService<OAuthAuthorizationCodeAuthentication>()  // Optional
    };

    // Option B: Expose External APIs from URL
    options.ExternalApis.Add(new ExternalApiOptions
    {
        ApiBaseUrl = "https://petstore.swagger.io/v2",
        OpenApiUrl = "https://petstore.swagger.io/v2/swagger.json",
        ToolPrefix = "petstore_"
    });

    // Option C: Expose External APIs from Local File
    options.ExternalApis.Add(new ExternalApiOptions
    {
        ApiBaseUrl = "https://api.example.com",
        OpenApiFilePath = "path/to/openapi-spec.json",  // or .yaml
        ToolPrefix = "myapi_"
    });
});

var app = builder.Build();

// Add Middleware (order matters!)
app.UseMcpifyContext();           // Must be first
app.UseMcpifyOAuth();             // Must come before UseAuthentication
app.UseAuthentication();          // If using ASP.NET Core auth
app.UseAuthorization();

// ... Map your endpoints ...

// Map the MCP endpoint (required for Http transport)
app.MapMcpifyEndpoint();

app.Run();
```

### 3. Usage with Claude Desktop

To use your MCPify app with [Claude Desktop](https://claude.ai/download), edit your config file (`%APPDATA%\Claude\claude_desktop_config.json` on Windows or `~/Library/Application Support/Claude/claude_desktop_config.json` on macOS):

```json
{
  "mcpServers": {
    "my-app": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "/absolute/path/to/YourProject.csproj", 
        "--",
        "--Mcpify:Transport=Stdio"
      ]
    }
  }
}
```

> **Note:** When using `dotnet run`, ensure your application does not print build logs to stdout, as this corrupts the MCP JSON-RPC protocol. You can suppress logs or publish your app as a single-file executable for a cleaner setup.

## Authentication

MCPify provides comprehensive OAuth 2.0 authentication support with automatic token management, validation, and scope enforcement.

### Enabling OAuth

Register the authentication provider in your `Program.cs` (ensure this is done before calling `AddMcpify`):

```csharp
services.AddScoped<OAuthAuthorizationCodeAuthentication>(sp => {
    return new OAuthAuthorizationCodeAuthentication(
        clientId: "your-client-id",
        authorizationEndpoint: "https://auth.example.com/authorize",
        tokenEndpoint: "https://auth.example.com/token",
        scope: "api_access",
        secureTokenStore: sp.GetRequiredService<ISecureTokenStore>(),
        mcpContextAccessor: sp.GetRequiredService<IMcpContextAccessor>(),
        redirectUri: "http://localhost:5000/auth/callback" // Your app must handle this
    );
});

// Register the Login Tool
services.AddLoginTool(sp => new LoginTool());
```

### The Login Flow

1.  The user asks Claude: *"Please login"* or uses a tool that requires auth.
2.  Claude calls the `login_auth_code_pkce` tool.
3.  MCPify automatically opens the system browser to the login page (in interactive environments).
4.  The user logs in and approves the request.
5.  The browser redirects back to your application (e.g., `/auth/callback`).
6.  Your app saves the token and displays a success message.
7.  The `login_auth_code_pkce` tool detects the successful login and reports back to Claude.
8.  Claude can now invoke authenticated tools!

### Headless / Remote Environments

When running MCPify on headless servers, containers, or remote environments where a browser cannot be opened, you can configure the login behavior to skip browser launch attempts and immediately return the authorization URL:

```csharp
builder.Services.AddMcpify(options =>
{
    // For headless/remote environments - return URL immediately without browser launch
    options.LoginBrowserBehavior = BrowserLaunchBehavior.Never;
});
```

Available options for `LoginBrowserBehavior`:

| Value | Description |
|-------|-------------|
| `Auto` (default) | Automatically detects headless environments (no DISPLAY on Linux, SSH sessions, containers) and skips browser launch when appropriate. |
| `Always` | Always attempt to open the browser, regardless of environment. |
| `Never` | Never attempt to open the browser. Returns the authorization URL immediately for manual authentication. Ideal for headless servers and containers. |

With `Auto` mode, MCPify detects headless environments by checking:
-   **Linux**: Missing `DISPLAY` or `WAYLAND_DISPLAY` environment variables, SSH sessions without X forwarding, Docker containers
-   **Windows**: Container environments (Kubernetes, Docker)
-   **macOS**: SSH sessions

### Token Validation

MCPify supports JWT token validation for enhanced security. Token validation is opt-in for backward compatibility.

```csharp
builder.Services.AddMcpify(options =>
{
    // Set the resource URL for audience validation
    options.ResourceUrlOverride = "https://api.example.com";

    // Configure OAuth provider(s)
    options.OAuthConfigurations.Add(new OAuth2Configuration
    {
        AuthorizationUrl = "https://auth.example.com/authorize",
        TokenUrl = "https://auth.example.com/token",
        Scopes = new Dictionary<string, string>
        {
            { "read", "Read access" },
            { "write", "Write access" }
        }
    });

    // Enable token validation (opt-in for backward compatibility)
    options.TokenValidation = new TokenValidationOptions
    {
        EnableJwtValidation = true,
        ValidateAudience = true,
        ValidateScopes = true,
        RequireOAuthConfiguredScopes = true,  // Auto-require scopes from OAuth config
        ClockSkew = TimeSpan.FromMinutes(5)
    };
});
```

**Scope Configuration Options:**

| Option | Description |
|--------|-------------|
| `RequireOAuthConfiguredScopes = true` | Automatically require all scopes from OAuth configurations. This includes scopes defined in `OAuthConfigurations` **and** scopes discovered from OpenAPI security schemes. |
| `DefaultRequiredScopes` | Explicitly list required scopes (use when you want different scopes than what's advertised in OAuth config). |

**Automatic Integration with OpenAPI:** When MCPify loads an external API from an OpenAPI spec that includes OAuth2 security schemes (like those configured for Swagger UI), the scopes are automatically parsed and added to the OAuth configuration store. With `RequireOAuthConfiguredScopes = true`, these scopes are automatically enforced during token validation - no duplicate configuration needed.

When validation fails, MCPify returns appropriate HTTP responses:

| Scenario | Status Code | WWW-Authenticate |
|----------|-------------|------------------|
| No token provided | 401 Unauthorized | `Bearer resource_metadata="..."` |
| Token expired | 401 Unauthorized | `Bearer error="invalid_token", error_description="Token has expired"` |
| Wrong audience | 401 Unauthorized | `Bearer error="invalid_token", error_description="Token audience does not match..."` |
| Missing scopes | 403 Forbidden | `Bearer error="insufficient_scope", scope="required_scope"` |

### Per-Tool Scope Requirements

Define granular scope requirements for specific tools using pattern matching:

```csharp
builder.Services.AddMcpify(options =>
{
    options.TokenValidation = new TokenValidationOptions
    {
        EnableJwtValidation = true,
        ValidateScopes = true,
        DefaultRequiredScopes = new List<string> { "mcp.access" }
    };

    // Define per-tool scope requirements
    options.ScopeRequirements = new List<ScopeRequirement>
    {
        // All admin_* tools require 'admin' scope
        new ScopeRequirement
        {
            Pattern = "admin_*",
            RequiredScopes = new List<string> { "admin" }
        },
        // Write operations require 'write' scope
        new ScopeRequirement
        {
            Pattern = "*_create",
            RequiredScopes = new List<string> { "write" }
        },
        new ScopeRequirement
        {
            Pattern = "*_update",
            RequiredScopes = new List<string> { "write" }
        },
        new ScopeRequirement
        {
            Pattern = "*_delete",
            RequiredScopes = new List<string> { "write" }
        },
        // Read-only tools need at least 'read' OR 'write' scope
        new ScopeRequirement
        {
            Pattern = "*_get",
            AnyOfScopes = new List<string> { "read", "write" }
        }
    };
});
```

Pattern matching supports:
-   `*` - matches any sequence of characters
-   `?` - matches any single character
-   Exact match - `tool_name`

### RFC 8707 Resource Parameter

MCPify automatically includes the [RFC 8707](https://datatracker.ietf.org/doc/html/rfc8707) `resource` parameter in OAuth requests when `ResourceUrlOverride` is configured. This helps authorization servers issue tokens scoped to specific resources:

```csharp
builder.Services.AddMcpify(options =>
{
    options.ResourceUrlOverride = "https://api.example.com";
});
```

The resource parameter is added to:
-   Authorization URL (`/authorize?resource=...`)
-   Token exchange requests (`POST /token` with `resource=...`)
-   Token refresh requests

## Contributing

We welcome contributions! Please see our [Contributing Guide](CONTRIBUTING.md) for details.

## License

MIT
