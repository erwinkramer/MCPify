using System.Collections.Concurrent;

namespace MCPify.Core.Auth.OAuth;

public class InMemorySessionTokenStore : ISessionTokenStore
{
    private readonly ConcurrentDictionary<string, TokenData> _tokens = new();
    private readonly AsyncLocal<string?> _currentSession = new();

    public void SetSession(string sessionId)
    {
        _currentSession.Value = sessionId;
    }

    public string? GetCurrentSession() => _currentSession.Value;

    public Task<TokenData?> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        var session = _currentSession.Value ?? throw new InvalidOperationException("SessionId not set on session token store.");
        _tokens.TryGetValue(session, out var token);
        return Task.FromResult<TokenData?>(token);
    }

    public Task SaveTokenAsync(TokenData token, CancellationToken cancellationToken = default)
    {
        var session = _currentSession.Value ?? throw new InvalidOperationException("SessionId not set on session token store.");
        _tokens[session] = token;
        return Task.CompletedTask;
    }
}
