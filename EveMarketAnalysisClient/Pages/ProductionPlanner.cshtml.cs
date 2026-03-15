using System.Collections.Immutable;
using System.Security.Claims;
using System.Text.Json;
using EveMarketAnalysisClient.Models;
using EveMarketAnalysisClient.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EveMarketAnalysisClient.Pages;

[Authorize]
public class ProductionPlannerModel : PageModel
{
    private static readonly HashSet<int> ValidRegionIds = new(
        TradeHubRegion.All.Select(r => r.RegionId));

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IShoppingListService _shoppingListService;
    private readonly IEsiCharacterClient _esiClient;
    private readonly IBlueprintDataService _blueprintData;

    public string CharacterName { get; set; } = "Unknown";
    public int? CharacterId { get; set; }
    public string? ErrorMessage { get; set; }

    public ProductionPlannerModel(
        IShoppingListService shoppingListService,
        IEsiCharacterClient esiClient,
        IBlueprintDataService blueprintData)
    {
        _shoppingListService = shoppingListService;
        _esiClient = esiClient;
        _blueprintData = blueprintData;
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

    public async Task<IActionResult> OnGetBlueprintsAsync(
        CancellationToken cancellationToken = default)
    {
        var characterIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(characterIdClaim) || !int.TryParse(characterIdClaim, out var characterId))
            return new JsonResult(new { error = "Could not determine character identity." }) { StatusCode = 400 };

        try
        {
            var blueprints = await _esiClient.GetCharacterBlueprintsAsync(characterId, cancellationToken);

            var enriched = blueprints
                .Select(bp =>
                {
                    var activity = _blueprintData.GetBlueprintActivity(bp.TypeId);
                    if (activity == null) return (object?)null;

                    return new
                    {
                        typeId = bp.TypeId,
                        typeName = bp.TypeName,
                        materialEfficiency = bp.MaterialEfficiency,
                        timeEfficiency = bp.TimeEfficiency,
                        runs = bp.Runs,
                        isCopy = bp.IsCopy,
                        producedTypeId = activity.ProducedTypeId,
                        producedTypeName = string.Empty // Resolved client-side or via name cache
                    };
                })
                .Where(b => b != null)
                .ToList();

            return new JsonResult(new { blueprints = enriched });
        }
        catch (Exception ex)
        {
            return new JsonResult(new { error = $"Failed to load blueprints: {ex.Message}" }) { StatusCode = 500 };
        }
    }

    public async Task<IActionResult> OnGetShoppingListAsync(
        string selections = "",
        CancellationToken cancellationToken = default)
    {
        var characterIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(characterIdClaim) || !int.TryParse(characterIdClaim, out var characterId))
            return new JsonResult(new { error = "Could not determine character identity." }) { StatusCode = 400 };

        // Parse selections JSON
        List<SelectionInput>? selectionInputs;
        try
        {
            selectionInputs = JsonSerializer.Deserialize<List<SelectionInput>>(selections, JsonOptions);
        }
        catch
        {
            return new JsonResult(new { error = "Invalid selections format." }) { StatusCode = 400 };
        }

        if (selectionInputs == null || selectionInputs.Count == 0)
            return new JsonResult(new { error = "No blueprints selected." }) { StatusCode = 400 };

        try
        {
            // Look up blueprint details to build full BlueprintSelection records
            var blueprintSelections = new List<BlueprintSelection>();
            var characterBlueprints = await _esiClient.GetCharacterBlueprintsAsync(characterId, cancellationToken);
            var blueprintLookup = characterBlueprints
                .GroupBy(b => b.TypeId)
                .ToDictionary(g => g.Key, g => g.First());

            foreach (var input in selectionInputs)
            {
                if (!blueprintLookup.TryGetValue(input.BlueprintTypeId, out var bp))
                    continue;

                var activity = _blueprintData.GetBlueprintActivity(bp.TypeId);
                if (activity == null) continue;

                blueprintSelections.Add(new BlueprintSelection(
                    BlueprintTypeId: bp.TypeId,
                    BlueprintName: bp.TypeName,
                    MaterialEfficiency: bp.MaterialEfficiency,
                    TimeEfficiency: bp.TimeEfficiency,
                    Runs: input.Runs,
                    MaxRuns: bp.Runs,
                    IsCopy: bp.IsCopy,
                    ProduceComponents: input.ProduceComponents,
                    ProducedTypeId: activity.ProducedTypeId));
            }

            var response = await _shoppingListService.GenerateShoppingListAsync(
                blueprintSelections.ToImmutableArray(), characterId, cancellationToken);

            return new JsonResult(response);
        }
        catch (Exception ex)
        {
            return new JsonResult(new { error = $"Failed to generate shopping list: {ex.Message}" }) { StatusCode = 500 };
        }
    }

    public async Task<IActionResult> OnGetCostsAsync(
        int regionId = 10000002,
        string typeIds = "",
        CancellationToken cancellationToken = default)
    {
        var characterIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(characterIdClaim) || !int.TryParse(characterIdClaim, out _))
            return new JsonResult(new { error = "Could not determine character identity." }) { StatusCode = 400 };

        if (!ValidRegionIds.Contains(regionId))
            return new JsonResult(new { error = $"Invalid regionId: {regionId}" }) { StatusCode = 400 };

        var parsedIds = typeIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s.Trim(), out var id) ? id : (int?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToImmutableArray();

        if (parsedIds.IsEmpty)
            return new JsonResult(new { error = "No valid type IDs provided." }) { StatusCode = 400 };

        try
        {
            var costsTask = _shoppingListService.FetchCostsAsync(parsedIds, regionId, cancellationToken);
            var volumesTask = _shoppingListService.FetchVolumesAsync(parsedIds, cancellationToken);
            await Task.WhenAll(costsTask, volumesTask);

            var costs = costsTask.Result;
            var volumes = volumesTask.Result;

            var region = TradeHubRegion.All.FirstOrDefault(r => r.RegionId == regionId) ?? TradeHubRegion.Default;

            var costEntries = parsedIds.Select(id => new
            {
                typeId = id,
                unitCost = costs.TryGetValue(id, out var cost) ? cost : null,
                volume = volumes.TryGetValue(id, out var vol) ? vol : 0.0,
                available = costs.TryGetValue(id, out var c) && c.HasValue
            }).ToList();

            return new JsonResult(new
            {
                regionId,
                regionName = region.RegionName,
                costs = costEntries,
                fetchedAt = DateTimeOffset.UtcNow
            });
        }
        catch (Exception ex)
        {
            return new JsonResult(new { error = $"Failed to fetch costs: {ex.Message}" }) { StatusCode = 500 };
        }
    }

    private record SelectionInput(
        int BlueprintTypeId = 0,
        int Runs = 1,
        bool ProduceComponents = false);
}
