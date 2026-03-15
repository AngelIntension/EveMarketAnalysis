using System.Security.Claims;
using EveMarketAnalysisClient.Models;
using EveMarketAnalysisClient.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EveMarketAnalysisClient.Pages;

[Authorize]
public class PortfolioOptimizerModel : PageModel
{
    private readonly IPortfolioAnalyzer _analyzer;

    public string CharacterName { get; set; } = "Unknown";
    public int? CharacterId { get; set; }
    public string? ErrorMessage { get; set; }

    public PortfolioOptimizerModel(IPortfolioAnalyzer analyzer)
    {
        _analyzer = analyzer;
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

    public async Task<IActionResult> OnGetAnalysisAsync(
        int procurementRegionId = 10000002,
        long procurementStationId = 60003760,
        int sellingRegionId = 10000002,
        long sellingStationId = 60003760,
        int manufacturingSystemId = 30000142,
        decimal buyingBrokerFee = 3.0m,
        decimal sellingBrokerFee = 3.0m,
        decimal salesTax = 3.6m,
        decimal minIskPerHour = 25_000_000m,
        decimal dailyIncomeGoal = 750_000_000m,
        int manufacturingSlots = 11,
        int? whatIfME = null,
        int? whatIfTE = null,
        int? phaseOverride = null,
        bool simulateNextPhase = false,
        CancellationToken cancellationToken = default)
    {
        var characterIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(characterIdClaim) || !int.TryParse(characterIdClaim, out var characterId))
            return new JsonResult(new { error = "Could not determine character identity." }) { StatusCode = 400 };

        // Validation
        if (manufacturingSlots < 1 || manufacturingSlots > 50)
            return new JsonResult(new { error = "Manufacturing slots must be between 1 and 50." }) { StatusCode = 400 };

        if (buyingBrokerFee < 0 || buyingBrokerFee > 100)
            return new JsonResult(new { error = "Buying broker fee must be between 0 and 100%." }) { StatusCode = 400 };

        if (sellingBrokerFee < 0 || sellingBrokerFee > 100)
            return new JsonResult(new { error = "Selling broker fee must be between 0 and 100%." }) { StatusCode = 400 };

        if (salesTax < 0 || salesTax > 100)
            return new JsonResult(new { error = "Sales tax must be between 0 and 100%." }) { StatusCode = 400 };

        if (whatIfME.HasValue && (whatIfME < 0 || whatIfME > 10))
            return new JsonResult(new { error = "What-if ME must be between 0 and 10." }) { StatusCode = 400 };

        if (whatIfTE.HasValue && (whatIfTE < 0 || whatIfTE > 20))
            return new JsonResult(new { error = "What-if TE must be between 0 and 20." }) { StatusCode = 400 };

        if (phaseOverride.HasValue && (phaseOverride < 1 || phaseOverride > 5))
            return new JsonResult(new { error = "Phase override must be between 1 and 5." }) { StatusCode = 400 };

        try
        {
            var configuration = new PortfolioConfiguration(
                ProcurementStationId: procurementStationId,
                ProcurementRegionId: procurementRegionId,
                SellingHubStationId: sellingStationId,
                SellingHubRegionId: sellingRegionId,
                ManufacturingSystemId: manufacturingSystemId,
                BuyingBrokerFeePercent: buyingBrokerFee,
                SellingBrokerFeePercent: sellingBrokerFee,
                SalesTaxPercent: salesTax,
                MinIskPerHour: minIskPerHour,
                DailyIncomeGoal: dailyIncomeGoal,
                ManufacturingSlots: manufacturingSlots,
                WhatIfME: whatIfME,
                WhatIfTE: whatIfTE);

            var result = await _analyzer.AnalyzeAsync(
                characterId, configuration, phaseOverride, simulateNextPhase, cancellationToken);

            return new JsonResult(result);
        }
        catch (Exception ex)
        {
            return new JsonResult(new { error = $"Failed to analyze portfolio: {ex.Message}" }) { StatusCode = 500 };
        }
    }
}
