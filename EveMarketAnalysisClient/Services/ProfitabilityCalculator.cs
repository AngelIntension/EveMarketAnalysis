using System.Collections.Immutable;
using EveMarketAnalysisClient.Models;
using EveMarketAnalysisClient.Services.Interfaces;
using EveStableInfrastructure;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace EveMarketAnalysisClient.Services;

public class ProfitabilityCalculator : IProfitabilityCalculator
{
    private readonly IBlueprintDataService _blueprintData;
    private readonly IEsiMarketClient _marketClient;
    private readonly ApiClient _apiClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ProfitabilityCalculator> _logger;
    private static readonly SemaphoreSlim ConcurrencyLimiter = new(20, 20);
    private static readonly TimeSpan NameCacheDuration = TimeSpan.FromHours(24);

    public ProfitabilityCalculator(
        IBlueprintDataService blueprintData,
        IEsiMarketClient marketClient,
        ApiClient apiClient,
        IMemoryCache cache,
        ILogger<ProfitabilityCalculator> logger)
    {
        _blueprintData = blueprintData;
        _marketClient = marketClient;
        _apiClient = apiClient;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ImmutableArray<ProfitabilityResult>> CalculateAsync(
        ImmutableArray<CharacterBlueprint> blueprints,
        ProfitabilitySettings settings,
        CancellationToken cancellationToken = default)
    {
        if (blueprints.IsEmpty)
            return ImmutableArray<ProfitabilityResult>.Empty;

        // 1. Look up blueprint activities and collect all type IDs
        var blueprintActivities = new List<(CharacterBlueprint Blueprint, BlueprintActivity Activity)>();
        var errorResults = new List<ProfitabilityResult>();
        var allTypeIds = new HashSet<int>();

        foreach (var bp in blueprints)
        {
            var activity = _blueprintData.GetBlueprintActivity(bp.TypeId);
            if (activity == null)
            {
                errorResults.Add(CreateErrorResult(bp, "No manufacturing data found for this blueprint"));
                continue;
            }

            blueprintActivities.Add((bp, activity));
            allTypeIds.Add(activity.ProducedTypeId);
            foreach (var mat in activity.Materials)
                allTypeIds.Add(mat.TypeId);
        }

        // 2. Fetch market snapshots for all unique types in parallel
        var marketSnapshots = new Dictionary<int, MarketSnapshot>();
        var marketTasks = allTypeIds.Select(async typeId =>
        {
            await ConcurrencyLimiter.WaitAsync(cancellationToken);
            try
            {
                var snapshot = await _marketClient.GetMarketSnapshotAsync(settings.RegionId, typeId, cancellationToken);
                return (typeId, snapshot: (MarketSnapshot?)snapshot);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch market data for type {TypeId}", typeId);
                return (typeId, snapshot: (MarketSnapshot?)null);
            }
            finally
            {
                ConcurrencyLimiter.Release();
            }
        });

        var snapshots = await Task.WhenAll(marketTasks);
        foreach (var (typeId, snapshot) in snapshots)
        {
            if (snapshot != null)
                marketSnapshots[typeId] = snapshot;
        }

        // 3. Resolve type names for all types via bulk POST /universe/names
        Dictionary<int, string> typeNames;
        try
        {
            typeNames = await ResolveTypeNamesAsync(allTypeIds, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve type names");
            typeNames = new Dictionary<int, string>();
        }

        // 4. Calculate profitability for each blueprint
        var results = new List<ProfitabilityResult>();
        foreach (var (bp, activity) in blueprintActivities)
        {
            try
            {
                var result = CalculateSingle(bp, activity, settings, marketSnapshots, typeNames);
                results.Add(result);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to calculate profitability for blueprint {TypeId}", bp.TypeId);
                errorResults.Add(CreateErrorResult(bp, $"Calculation failed: {ex.Message}"));
            }
        }

        // 5. Sort by ISK/hour descending, limit to top 50
        var sorted = results
            .Where(r => r.HasMarketData)
            .OrderByDescending(r => r.IskPerHour)
            .Take(50)
            .Concat(results.Where(r => !r.HasMarketData))
            .Concat(errorResults)
            .ToImmutableArray();

        return sorted;
    }

    private ProfitabilityResult CalculateSingle(
        CharacterBlueprint bp,
        BlueprintActivity activity,
        ProfitabilitySettings settings,
        Dictionary<int, MarketSnapshot> snapshots,
        Dictionary<int, string> typeNames)
    {
        // ME-adjusted material quantities
        var adjustedMaterials = activity.Materials
            .Select(mat =>
            {
                var adjustedQty = Math.Max(1, (int)Math.Ceiling(mat.BaseQuantity * (1.0 - bp.MaterialEfficiency / 100.0)));
                var name = typeNames.TryGetValue(mat.TypeId, out var n) ? n : $"Type {mat.TypeId}";
                return mat with { AdjustedQuantity = adjustedQty, TypeName = name };
            })
            .ToImmutableArray();

        // TE-adjusted production time
        var productionTimeSeconds = (int)(activity.BaseTime * (1.0 - bp.TimeEfficiency / 100.0));

        // Material cost
        var totalMaterialCost = 0m;
        var hasMaterialPrices = true;
        foreach (var mat in adjustedMaterials)
        {
            if (snapshots.TryGetValue(mat.TypeId, out var matSnapshot) && matSnapshot.LowestSellPrice.HasValue)
            {
                totalMaterialCost += matSnapshot.LowestSellPrice.Value * mat.AdjustedQuantity;
            }
            else
            {
                hasMaterialPrices = false;
            }
        }

        // Product sell value: prefer highest buy, fall back to lowest sell
        var productSnapshot = snapshots.GetValueOrDefault(activity.ProducedTypeId);
        var productSellValue = 0m;
        var hasMarketData = false;
        var averageDailyVolume = 0.0;

        if (productSnapshot != null)
        {
            productSellValue = productSnapshot.HighestBuyPrice ?? productSnapshot.LowestSellPrice ?? 0m;
            hasMarketData = productSellValue > 0;
            averageDailyVolume = productSnapshot.AverageDailyVolume;
        }

        // Tax and fees
        var taxAmount = productSellValue * settings.TaxRate;
        var installationFee = productSellValue * settings.InstallationFeeRate;

        // Profit
        var grossProfit = productSellValue - totalMaterialCost - taxAmount - installationFee;
        var profitMarginPercent = totalMaterialCost > 0
            ? (double)(grossProfit / totalMaterialCost) * 100.0
            : 0.0;
        var iskPerHour = productionTimeSeconds > 0
            ? (double)grossProfit / (productionTimeSeconds / 3600.0)
            : 0.0;

        var producedName = typeNames.TryGetValue(activity.ProducedTypeId, out var pName) ? pName : $"Type {activity.ProducedTypeId}";

        string? errorMessage = null;
        if (!hasMarketData)
            errorMessage = "No market data available for product";
        else if (!hasMaterialPrices)
            errorMessage = "Some material prices unavailable — cost is an estimate";

        return new ProfitabilityResult(
            Blueprint: bp,
            ProducedTypeName: producedName,
            ProducedTypeId: activity.ProducedTypeId,
            Materials: adjustedMaterials,
            TotalMaterialCost: totalMaterialCost,
            ProductSellValue: productSellValue * activity.ProducedQuantity,
            TaxAmount: taxAmount,
            InstallationFee: installationFee,
            GrossProfit: grossProfit,
            ProfitMarginPercent: profitMarginPercent,
            ProductionTimeSeconds: productionTimeSeconds,
            IskPerHour: iskPerHour,
            AverageDailyVolume: averageDailyVolume,
            HasMarketData: hasMarketData,
            ErrorMessage: errorMessage);
    }

    private async Task<Dictionary<int, string>> ResolveTypeNamesAsync(
        HashSet<int> typeIds, CancellationToken cancellationToken)
    {
        const string cacheKey = "esi:typenames";
        var nameCache = _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = NameCacheDuration;
            return new Dictionary<int, string>();
        })!;

        var idsToFetch = typeIds.Where(id => !nameCache.ContainsKey(id)).ToList();

        if (idsToFetch.Count > 0)
        {
            // ESI /universe/names accepts max 1000 IDs per call
            foreach (var batch in idsToFetch.Chunk(1000))
            {
                try
                {
                    var body = batch.Select(id => (long?)id).ToList();
                    var results = await _apiClient.Universe.Names.PostAsync(body, cancellationToken: cancellationToken);
                    if (results != null)
                    {
                        foreach (var entry in results)
                        {
                            if (entry.Id.HasValue && entry.Name != null)
                                nameCache[(int)entry.Id.Value] = entry.Name;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to resolve names for batch of {Count} IDs", batch.Length);
                }
            }

            foreach (var id in idsToFetch)
                nameCache.TryAdd(id, $"Type {id}");
        }

        return nameCache;
    }

    private static ProfitabilityResult CreateErrorResult(CharacterBlueprint bp, string errorMessage)
    {
        return new ProfitabilityResult(
            Blueprint: bp,
            ProducedTypeName: string.Empty,
            ProducedTypeId: 0,
            Materials: ImmutableArray<MaterialRequirement>.Empty,
            TotalMaterialCost: 0,
            ProductSellValue: 0,
            TaxAmount: 0,
            InstallationFee: 0,
            GrossProfit: 0,
            ProfitMarginPercent: 0,
            ProductionTimeSeconds: 0,
            IskPerHour: 0,
            AverageDailyVolume: 0,
            HasMarketData: false,
            ErrorMessage: errorMessage);
    }
}
