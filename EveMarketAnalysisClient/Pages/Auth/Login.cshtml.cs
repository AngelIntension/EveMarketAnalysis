using System.Web;
using EveMarketAnalysisClient.Configuration;
using EveMarketAnalysisClient.Services;
using EveMarketAnalysisClient.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace EveMarketAnalysisClient.Pages.Auth;

public class LoginModel : PageModel
{
    private readonly IEsiOAuthMetadataService _metadataService;
    private readonly EsiOptions _options;

    public string? RedirectUrl { get; private set; }

    public LoginModel(IEsiOAuthMetadataService metadataService, IOptions<EsiOptions> options)
    {
        _metadataService = metadataService;
        _options = options.Value;
    }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken = default)
    {
        var metadata = await _metadataService.GetMetadataAsync(cancellationToken);

        var verifier = PkceService.GenerateCodeVerifier();
        var challenge = PkceService.GenerateCodeChallenge(verifier);
        var state = PkceService.GenerateState();

        // Store verifier and state in encrypted temp cookies
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromMinutes(10),
            IsEssential = true
        };

        Response.Cookies.Append("pkce_verifier", verifier, cookieOptions);
        Response.Cookies.Append("pkce_state", state, cookieOptions);

        var queryParams = new Dictionary<string, string?>
        {
            ["response_type"] = "code",
            ["redirect_uri"] = _options.RedirectUri,
            ["client_id"] = _options.ClientId,
            ["scope"] = _options.Scopes,
            ["state"] = state,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256"
        };

        var query = string.Join("&", queryParams
            .Where(kvp => kvp.Value != null)
            .Select(kvp => $"{HttpUtility.UrlEncode(kvp.Key)}={HttpUtility.UrlEncode(kvp.Value)}"));

        RedirectUrl = $"{metadata.AuthorizationEndpoint}?{query}";

        return Redirect(RedirectUrl);
    }
}
