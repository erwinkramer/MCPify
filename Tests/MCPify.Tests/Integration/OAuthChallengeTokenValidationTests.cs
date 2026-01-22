using System.Net;
using System.Net.Http.Headers;
using System.Text;
using MCPify.Core;
using MCPify.Core.Auth;
using MCPify.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MCPify.Tests.Integration;

public class OAuthChallengeTokenValidationTests
{
    [Fact]
    public async Task PostWithoutSession_ReturnsUnauthorizedChallenge_WhenTokenValidationEnabled()
    {
        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddLogging();
                        services.AddRouting();
                        services.AddMcpify(options =>
                        {
                            options.Transport = McpTransportType.Http;
                            options.TokenValidation = new TokenValidationOptions
                            {
                                EnableJwtValidation = true,
                                ValidateAudience = true
                            };
                        });
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseMcpifyContext();
                        app.UseMcpifyOAuth();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapMcpifyEndpoint();
                        });
                    });
            })
            .StartAsync();

        var options = host.Services.GetRequiredService<McpifyOptions>();
        Assert.True(options.TokenValidation?.EnableJwtValidation, "Token validation should be enabled");
        var validationOptions = host.Services.GetRequiredService<TokenValidationOptions>();
        Assert.True(validationOptions.EnableJwtValidation, "TokenValidationOptions from DI should have EnableJwtValidation true");

        var client = host.GetTestClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/")
        {
            Content = new StringContent("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"ping\",\"params\":{}}", Encoding.UTF8, "application/json")
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        var authenticateHeader = string.Join(" | ", response.Headers.WwwAuthenticate.Select(h => h.ToString()));
        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized,
            $"Expected 401 challenge, got {(int)response.StatusCode} {response.StatusCode}. Headers: {authenticateHeader}. Body: {body}");

        Assert.Contains(response.Headers.WwwAuthenticate, header =>
            string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase));
    }
}
