namespace MCPify.Core.Auth.OAuth;

public interface ISessionTokenStore : ITokenStore
{
    void SetSession(string sessionId);
    string? GetCurrentSession();
}
