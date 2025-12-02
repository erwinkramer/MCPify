using MCPify.Core;
using Microsoft.AspNetCore.Routing;

namespace MCPify.Endpoints;

public interface IEndpointMetadataProvider
{
    IEnumerable<OpenApiOperationDescriptor> GetLocalEndpoints(IEnumerable<EndpointDataSource>? dataSources = null);
}
