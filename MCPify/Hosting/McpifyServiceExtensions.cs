using MCPify.Core;
using MCPify.OpenApi;
using MCPify.Schema;
using MCPify.Tools;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace MCPify.Hosting;

public static class McpifyServiceExtensions
{
    public static IServiceCollection AddMcpify(
        this IServiceCollection services,
        string swaggerUrl,
        string apiBaseUrl,
        Action<McpifyOptions>? configure = null)
    {
        var opts = new McpifyOptions();
        configure?.Invoke(opts);

        services.AddSingleton(opts);

        services.AddMcpServer()
            .WithHttpTransport();

        services.AddHttpClient();

        services.AddSingleton<IOpenApiProvider>(_ =>
            opts.ProviderOverride ?? new OpenApiV3Provider());

        services.AddSingleton<IJsonSchemaGenerator>(_ =>
            opts.SchemaGeneratorOverride ?? new DefaultJsonSchemaGenerator());

        // Eagerly load the Swagger spec and register tools into DI
        try 
        {
            var provider = opts.ProviderOverride ?? new OpenApiV3Provider();
            var document = provider.LoadAsync(swaggerUrl).GetAwaiter().GetResult();
            var operations = provider.GetOperations(document);

            if (opts.Filter != null)
            {
                operations = operations.Where(opts.Filter);
            }

            foreach (var operation in operations)
            {
                var toolName = string.IsNullOrEmpty(opts.ToolPrefix)
                    ? operation.Name
                    : opts.ToolPrefix + operation.Name;

                var descriptor = operation with { Name = toolName };

                // Register the tool instance
                services.AddSingleton<McpServerTool>(sp =>
                {
                    var httpClient = sp.GetRequiredService<HttpClient>();
                    var schema = sp.GetRequiredService<IJsonSchemaGenerator>();
                    var options = sp.GetRequiredService<McpifyOptions>();
                    return new OpenApiProxyTool(descriptor, apiBaseUrl, httpClient, schema, options);
                });
            }

            Console.WriteLine($"[MCPify] Successfully registered {operations.Count()} tools from Swagger.");
        }
        catch (Exception ex)
        {
            // Log failure but allow application to start (with 0 dynamic tools)
            Console.WriteLine($"[MCPify] WARNING: Failed to load OpenAPI spec from {swaggerUrl}. Dynamic tools will be unavailable.");
            Console.WriteLine($"[MCPify] Error: {ex.Message}");
        }

        return services;
    }

    public static IServiceCollection AddMcpifyTestTool(this IServiceCollection services)
    {
        services.AddSingleton<McpServerTool, SimpleMathTool>();
        return services;
    }
}