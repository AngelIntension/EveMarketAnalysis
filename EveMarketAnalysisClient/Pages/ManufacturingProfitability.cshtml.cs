using System.Collections.Immutable;
using System.Security.Claims;
using EveMarketAnalysisClient.Models;
using EveMarketAnalysisClient.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EveMarketAnalysisClient.Pages;

[Authorize]
public class ManufacturingProfitabilityModel : PageModel
{
    private static readonly HashSet<int> ValidRegionIds = new(
        TradeHubRegion.All.Select(r => r.RegionId));

    private readonly IProfitabilityCalculator _calculator;
    private readonly IEsiCharacterClient _esiClient;

    public string CharacterName { get; set; } = "Unknown";
    public int? CharacterId { get; set; }
    public string? ErrorMessage { get; set; }

    public ManufacturingProfitabilityModel(
        IProfitabilityCalculator calculator,
        IEsiCharacterClient esiClient)
    {
        _calculator = calculator;
        _esiClient = esiClient;
    }

    public void OnGet()
    {
        var characterIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        CharacterName = User.Identity?.Name ?? "Unknown";

        if (string.IsNullOrEmpty(characterIdClaim) || !int.TryParse(characterIdClaim, out var id))
        {
            ErrorMessage = "Could not determine character identity. Please log in again.";
            return;
        }

        CharacterId = id;
    }

    public async Task<IActionResult> OnGetProfitabilityAsync(
        int regionId = 10000002,
        decimal taxRate = 0.08m,
        CancellationToken cancellationToken = default)
    {
        var characterIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(characterIdClaim) || !int.TryParse(characterIdClaim, out var characterId))
            return new JsonResult(new { error = "Could not determine character identity." }) { StatusCode = 400 };

        if (!ValidRegionIds.Contains(regionId))
            return new JsonResult(new { error = $"Invalid regionId: {regionId}" }) { StatusCode = 400 };

        if (taxRate < 0.0m || taxRate > 1.0m)
            return new JsonResult(new { error = $"taxRate must be between 0.0 and 1.0" }) { StatusCode = 400 };

        try
        {
            var blueprints = await _esiClient.GetCharacterBlueprintsAsync(characterId, cancellationToken);

            var settings = new ProfitabilitySettings(
                RegionId: regionId,
                TaxRate: taxRate);

            var results = await _calculator.CalculateAsync(blueprints, settings, cancellationToken);

            var region = TradeHubRegion.All.FirstOrDefault(r => r.RegionId == regionId) ?? TradeHubRegion.Default;
            var successCount = results.Count(r => r.ErrorMessage == null);
            var errorCount = results.Count(r => r.ErrorMessage != null);

            var response = new ProfitabilityResponse(
                Results: results,
                RegionId: regionId,
                RegionName: region.RegionName,
                TaxRate: taxRate,
                TotalBlueprints: blueprints.Length,
                SuccessCount: successCount,
                ErrorCount: errorCount,
                FetchedAt: DateTimeOffset.UtcNow);

            return new JsonResult(response);
        }
        catch (Exception ex)
        {
            return new JsonResult(new { error = $"Failed to load profitability data: {ex.Message}" }) { StatusCode = 500 };
        }
    }
}
