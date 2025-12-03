using System.Net.Http.Headers;

namespace MCPify.Core.Auth;

public class BearerAuthentication : IAuthenticationProvider
{
    public string Token { get; }

    public BearerAuthentication(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) throw new ArgumentException("Token cannot be empty", nameof(token));
        Token = token;
    }

    public void Apply(HttpRequestMessage request)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
    }
}
