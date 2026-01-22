using System.Collections.Generic;
using System.Linq;
using MCPify.Core;
using MCPify.Core.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;

namespace MCPify.Hosting;

public class McpOAuthAuthenticationMiddleware
{
    private readonly RequestDelegate _next;

    public const string TokenValidationResultKey = "McpTokenValidationResult";

    public McpOAuthAuthenticationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;
        if (path.StartsWithSegments("/.well-known") ||
            path.StartsWithSegments("/swagger") ||
            path.StartsWithSegments("/health") ||
            path.StartsWithSegments("/connect") ||
            path.StartsWithSegments("/auth"))
        {
            await _next(context);
            return;
        }

        var options = context.RequestServices.GetService<McpifyOptions>();
        var oauthStore = context.RequestServices.GetService<OAuthConfigurationStore>();

        var oauthConfigurations = oauthStore?.GetConfigurations().ToList() ?? new List<OAuth2Configuration>();
        var validationOptions = options?.TokenValidation;

        var challengeScopes = BuildChallengeScopes(oauthConfigurations, validationOptions);
        var authRequired = oauthConfigurations.Count > 0 || (validationOptions?.EnableJwtValidation == true);

        if (!authRequired)
        {
            await _next(context);
            return;
        }

        var resourceUrl = GetResourceUrl(context, options);
        var accessor = context.RequestServices.GetService<IMcpContextAccessor>();

        if (!TryGetBearerToken(context, out var token))
        {
            await WriteChallengeResponse(context, resourceUrl, challengeScopes, null, null);
            return;
        }

        if (accessor != null)
        {
            accessor.AccessToken = token;
        }

        if (validationOptions?.EnableJwtValidation == true)
        {
            var validator = context.RequestServices.GetService<IAccessTokenValidator>();
            if (validator != null)
            {
                var expectedAudience = validationOptions.ValidateAudience
                    ? (validationOptions.ExpectedAudience ?? resourceUrl)
                    : null;

                var validationResult = await validator.ValidateAsync(token, expectedAudience, context.RequestAborted);
                context.Items[TokenValidationResultKey] = validationResult;

                if (!validationResult.IsValid)
                {
                    await WriteChallengeResponse(context, resourceUrl, challengeScopes,
                        validationResult.ErrorCode ?? "invalid_token",
                        validationResult.ErrorDescription ?? "Token validation failed");
                    return;
                }

                if (validationOptions.ValidateScopes)
                {
                    var scopeStore = context.RequestServices.GetService<ScopeRequirementStore>();
                    if (scopeStore != null)
                    {
                        var scopeResult = scopeStore.ValidateScopesForTool("*", validationResult.Scopes);
                        if (!scopeResult.IsValid)
                        {
                            await WriteInsufficientScopeResponse(context, resourceUrl, scopeResult.MissingScopes);
                            return;
                        }
                    }
                }
            }
        }

        await _next(context);
    }

    private static string GetResourceUrl(HttpContext context, McpifyOptions? options)
    {
        var resourceUrl = options?.ResourceUrlOverride;
        if (string.IsNullOrWhiteSpace(resourceUrl))
        {
            resourceUrl = options?.LocalEndpoints?.BaseUrlOverride;
        }

        if (string.IsNullOrWhiteSpace(resourceUrl))
        {
            resourceUrl = $"{context.Request.Scheme}://{context.Request.Host}";
        }

        return resourceUrl.TrimEnd('/');
    }

    private static IReadOnlyList<string> BuildChallengeScopes(
        IReadOnlyCollection<OAuth2Configuration> configurations,
        TokenValidationOptions? validationOptions)
    {
        var scopes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var configuration in configurations)
        {
            foreach (var scope in configuration.Scopes.Keys)
            {
                scopes.Add(scope);
            }
        }

        if (validationOptions?.DefaultRequiredScopes != null)
        {
            foreach (var scope in validationOptions.DefaultRequiredScopes)
            {
                scopes.Add(scope);
            }
        }

        return scopes.ToList();
    }

    private static bool TryGetBearerToken(HttpContext context, out string token)
    {
        token = string.Empty;
        string? authorization = context.Request.Headers[HeaderNames.Authorization];

        if (string.IsNullOrEmpty(authorization) ||
            !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        token = authorization.Substring("Bearer ".Length).Trim();
        return !string.IsNullOrEmpty(token);
    }

    private static Task WriteChallengeResponse(
        HttpContext context,
        string resourceUrl,
        IReadOnlyList<string> scopes,
        string? errorCode,
        string? errorDescription)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        var metadataUrl = $"{resourceUrl}/.well-known/oauth-protected-resource";
        context.Response.Headers[HeaderNames.WWWAuthenticate] =
            BuildWwwAuthenticateHeader(metadataUrl, scopes, errorCode, errorDescription);
        return Task.CompletedTask;
    }

    private static Task WriteInsufficientScopeResponse(
        HttpContext context,
        string resourceUrl,
        IReadOnlyList<string> requiredScopes)
    {
        var metadataUrl = $"{resourceUrl}/.well-known/oauth-protected-resource";

        context.Response.StatusCode = StatusCodes.Status403Forbidden;

        var parts = new List<string>
        {
            "Bearer",
            $"error=\"insufficient_scope\"",
            $"error_description=\"The access token does not have the required scope(s)\"",
            $"resource_metadata=\"{metadataUrl}\""
        };

        if (requiredScopes.Count > 0)
        {
            parts.Add($"scope=\"{string.Join(" ", requiredScopes)}\"");
        }

        context.Response.Headers[HeaderNames.WWWAuthenticate] = string.Join(", ", parts);
        return Task.CompletedTask;
    }

    private static string BuildWwwAuthenticateHeader(
        string metadataUrl,
        IReadOnlyList<string> scopes,
        string? errorCode,
        string? errorDescription)
    {
        var parts = new List<string>
        {
            $"Bearer resource_metadata=\"{metadataUrl}\""
        };

        if (!string.IsNullOrEmpty(errorCode))
        {
            parts.Add($"error=\"{errorCode}\"");
        }

        if (!string.IsNullOrEmpty(errorDescription))
        {
            var escapedDescription = errorDescription.Replace("\"", "\\\"");
            parts.Add($"error_description=\"{escapedDescription}\"");
        }

        if (scopes.Count > 0)
        {
            parts.Add($"scope=\"{string.Join(" ", scopes)}\"");
        }

        return string.Join(", ", parts);
    }
}
