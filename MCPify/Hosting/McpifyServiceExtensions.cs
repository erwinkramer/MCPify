using MCPify.Core;
using MCPify.Endpoints;
using MCPify.OpenApi;
using MCPify.Schema;
using MCPify.Tools;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using System.Net.Http;

namespace MCPify.Hosting;

public static class McpifyServiceExtensions
{
    public static IServiceCollection AddMcpify(
        this IServiceCollection services,
        Action<McpifyOptions> configure)
    {
        var opts = new McpifyOptions();
        configure(opts);

        services.AddSingleton(opts);

        services.AddSingleton<McpServerPrimitiveCollection<McpServerTool>>();
        services.AddSingleton<McpifyServiceRegistrar>();

        var serverBuilder = services.AddMcpServer();
        if (opts.Transport == McpTransportType.Stdio)
        {
            serverBuilder.WithStdioServerTransport();
        }
        else
        {
            serverBuilder.WithHttpTransport();
        }

        services.AddHttpClient();

        services.AddOptions<McpServerOptions>()
            .PostConfigure<McpServerPrimitiveCollection<McpServerTool>>((options, sharedCollection) =>
            {
                if (options.ToolCollection != null && !ReferenceEquals(options.ToolCollection, sharedCollection))
                {
                    foreach (var tool in options.ToolCollection)
                    {
                        sharedCollection.Add(tool);
                    }
                }

                options.ToolCollection = sharedCollection;
            });

        services.AddSingleton<IOpenApiProvider>(_ =>
            opts.ProviderOverride ?? new OpenApiV3Provider(opts.OpenApiDownloadTimeout));

        services.AddSingleton<IJsonSchemaGenerator>(_ =>
            opts.SchemaGeneratorOverride ?? new DefaultJsonSchemaGenerator());

        services.AddSingleton<IEndpointMetadataProvider, AspNetCoreEndpointMetadataProvider>();

        return services;
    }

    public static IServiceCollection AddMcpify(
        this IServiceCollection services,
        string swaggerUrl,
        string apiBaseUrl,
        Action<McpifyOptions>? configure = null)
    {
        return services.AddMcpify(options =>
        {
            configure?.Invoke(options);

            options.ExternalApis.Add(new ExternalApiOptions
            {
                SwaggerUrl = swaggerUrl,
                ApiBaseUrl = apiBaseUrl,
                ToolPrefix = options.ExternalApis.Count == 0 ? null : $"api{options.ExternalApis.Count}_",
            });
        });
    }

    public static IServiceCollection AddMcpifyTestTool(this IServiceCollection services)
    {
        services.AddSingleton<McpServerTool, SimpleMathTool>();
        return services;
    }
}
