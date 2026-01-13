namespace MCPify.Core.Auth;

public class OAuth2Configuration
{
    public List<string> AuthorizationServers { get; set; } = [];
    public string AuthorizationUrl { get; set; } = string.Empty;
    public string FlowType { get; set; } = string.Empty;
    public string? RefreshUrl { get; set; }
    public Dictionary<string, string> Scopes { get; set; } = new();
    public string TokenUrl { get; set; } = string.Empty;
}
