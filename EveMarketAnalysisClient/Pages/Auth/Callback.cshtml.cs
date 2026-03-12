using System.Security.Claims;
using EveMarketAnalysisClient.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EveMarketAnalysisClient.Pages.Auth;

public class CallbackModel : PageModel
{
    private readonly IEsiTokenService _tokenService;

    public string? ErrorMessage { get; set; }

    public CallbackModel(IEsiTokenService tokenService)
    {
        _tokenService = tokenService;
    }

    public async Task<IActionResult> OnGetAsync(
        string? code = null,
        string? state = null,
        string? error = null,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(error))
        {
            ErrorMessage = error switch
            {
                "access_denied" => "You denied consent to the application. Please try again if this was unintentional.",
                _ => $"Authentication error: {error}"
            };
            return Page();
        }

        // Validate state
        var expectedState = Request.Cookies["pkce_state"];
        if (string.IsNullOrEmpty(state) || state != expectedState)
        {
            ErrorMessage = "Invalid state parameter. This may indicate a security issue. Please try logging in again.";
            return Page();
        }

        if (string.IsNullOrEmpty(code))
        {
            ErrorMessage = "No authorization code received. Please try logging in again.";
            return Page();
        }

        var verifier = Request.Cookies["pkce_verifier"];
        if (string.IsNullOrEmpty(verifier))
        {
            ErrorMessage = "PKCE verifier not found. Your session may have expired. Please try logging in again.";
            return Page();
        }

        try
        {
            var tokenSet = await _tokenService.ExchangeCodeAsync(code, verifier, cancellationToken);

            // Create claims principal
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, tokenSet.CharacterId.ToString()),
                new(ClaimTypes.Name, tokenSet.CharacterName),
                new("access_token", tokenSet.AccessToken),
                new("refresh_token", tokenSet.RefreshToken),
                new("expires_at", tokenSet.ExpiresAt.ToString("O"))
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = tokenSet.ExpiresAt.AddDays(30), // Cookie lives longer than access token
                AllowRefresh = true
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                authProperties);

            // Clean up PKCE cookies
            Response.Cookies.Delete("pkce_verifier");
            Response.Cookies.Delete("pkce_state");

            return RedirectToPage("/Index");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to exchange authorization code: {ex.Message}";
            return Page();
        }
    }
}
