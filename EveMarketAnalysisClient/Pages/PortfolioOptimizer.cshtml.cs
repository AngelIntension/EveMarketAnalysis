using System.Security.Claims;
using EveMarketAnalysisClient.Models;
using EveMarketAnalysisClient.Services.Interfaces;
using EveStableInfrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Memory;

namespace EveMarketAnalysisClient.Pages;

[Authorize]
public class PortfolioOptimizerModel : PageModel
{
    private readonly IPortfolioAnalyzer _analyzer;
    private readonly ApiClient _apiClient;
    private readonly IMemoryCache _cache;

    public string CharacterName { get; set; } = "Unknown";
    public int? CharacterId { get; set; }
    public string? ErrorMessage { get; set; }

    public PortfolioOptimizerModel(IPortfolioAnalyzer analyzer, ApiClient apiClient, IMemoryCache cache)
    {
        _analyzer = analyzer;
        _apiClient = apiClient;
        _cache = cache;
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
        decimal facilityTax = 0.25m,
        decimal sccSurcharge = 4.0m,
        decimal minIskPerHour = 25_000_000m,
        decimal dailyIncomeGoal = 750_000_000m,
        int manufacturingSlots = 11,
        int? whatIfME = null,
        int? whatIfTE = null,
        bool useBuyOrders = true,
        bool useBuyOrdersForSelling = false,
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

        if (facilityTax < 0 || facilityTax > 100)
            return new JsonResult(new { error = "Facility tax must be between 0 and 100%." }) { StatusCode = 400 };

        if (sccSurcharge < 0 || sccSurcharge > 100)
            return new JsonResult(new { error = "SCC surcharge must be between 0 and 100%." }) { StatusCode = 400 };

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
                FacilityTaxPercent: facilityTax,
                SccSurchargePercent: sccSurcharge,
                MinIskPerHour: minIskPerHour,
                DailyIncomeGoal: dailyIncomeGoal,
                ManufacturingSlots: manufacturingSlots,
                WhatIfME: whatIfME,
                WhatIfTE: whatIfTE,
                UseBuyOrdersForMaterials: useBuyOrders,
                UseBuyOrdersForSelling: useBuyOrdersForSelling);

            var result = await _analyzer.AnalyzeAsync(
                characterId, configuration, phaseOverride, simulateNextPhase, cancellationToken);

            return new JsonResult(result);
        }
        catch (Exception ex)
        {
            return new JsonResult(new { error = $"Failed to analyze portfolio: {ex.Message}" }) { StatusCode = 500 };
        }
    }

    public async Task<IActionResult> OnGetSearchSystemsAsync(
        string query = "",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            return new JsonResult(Array.Empty<object>());

        var systemMap = await GetSystemNameMapAsync(cancellationToken);

        var results = systemMap
            .Where(kvp => kvp.Value.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(kvp => !kvp.Value.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            .ThenBy(kvp => kvp.Value)
            .Take(15)
            .Select(kvp => new { id = kvp.Key, name = kvp.Value })
            .ToList();

        return new JsonResult(results);
    }

    private async Task<Dictionary<int, string>> GetSystemNameMapAsync(CancellationToken cancellationToken)
    {
        const string cacheKey = "esi:systemnames";
        if (_cache.TryGetValue(cacheKey, out Dictionary<int, string>? cached) && cached != null)
            return cached;

        // Fetch all system IDs
        var systemIds = await _apiClient.Universe.Systems.GetAsync(cancellationToken: cancellationToken);
        if (systemIds == null || systemIds.Count == 0)
            return new Dictionary<int, string>();

        // Batch-resolve names (max 1000 per call)
        var nameMap = new Dictionary<int, string>();
        foreach (var batch in systemIds.Chunk(1000))
        {
            try
            {
                var body = batch.Select(id => id).ToList();
                var results = await _apiClient.Universe.Names.PostAsync(body, cancellationToken: cancellationToken);
                if (results != null)
                {
                    foreach (var entry in results)
                    {
                        if (entry.Id.HasValue && entry.Name != null)
                            nameMap[(int)entry.Id.Value] = entry.Name;
                    }
                }
            }
            catch
            {
                // Skip failed batches
            }
        }

        _cache.Set(cacheKey, nameMap, TimeSpan.FromHours(24));
        return nameMap;
    }
}
