namespace MCPify.Core;

public interface IMcpContextAccessor
{
    string? SessionId { get; set; }
    string? ConnectionId { get; set; }
    string? AccessToken { get; set; }
}

public class McpContextAccessor : IMcpContextAccessor
{
    public string? SessionId { get; set; }

    public string? ConnectionId { get; set; }

    public string? AccessToken { get; set; }
}