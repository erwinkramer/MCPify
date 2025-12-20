using MCPify.Core;
using MCPify.Core.Auth;
using MCPify.Core.Auth.OAuth;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace MCPify.Tools;

public class LoginTool : McpServerTool
{
    public LoginTool()
    {
    }

    public override Tool ProtocolTool => new()
    {
        Name = "login_auth_code_pkce",
        Description = "Initiates the OAuth login flow. It attempts to open the system browser with the authorization URL and waits for the user to complete the login. Returns a success message once authenticated.",
        InputSchema = JsonDocument.Parse("""{ "type": "object", "properties": { "sessionId": { "type": "string" } } }""").RootElement
    };

    public override IReadOnlyList<object> Metadata => Array.Empty<object>();

    public override async ValueTask<CallToolResult> InvokeAsync(RequestContext<CallToolRequestParams> context, CancellationToken token)
    {
        var logger = context.Services != null ? context.Services.GetService<Microsoft.Extensions.Logging.ILogger<LoginTool>>() : null;
        string sessionId = Constants.DefaultSessionId;

        if (context.Params?.Arguments != null &&
            context.Params.Arguments.TryGetValue("sessionId", out var sessionElement) &&
            sessionElement.ValueKind == JsonValueKind.String &&
            !string.IsNullOrEmpty(sessionElement.GetString()))
        {
            sessionId = sessionElement.GetString()!;
        }

        logger?.LogInformation("Initiating login for session {SessionId}", sessionId);

        var auth = context.Services!.GetRequiredService<OAuthAuthorizationCodeAuthentication>();
        var tokenStore = context.Services!.GetRequiredService<ISecureTokenStore>();

        var authUrl = auth.BuildAuthorizationUrl(sessionId);
        var browserOpened = false;

        try
        {
            logger?.LogDebug("Attempting to open browser for URL: {AuthUrl}", authUrl);
            OpenBrowser(authUrl);
            browserOpened = true;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to open browser automatically.");
        }

        var timeout = DateTime.UtcNow.AddMinutes(2);
        while (DateTime.UtcNow < timeout && !token.IsCancellationRequested)
        {
            var tokenData = await tokenStore.GetTokenAsync(sessionId, "OAuth", token);
            if (tokenData != null && !string.IsNullOrEmpty(tokenData.AccessToken))
            {
                 logger?.LogInformation("Login successful for session {SessionId}", sessionId);
                 return new CallToolResult
                {
                    Content = new[] { new TextContentBlock { Text = $"Login successful! Session '{sessionId}' is now authenticated." } }
                };
            }

            try
            {
                await Task.Delay(1000, token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger?.LogInformation("Login timed out or was cancelled for session {SessionId}", sessionId);

        var response = browserOpened 
            ? $"Browser opened. Please complete the login there. If it didn't open, visit: {authUrl}"
            : $"Please open this URL to authenticate: {authUrl}";

        return new CallToolResult
        {
            Content = new[] { new TextContentBlock { Text = response } }
        };
    }

    private void OpenBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                url = url.Replace("&", "^&");
                Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", url);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", url);
            }
        }
    }

    private static CallToolResult Error(string message) => new()
    {
        IsError = true,
        Content = new[] { new TextContentBlock { Text = message } }
    };
}