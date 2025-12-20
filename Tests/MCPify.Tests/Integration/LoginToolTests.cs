using MCPify.Core;
using MCPify.Core.Auth;
using MCPify.Core.Auth.OAuth;
using MCPify.Tools;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Moq;
using System.Text.Json;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace MCPify.Tests.Integration;

public class LoginToolTests
{
    private static RequestContext<CallToolRequestParams> CreateContext(CallToolRequestParams @params, IServiceProvider services)
    {
        var mockServer = new Mock<McpServer>();
        mockServer.SetupGet(s => s.Services).Returns(services);
        
        // Find concrete JsonRpcRequest
        var jsonRpcRequestType = typeof(RequestContext<>).Assembly.GetTypes()
            .First(t => t.Name == "JsonRpcRequest" && !t.IsAbstract);
        var jsonRpcRequest = RuntimeHelpers.GetUninitializedObject(jsonRpcRequestType);

        var ctor = typeof(RequestContext<CallToolRequestParams>).GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(c => c.GetParameters().Length == 2);
            
        if (ctor == null)
        {
             throw new Exception("Could not find RequestContext constructor");
        }

        var context = (RequestContext<CallToolRequestParams>)ctor.Invoke(new object?[] { mockServer.Object, jsonRpcRequest });
        context.Params = @params;
        context.Services = services;
        return context;
    }

    [Fact]
    public async Task LoginTool_ShouldPollAndReturnSuccess_WhenTokenAppears()
    {
        // Arrange
        var services = new ServiceCollection();
        
        var mockTokenStoreArg = new Mock<ISecureTokenStore>();
        var mockAccessorArg = new Mock<IMcpContextAccessor>();

        var mockAuth = new Mock<OAuthAuthorizationCodeAuthentication>(
            "client", "http://auth", "http://token", "scope", 
            mockTokenStoreArg.Object, mockAccessorArg.Object,
            null, null, "http://callback", null, false, null, null, false);
            
        mockAuth.Setup(x => x.BuildAuthorizationUrl(It.IsAny<string>()))
                .Returns("http://auth/authorize?foo=bar");

        var tokenStore = new InMemoryTokenStore();
        
        services.AddSingleton(mockAuth.Object);
        services.AddSingleton<ISecureTokenStore>(tokenStore);
        services.AddSingleton<LoginTool>();

        var provider = services.BuildServiceProvider();
        var tool = provider.GetRequiredService<LoginTool>();

        var arguments = new Dictionary<string, JsonElement>();
        var callToolParams = new CallToolRequestParams { Name = "login", Arguments = arguments };
        var context = CreateContext(callToolParams, provider);

        // Act
        var toolTask = tool.InvokeAsync(context, CancellationToken.None);

        await Task.Delay(500);
        await tokenStore.SaveTokenAsync("default", "OAuth", new TokenData("test-access-token", null, null), CancellationToken.None);

        var result = await toolTask;

        // Assert
        Assert.True(result.IsError != true);
        Assert.Single(result.Content);
        var textContent = Assert.IsType<TextContentBlock>(result.Content[0]);
        Assert.Contains("Login successful", textContent.Text);
        Assert.Contains("default", textContent.Text);
    }

    [Fact]
    public async Task LoginTool_ShouldTimeout_WhenNoTokenAppears()
    {
        // Arrange
        var services = new ServiceCollection();
        var mockTokenStoreArg = new Mock<ISecureTokenStore>();
        var mockAccessorArg = new Mock<IMcpContextAccessor>();

        var mockAuth = new Mock<OAuthAuthorizationCodeAuthentication>(
             "client", "http://auth", "http://token", "scope",
             mockTokenStoreArg.Object, mockAccessorArg.Object,
             null, null, "http://callback", null, false, null, null, false);

        mockAuth.Setup(x => x.BuildAuthorizationUrl(It.IsAny<string>()))
                .Returns("http://auth/authorize?foo=bar");

        var tokenStore = new InMemoryTokenStore();

        services.AddSingleton(mockAuth.Object);
        services.AddSingleton<ISecureTokenStore>(tokenStore);
        services.AddSingleton<LoginTool>();

        var provider = services.BuildServiceProvider();
        var tool = provider.GetRequiredService<LoginTool>();

        var cts = new CancellationTokenSource(1000); 

        var arguments = new Dictionary<string, JsonElement>();
        var callToolParams = new CallToolRequestParams { Name = "login", Arguments = arguments };
        var context = CreateContext(callToolParams, provider);

        // Act
        var result = await tool.InvokeAsync(context, cts.Token);

        // Assert
        Assert.True(result.IsError != true);
        Assert.Single(result.Content);
        var textContent = Assert.IsType<TextContentBlock>(result.Content[0]);
        
        Assert.DoesNotContain("Login successful", textContent.Text);
        Assert.Contains("http://auth/authorize?foo=bar", textContent.Text);
    }
}