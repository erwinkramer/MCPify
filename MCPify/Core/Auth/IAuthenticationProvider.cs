namespace MCPify.Core.Auth;

public interface IAuthenticationProvider
{
    void Apply(HttpRequestMessage request);
}
