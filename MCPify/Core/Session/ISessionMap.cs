using System.Collections.Concurrent;

namespace MCPify.Core.Session;

public interface ISessionMap
{
    /// <summary>
    /// resolves the effective identity (Principal ID) for a given session handle.
    /// If the handle is unknown, it returns the handle itself (treating it as a temporary identity).
    /// </summary>
    string ResolvePrincipal(string sessionHandle);

    /// <summary>
    /// Promotes a session handle to map to a specific principal (Real User ID).
    /// </summary>
    void UpgradeSession(string sessionHandle, string principalId);
}

public class InMemorySessionMap : ISessionMap
{
    // Maps SessionHandle -> PrincipalId
    private readonly ConcurrentDictionary<string, string> _map = new();

    public string ResolvePrincipal(string sessionHandle)
    {
        if (string.IsNullOrEmpty(sessionHandle)) return sessionHandle;
        
        // Return the mapped principal, or fall back to the handle itself (Temp ID)
        return _map.TryGetValue(sessionHandle, out var principal) ? principal : sessionHandle;
    }

    public void UpgradeSession(string sessionHandle, string principalId)
    {
        if (string.IsNullOrEmpty(sessionHandle) || string.IsNullOrEmpty(principalId)) return;
        
        _map[sessionHandle] = principalId;
    }
}
