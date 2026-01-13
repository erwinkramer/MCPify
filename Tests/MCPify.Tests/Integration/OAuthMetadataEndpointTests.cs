using System.Net;
using System.Net.Http.Json;
using System.Linq;
using MCPify.Core;
using MCPify.Core.Auth;
using MCPify.Hosting;
using MCPify.OpenApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;

namespace MCPify.Tests.Integration;

public class OAuthMetadataEndpointTests
{
    [Fact]
    public async Task GetMetadata_Returns404_WhenNoOAuthConfigured()
    {
        using var host = await CreateHostAsync();
        var client = host.GetTestClient();

        var response = await client.GetAsync("/.well-known/oauth-protected-resource");
        
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetMetadata_ReturnsMetadata_WhenOAuthConfigured()
    {
        var authUrl = "https://auth.example.com/authorize";
        var tokenUrl = "https://auth.example.com/token";
        var authorizationServers = new[]
        {
            "https://auth.example.com/login/oauth",
            "https://auth-backup.example.com/login/oauth"
        };

        using var host = await CreateHostAsync(services =>
        {
            var store = services.GetRequiredService<OAuthConfigurationStore>();
            store.AddConfiguration(new OAuth2Configuration
            {
                AuthorizationUrl = authUrl,
                TokenUrl = tokenUrl,
                AuthorizationServers = authorizationServers.ToList(),
                Scopes = new Dictionary<string, string> { { "scope1", "desc" } }
            });
        });
        
        var client = host.GetTestClient();

        var response = await client.GetAsync("/.well-known/oauth-protected-resource");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var metadata = await response.Content.ReadFromJsonAsync<ProtectedResourceMetadata>();
        Assert.NotNull(metadata);
        Assert.Equal(authorizationServers.OrderBy(server => server), metadata!.AuthorizationServers.OrderBy(server => server));
        Assert.Contains("scope1", metadata.ScopesSupported);
    }

    [Fact]
    public async Task GetMetadata_UsesResourceOverride_WhenConfigured()
    {
        var publicUrl = "https://public.example.com";

        using var host = await CreateHostAsync(services =>
        {
            var store = services.GetRequiredService<OAuthConfigurationStore>();
            store.AddConfiguration(new OAuth2Configuration
            {
                AuthorizationUrl = "https://auth.example.com/oauth2/v2.0/authorize",
                TokenUrl = "https://auth.example.com/oauth2/v2.0/token"
            });
        }, options =>
        {
            options.ResourceUrlOverride = publicUrl;
        });

        var client = host.GetTestClient();

        var response = await client.GetAsync("/.well-known/oauth-protected-resource");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var metadata = await response.Content.ReadFromJsonAsync<ProtectedResourceMetadata>();
        Assert.NotNull(metadata);
        Assert.Equal(publicUrl, metadata!.Resource);
    }

    [Fact]
    public async Task GetMetadata_FallsBackToAuthorizationUrlAuthority_WhenAuthorizationServerMissing()
    {
        var authUrl = "https://auth.example.com/oauth2/v2.0/authorize";

        using var host = await CreateHostAsync(services =>
        {
            var store = services.GetRequiredService<OAuthConfigurationStore>();
            store.AddConfiguration(new OAuth2Configuration
            {
                AuthorizationUrl = authUrl
            });
        });

        var client = host.GetTestClient();

        var response = await client.GetAsync("/.well-known/oauth-protected-resource");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var metadata = await response.Content.ReadFromJsonAsync<ProtectedResourceMetadata>();
        Assert.NotNull(metadata);
        Assert.Contains("https://auth.example.com", metadata!.AuthorizationServers);
    }

    private async Task<IHost> CreateHostAsync(Action<IServiceProvider>? configure = null, Action<McpifyOptions>? configureOptions = null)
    {
        return await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddMcpify(options =>
                        {
                            configureOptions?.Invoke(options);
                        });
                        services.AddLogging();
                        services.AddRouting();
                    })
                    .Configure(app =>
                    {
                        configure?.Invoke(app.ApplicationServices);
                        app.UseRouting();
                        app.UseEndpoints(endpoints => 
                        {
                            endpoints.MapMcpifyEndpoint();
                        });
                    });
            })
            .StartAsync();
    }

    private class ProtectedResourceMetadata
    {
        [System.Text.Json.Serialization.JsonPropertyName("resource")]
        public string Resource { get; set; } = default!;

        [System.Text.Json.Serialization.JsonPropertyName("authorization_servers")]
        public List<string> AuthorizationServers { get; set; } = default!;

        [System.Text.Json.Serialization.JsonPropertyName("scopes_supported")]
        public List<string> ScopesSupported { get; set; } = default!;
    }
}