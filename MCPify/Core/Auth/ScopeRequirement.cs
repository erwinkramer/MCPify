using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;

namespace MCPify.Core.Auth;

/// <summary>
/// Authorization requirement that enforces the presence of a matching OAuth scope.
/// Supports simple glob-style patterns ("*" and "?").
/// </summary>
public sealed class ScopeRequirement : IAuthorizationRequirement, IAuthorizationRequirementData
{
    private string _pattern = "*";

    /// <summary>
    /// Initializes a new instance of the <see cref="ScopeRequirement"/> class.
    /// </summary>
    public ScopeRequirement()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ScopeRequirement"/> class.
    /// </summary>
    /// <param name="pattern">The scope pattern that must match the granted scopes.</param>
    public ScopeRequirement(string pattern)
    {
        Pattern = pattern;
    }

    /// <summary>
    /// Gets or sets the scope pattern required for the protected resource.
    /// The pattern supports '*' (zero or more characters) and '?' (single character) wildcards.
    /// </summary>
    public string Pattern
    {
        get => _pattern;
        init => _pattern = string.IsNullOrWhiteSpace(value) ? "*" : value;
    }

    /// <summary>
    /// Evaluates whether the supplied scope value satisfies this requirement.
    /// </summary>
    /// <param name="scope">The scope value to evaluate.</param>
    public bool Matches(string? scope)
    {
        if (scope is null)
        {
            return false;
        }

        if (_pattern == "*")
        {
            return true;
        }

        var comparison = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        var regexPattern = "^" + Regex.Escape(_pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";

        return Regex.IsMatch(scope, regexPattern, comparison);
    }

    /// <inheritdoc />
    public IEnumerable<IAuthorizationRequirement> GetRequirements()
    {
        yield return this;
    }
}
