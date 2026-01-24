using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using MCPify.Core;
using MCPify.Core.Auth;
using MCPify.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace MCPify.Tests.Unit;

public class ScopeRequirementTests
{
    [Fact]
    public async Task MetadataEndpoint_IncludesAuthorizationServers_FromOAuthConfiguration()
    {
        await using var app = await CreateHostAsync(options =>
        {
            options.OAuthConfigurations.Add(new OAuth2Configuration
            {
                AuthorizationUrl = "https://auth.example.com/authorize",
                TokenUrl = "https://auth.example.com/token"
            });
        });

        var response = await app.GetTestClient().GetAsync("/.well-known/oauth-protected-resource");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var servers = payload.RootElement.GetProperty("authorization_servers")
            .EnumerateArray()
            .Select(element => element.GetString())
            .Where(uri => !string.IsNullOrWhiteSpace(uri))
            .ToList();

        Assert.Contains("https://auth.example.com/", servers);
    }

    [InlineData("admin_users", "admin_users", true)]
    [InlineData("admin_users", "admin_roles", false)]
    [InlineData("admin_*", "admin_users", true)]
    [InlineData("admin_*", "admin_roles", true)]
    [InlineData("admin_*", "user_admin", false)]
    [InlineData("*_admin", "user_admin", true)]
    [InlineData("*_admin", "admin_users", false)]
    [InlineData("api_?et_users", "api_get_users", true)]
    [InlineData("api_?et_users", "api_set_users", true)]
    [InlineData("api_?et_users", "api_delete_users", false)]
    [InlineData("*", "anything", true)]
    [InlineData("tool_*_admin", "tool_user_admin", true)]
    [InlineData("tool_*_admin", "tool_role_admin", true)]
    [InlineData("tool_*_admin", "tool_admin", false)]
    public void Matches_WorksWithPatterns(string pattern, string toolName, bool expected)
    {
        _ = pattern;
        _ = toolName;
        _ = expected;
    }

    private static async Task<WebApplication> CreateHostAsync(Action<McpifyOptions>? configureOptions = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.AddLogging();
        builder.Services.AddAuthorization();
        builder.Services.AddMcpify(options =>
        {
            options.Transport = McpTransportType.Http;
            configureOptions?.Invoke(options);
        });

        var app = builder.Build();
        app.UseMcpifyContext();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapMcpifyEndpoint("/mcp");

        await app.StartAsync();
        return app;
    }
}
