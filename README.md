# MCPify

**MCPify** is a .NET library that bridges the gap between your existing ASP.NET Core APIs (or external OpenAPI/Swagger specs) and the [Model Context Protocol (MCP)](https://modelcontextprotocol.io/). It allows you to expose API operations as MCP tools that can be consumed by AI assistants like Claude Desktop, ensuring seamless integration with your existing services.

## Features

-   **Automatic Tool Generation**: Dynamically converts OpenAPI (Swagger) v2/v3 definitions into MCP tools.
-   **Hybrid Support**: Expose your **local** ASP.NET Core endpoints and **external** public APIs simultaneously.
-   **Seamless Authentication**: Built-in support for OAuth 2.0 Authorization Code Flow with PKCE.
    -   Includes a `login_auth_code_pkce` tool that handles the browser-based login flow automatically.
    -   Securely stores tokens per session using encrypted local storage.
    -   Automatically refreshes tokens when they expire.
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

// 1. Add MCPify services
builder.Services.AddMcpify(options =>
{
    // Choose Transport (Stdio for local tools, Http for remote)
    options.Transport = McpTransportType.Stdio;

    // Option A: Expose Local Endpoints
    options.LocalEndpoints = new LocalEndpointsOptions
    {
        Enabled = true,
        ToolPrefix = "myapp_",
        // Optional: Filter which endpoints to expose
        Filter = op => op.Route.StartsWith("/api")
    };

    // Option B: Expose External APIs via Swagger
    options.ExternalApis.Add(new ExternalApiOptions
    {
        ApiBaseUrl = "https://petstore.swagger.io/v2",
        OpenApiUrl = "https://petstore.swagger.io/v2/swagger.json",
        ToolPrefix = "petstore_"
    });
});

var app = builder.Build();

// 2. Add Middleware
app.UseMcpifyContext();
app.UseMcpifyOAuth(); // If using Authentication

// ... Map your endpoints ...

// 3. Map the MCP endpoint (required for Http transport, optional for Stdio)
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

MCPify provides a first-class experience for APIs secured with OAuth 2.0.

### Enabling OAuth

Register the authentication provider in your `Program.cs`:

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
3.  MCPify automatically opens the system browser to the login page.
4.  The user logs in and approves the request.
5.  The browser redirects back to your application (e.g., `/auth/callback`).
6.  Your app saves the token and displays a success message.
7.  The `login_auth_code_pkce` tool detects the successful login and reports back to Claude.
8.  Claude can now invoke authenticated tools!

## Contributing

We welcome contributions! Please see our [Contributing Guide](CONTRIBUTING.md) for details.

## License

MIT
