using EveMarketAnalysisClient.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;

namespace EveMarketAnalysisClient.Services;

public class EsiAuthenticationProvider : IAuthenticationProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IEsiTokenService _tokenService;

    public EsiAuthenticationProvider(
        IHttpContextAccessor httpContextAccessor,
        IEsiTokenService tokenService)
    {
        _httpContextAccessor = httpContextAccessor;
        _tokenService = tokenService;
    }

    public async Task AuthenticateRequestAsync(
        RequestInformation request,
        Dictionary<string, object>? additionalAuthenticationContext = null,
        CancellationToken cancellationToken = default)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User.Identity?.IsAuthenticated != true)
            return;

        var accessToken = httpContext.User.FindFirst("access_token")?.Value;
        if (string.IsNullOrEmpty(accessToken))
            return;

        var expiresAtClaim = httpContext.User.FindFirst("expires_at")?.Value;
        if (!string.IsNullOrEmpty(expiresAtClaim) &&
            DateTimeOffset.TryParse(expiresAtClaim, out var expiresAt) &&
            expiresAt <= DateTimeOffset.UtcNow)
        {
            var refreshToken = httpContext.User.FindFirst("refresh_token")?.Value;
            if (!string.IsNullOrEmpty(refreshToken))
            {
                try
                {
                    var newTokenSet = await _tokenService.RefreshAsync(refreshToken, cancellationToken);
                    accessToken = newTokenSet.AccessToken;
                }
                catch (HttpRequestException)
                {
                    // Token refresh failed (revoked/expired refresh token)
                    // Clear auth and let the request proceed without auth header
                    // The caller will get a 401 which should trigger redirect to login
                    return;
                }
            }
        }

        request.Headers.Add("Authorization", $"Bearer {accessToken}");
    }
}
