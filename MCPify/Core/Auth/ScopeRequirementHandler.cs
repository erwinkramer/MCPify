using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace MCPify.Core.Auth;

/// <summary>
/// Evaluates <see cref="ScopeRequirement"/> instances against the authenticated user's scopes.
/// </summary>
public sealed class ScopeRequirementHandler : AuthorizationHandler<ScopeRequirement>
{
    private static readonly string[] ScopeClaimTypes =
    {
        "scope",
        "scp",
        "http://schemas.microsoft.com/identity/claims/scope"
    };

    /// <inheritdoc />
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ScopeRequirement requirement)
    {
        if (context.User is null)
        {
            return Task.CompletedTask;
        }

        var scopes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var claim in context.User.Claims)
        {
            if (!ScopeClaimTypes.Contains(claim.Type))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(claim.Value))
            {
                continue;
            }

            foreach (var entry in SplitScopes(claim.Value))
            {
                scopes.Add(entry);
            }
        }

        if (scopes.Any(requirement.Matches))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    private static IEnumerable<string> SplitScopes(string value)
    {
        if (value.Contains(' '))
        {
            foreach (var part in value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                yield return part;
            }
        }
        else if (value.Contains(','))
        {
            foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                yield return part;
            }
        }
        else
        {
            yield return value.Trim();
        }
    }
}
