using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using EveMarketAnalysisClient.Models;
using EveMarketAnalysisClient.Services.Interfaces;
using EveStableInfrastructure;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace EveMarketAnalysisClient.Services;

public class PortfolioAnalyzer : IPortfolioAnalyzer
{
    private readonly IBlueprintDataService _blueprintData;
    private readonly IEsiMarketClient _marketClient;
    private readonly IEsiCharacterClient _characterClient;
    private readonly IPhaseService _phaseService;
    private readonly ApiClient _apiClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PortfolioAnalyzer> _logger;
    private static readonly SemaphoreSlim ConcurrencyLimiter = new(20, 20);
    private static readonly TimeSpan CostIndexCacheDuration = TimeSpan.FromHours(1);
    private static readonly TimeSpan NameCacheDuration = TimeSpan.FromHours(24);

    private readonly Lazy<IReadOnlyDictionary<int, ImmutableArray<SkillRequirement>>> _skillRequirements;

    public PortfolioAnalyzer(
        IBlueprintDataService blueprintData,
        IEsiMarketClient marketClient,
        IEsiCharacterClient characterClient,
        IPhaseService phaseService,
        ApiClient apiClient,
        IMemoryCache cache,
        ILogger<PortfolioAnalyzer> logger)
    {
        _blueprintData = blueprintData;
        _marketClient = marketClient;
        _characterClient = characterClient;
        _phaseService = phaseService;
        _apiClient = apiClient;
        _cache = cache;
        _logger = logger;
        _skillRequirements = new Lazy<IReadOnlyDictionary<int, ImmutableArray<SkillRequirement>>>(LoadSkillRequirements);
    }

    public async Task<PortfolioAnalysis> AnalyzeAsync(
        int characterId,
        PortfolioConfiguration configuration,
        int? phaseOverride = null,
        bool simulateNextPhase = false,
        CancellationToken cancellationToken = default)
    {
        // 1. Fetch blueprints and skills in parallel
        var blueprintsTask = _characterClient.GetCharacterBlueprintsAsync(characterId, cancellationToken);
        var skillsTask = _characterClient.GetCharacterSkillsAsync(characterId, cancellationToken);
        await Task.WhenAll(blueprintsTask, skillsTask);

        var blueprints = blueprintsTask.Result;
        var skills = skillsTask.Result;

        var portfolioSizeWarning = blueprints.Length > 300;

        if (blueprints.IsEmpty)
        {
            return CreateEmptyAnalysis(portfolioSizeWarning);
        }

        // 2. Build skill lookup
        var skillLookup = skills.ToDictionary(s => s.SkillId, s => s.TrainedLevel);

        // 3. Filter blueprints by skill requirements and resolve activities
        var eligibleBlueprints = new List<(CharacterBlueprint Blueprint, BlueprintActivity Activity)>();
        var errorResults = new List<BlueprintRankingEntry>();
        var allTypeIds = new HashSet<int>();

        foreach (var bp in blueprints)
        {
            var activity = _blueprintData.GetBlueprintActivity(bp.TypeId);
            if (activity == null)
            {
                errorResults.Add(CreateErrorEntry(bp, "No manufacturing data found"));
                continue;
            }

            // Skill gating
            if (!MeetsSkillRequirements(bp.TypeId, skillLookup))
                continue;

            eligibleBlueprints.Add((bp, activity));
            allTypeIds.Add(activity.ProducedTypeId);
            foreach (var mat in activity.Materials)
                allTypeIds.Add(mat.TypeId);
        }

        // 4. Fetch market snapshots for all unique types in parallel
        var marketSnapshots = await FetchMarketSnapshotsAsync(
            allTypeIds, configuration.ProcurementRegionId, cancellationToken);

        // Also fetch selling hub snapshots if different region
        Dictionary<int, MarketSnapshot>? sellingSnapshots = null;
        if (configuration.SellingHubRegionId != configuration.ProcurementRegionId)
        {
            sellingSnapshots = await FetchMarketSnapshotsAsync(
                allTypeIds, configuration.SellingHubRegionId, cancellationToken);
        }

        // 5. Fetch system cost index and adjusted prices (for EIV calculation)
        var costIndex = await GetManufacturingCostIndexAsync(
            configuration.ManufacturingSystemId, cancellationToken);
        var adjustedPrices = await GetAdjustedPricesAsync(cancellationToken);

        // 6. Resolve type names
        var typeNames = await ResolveTypeNamesAsync(allTypeIds, cancellationToken);

        // 7. Calculate rankings
        var rankings = new List<BlueprintRankingEntry>();
        var phases = _phaseService.GetAllPhases();

        // Determine current phase
        var currentPhaseNumber = phaseOverride ?? 1;

        foreach (var (bp, activity) in eligibleBlueprints)
        {
            try
            {
                var entry = CalculateRankingEntry(
                    bp, activity, configuration, marketSnapshots, sellingSnapshots,
                    typeNames, costIndex, adjustedPrices, currentPhaseNumber);
                rankings.Add(entry);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to calculate ranking for blueprint {TypeId}", bp.TypeId);
                errorResults.Add(CreateErrorEntry(bp, $"Calculation failed: {ex.Message}"));
            }
        }

        // 8. Sort by ISK/hr descending
        var sortedRankings = rankings
            .Where(r => r.HasMarketData)
            .OrderByDescending(r => r.IskPerHour)
            .Concat(rankings.Where(r => !r.HasMarketData))
            .Concat(errorResults)
            .ToImmutableArray();

        // 9. Determine initial phase (before exhaustion check)
        var ownedTypeIdSet = blueprints.Select(b => b.TypeId).ToHashSet();
        var initialPhaseStatuses = EvaluatePhaseStatuses(sortedRankings, phases, configuration,
            ImmutableArray<BpoPurchaseRecommendation>.Empty, 0);
        currentPhaseNumber = DetermineCurrentPhase(initialPhaseStatuses, phaseOverride);
        var phaseOverrideActive = phaseOverride.HasValue;

        // 10. Resolve names for BPO recommendation candidates (capped to 100)
        var phaseCandidates = _phaseService.GetCandidateTypeIdsForPhase(currentPhaseNumber);
        if (phaseCandidates.Length > 0)
        {
            var bpoTypeIds = new HashSet<int>();
            var count = 0;
            foreach (var candidateId in phaseCandidates)
            {
                if (count >= 100) break;
                if (!ownedTypeIdSet.Contains(candidateId))
                {
                    bpoTypeIds.Add(candidateId);
                    var act = _blueprintData.GetBlueprintActivity(candidateId);
                    if (act != null)
                        bpoTypeIds.Add(act.ProducedTypeId);
                    count++;
                }
            }
            if (bpoTypeIds.Count > 0)
                await ResolveTypeNamesAsync(bpoTypeIds, cancellationToken);
        }

        // 11. Generate BPO recommendations
        var bpoRecommendations = await GenerateBpoRecommendationsAsync(
            blueprints, currentPhaseNumber, phases, configuration,
            marketSnapshots, typeNames, costIndex, adjustedPrices, cancellationToken);

        // 12. Re-evaluate phase statuses with exhaustion check using BPO results
        var phaseStatuses = EvaluatePhaseStatuses(sortedRankings, phases, configuration,
            bpoRecommendations, currentPhaseNumber);

        // If current phase is now exhausted, advance and regenerate BPO recommendations for new phase
        var finalPhaseNumber = DetermineCurrentPhase(phaseStatuses, phaseOverride);
        if (finalPhaseNumber != currentPhaseNumber)
        {
            currentPhaseNumber = finalPhaseNumber;

            // Resolve names for new phase candidates
            var newPhaseCandidates = _phaseService.GetCandidateTypeIdsForPhase(currentPhaseNumber);
            if (newPhaseCandidates.Length > 0)
            {
                var newBpoTypeIds = new HashSet<int>();
                var newCount = 0;
                foreach (var candidateId in newPhaseCandidates)
                {
                    if (newCount >= 100) break;
                    if (!ownedTypeIdSet.Contains(candidateId))
                    {
                        newBpoTypeIds.Add(candidateId);
                        var act = _blueprintData.GetBlueprintActivity(candidateId);
                        if (act != null)
                            newBpoTypeIds.Add(act.ProducedTypeId);
                        newCount++;
                    }
                }
                if (newBpoTypeIds.Count > 0)
                    await ResolveTypeNamesAsync(newBpoTypeIds, cancellationToken);
            }

            bpoRecommendations = await GenerateBpoRecommendationsAsync(
                blueprints, currentPhaseNumber, phases, configuration,
                marketSnapshots, typeNames, costIndex, adjustedPrices, cancellationToken);
        }

        if (simulateNextPhase && currentPhaseNumber < 5)
            currentPhaseNumber++;

        // 13. Generate research recommendations
        var researchRecommendations = GenerateResearchRecommendations(
            eligibleBlueprints, configuration, marketSnapshots, sellingSnapshots,
            typeNames, costIndex, adjustedPrices);

        var successCount = sortedRankings.Count(r => r.ErrorMessage == null);
        var errorCount = sortedRankings.Count(r => r.ErrorMessage != null);

        return new PortfolioAnalysis(
            Rankings: sortedRankings,
            PhaseStatuses: phaseStatuses,
            CurrentPhaseNumber: currentPhaseNumber,
            PhaseOverrideActive: phaseOverrideActive,
            BpoRecommendations: bpoRecommendations,
            ResearchRecommendations: researchRecommendations,
            TotalBlueprintsEvaluated: blueprints.Length,
            SuccessCount: successCount,
            ErrorCount: errorCount,
            PortfolioSizeWarning: portfolioSizeWarning,
            FetchedAt: DateTimeOffset.UtcNow);
    }

    private BlueprintRankingEntry CalculateRankingEntry(
        CharacterBlueprint bp,
        BlueprintActivity activity,
        PortfolioConfiguration config,
        Dictionary<int, MarketSnapshot> procurementSnapshots,
        Dictionary<int, MarketSnapshot>? sellingSnapshots,
        Dictionary<int, string> typeNames,
        double costIndex,
        Dictionary<int, decimal> adjustedPrices,
        int currentPhaseNumber)
    {
        var effectiveME = config.WhatIfME ?? bp.MaterialEfficiency;
        var effectiveTE = config.WhatIfTE ?? bp.TimeEfficiency;

        // Use shared calculation for all cost/profit math
        var calc = CalculateDetailed(activity, effectiveME, effectiveTE, config,
            procurementSnapshots, costIndex, sellingSnapshots, adjustedPrices);

        var iskPerHour = calc.ProductionTimeSeconds > 0
            ? calc.GrossProfit / (decimal)(calc.ProductionTimeSeconds / 3600.0) : 0m;

        // Phase assignment
        var phase = _phaseService.GetPhaseForTypeId(bp.TypeId);

        var producedName = typeNames.GetValueOrDefault(activity.ProducedTypeId, $"Type {activity.ProducedTypeId}");
        var profitMarginPercent = calc.MaterialCost > 0 ? calc.GrossProfit / calc.MaterialCost * 100m : 0m;

        string? errorMessage = null;
        if (!calc.HasMarketData)
            errorMessage = "No market data available for product";
        else if (!calc.HasAllMaterialPrices)
            errorMessage = "Some material prices unavailable — cost is an estimate";

        return new BlueprintRankingEntry(
            Blueprint: bp,
            ProducedTypeName: producedName,
            ProducedTypeId: activity.ProducedTypeId,
            PhaseNumber: phase?.PhaseNumber,
            MaterialCost: calc.MaterialCost,
            ProductRevenue: calc.ProductRevenue,
            BuyingBrokerFee: calc.BuyingBrokerFee,
            SellingBrokerFee: calc.SellingBrokerFee,
            SalesTax: calc.SalesTax,
            SystemCostFee: calc.SystemCostFee,
            FacilityTax: calc.FacilityTax,
            SccSurcharge: calc.SccSurcharge,
            GrossProfit: calc.GrossProfit,
            ProfitMarginPercent: profitMarginPercent,
            ProductionTimeSeconds: calc.ProductionTimeSeconds,
            IskPerHour: iskPerHour,
            AverageDailyVolume: calc.AverageDailyVolume,
            IsCurrentPhase: phase?.PhaseNumber == currentPhaseNumber,
            MeetsThreshold: iskPerHour >= config.MinIskPerHour,
            HasMarketData: calc.HasMarketData,
            ErrorMessage: errorMessage);
    }

    private ImmutableArray<PhaseStatus> EvaluatePhaseStatuses(
        ImmutableArray<BlueprintRankingEntry> rankings,
        ImmutableArray<PhaseDefinition> phases,
        PortfolioConfiguration config,
        ImmutableArray<BpoPurchaseRecommendation> bpoRecommendations,
        int bpoRecommendationPhase)
    {
        var requiredCount = (int)Math.Ceiling(config.ManufacturingSlots * 9.0 / 11.0);

        return phases.Select(phase =>
        {
            var phaseRankings = rankings.Where(r =>
                r.PhaseNumber == phase.PhaseNumber && r.HasMarketData && r.ErrorMessage == null);

            var ownedProfitableAny = phaseRankings.Count(r => r.HasMarketData && r.IskPerHour > 0);
            var ownedProfitable = phaseRankings.Count(r => r.MeetsThreshold);
            var dailyIncome = phaseRankings
                .Where(r => r.MeetsThreshold)
                .Sum(r => r.IskPerHour * 24m);

            // Phase is exhausted if we evaluated BPO recommendations for it
            // and found zero profitable unowned BPOs (user owns at least some profitable BPs)
            var isPhaseExhausted = phase.PhaseNumber == bpoRecommendationPhase
                && ownedProfitableAny > 0
                && !bpoRecommendations.Any(r => r.HasMarketData && r.ProjectedIskPerHour >= config.MinIskPerHour);

            var isSlotComplete = ownedProfitable >= requiredCount;
            var isIncomeComplete = dailyIncome >= config.DailyIncomeGoal;
            var isComplete = isSlotComplete || isIncomeComplete || isPhaseExhausted;

            string? completionReason = null;
            if (isSlotComplete) completionReason = "slots";
            else if (isIncomeComplete) completionReason = "income";
            else if (isPhaseExhausted) completionReason = "exhausted";

            return new PhaseStatus(
                Phase: phase,
                OwnedProfitableCount: ownedProfitableAny,
                RequiredCount: requiredCount,
                IsComplete: isComplete,
                DailyPotentialIncome: dailyIncome,
                CompletionReason: completionReason);
        }).ToImmutableArray();
    }

    private static int DetermineCurrentPhase(
        ImmutableArray<PhaseStatus> phaseStatuses, int? phaseOverride)
    {
        if (phaseOverride.HasValue && phaseOverride.Value >= 1 && phaseOverride.Value <= 5)
            return phaseOverride.Value;

        // Find the first incomplete phase
        foreach (var status in phaseStatuses)
        {
            if (!status.IsComplete)
                return status.Phase.PhaseNumber;
        }

        // All phases complete — stay at 5
        return 5;
    }

    private async Task<ImmutableArray<BpoPurchaseRecommendation>> GenerateBpoRecommendationsAsync(
        ImmutableArray<CharacterBlueprint> ownedBlueprints,
        int recommendationPhase,
        ImmutableArray<PhaseDefinition> phases,
        PortfolioConfiguration config,
        Dictionary<int, MarketSnapshot> marketSnapshots,
        Dictionary<int, string> typeNames,
        double costIndex,
        Dictionary<int, decimal> adjustedPrices,
        CancellationToken cancellationToken)
    {
        var phaseCandidates = _phaseService.GetCandidateTypeIdsForPhase(recommendationPhase);
        if (phaseCandidates.IsEmpty)
            return ImmutableArray<BpoPurchaseRecommendation>.Empty;

        var ownedTypeIds = ownedBlueprints.Select(b => b.TypeId).ToHashSet();
        var unownedTypeIds = phaseCandidates
            .Where(id => !ownedTypeIds.Contains(id))
            .Take(100) // Cap to avoid excessive API calls
            .ToList();

        if (unownedTypeIds.Count == 0)
            return ImmutableArray<BpoPurchaseRecommendation>.Empty;

        // Fetch market data for unowned candidates' materials and products
        var additionalTypeIds = new HashSet<int>();
        foreach (var bpTypeId in unownedTypeIds)
        {
            var act = _blueprintData.GetBlueprintActivity(bpTypeId);
            if (act == null) continue;
            additionalTypeIds.Add(act.ProducedTypeId);
            foreach (var mat in act.Materials)
                additionalTypeIds.Add(mat.TypeId);
        }
        var missingTypeIds = new HashSet<int>(additionalTypeIds.Where(id => !marketSnapshots.ContainsKey(id)));
        if (missingTypeIds.Count > 0)
        {
            var additionalSnapshots = await FetchMarketSnapshotsAsync(
                missingTypeIds, config.ProcurementRegionId, cancellationToken);
            foreach (var (id, snapshot) in additionalSnapshots)
                marketSnapshots.TryAdd(id, snapshot);
        }

        var recommendations = new List<BpoPurchaseRecommendation>();
        foreach (var bpTypeId in unownedTypeIds)
        {
            var activity = _blueprintData.GetBlueprintActivity(bpTypeId);
            if (activity == null) continue;

            var producedName = typeNames.GetValueOrDefault(activity.ProducedTypeId, $"Type {activity.ProducedTypeId}");
            var bpName = typeNames.GetValueOrDefault(bpTypeId, $"Blueprint {bpTypeId}");

            // Calculate projected ISK/hr at ME10/TE20
            var projectedCalc = CalculateDetailed(
                activity, 10, 20, config, marketSnapshots, costIndex, adjustedPrices: adjustedPrices);
            var projectedIskPerHour = CalculateIskPerHour(projectedCalc);

            // Fetch region-wide BPO market data (includes NPC detection via order duration)
            MarketSnapshot? bpoSnapshot = null;
            try
            {
                bpoSnapshot = await _marketClient.GetRegionMarketSnapshotAsync(
                    config.ProcurementRegionId, bpTypeId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch BPO market price for {TypeId}", bpTypeId);
            }

            // NPC price: detected from sell orders with duration > 90 days
            var npcPrice = bpoSnapshot?.NpcSellPrice;

            // Player price: lowest sell excluding NPC orders
            // If NPC and lowest sell are the same, there are no player orders below NPC
            var playerPrice = bpoSnapshot?.LowestSellPrice;

            var buyPrice = playerPrice ?? npcPrice;

            decimal? paybackDays = null;
            decimal? roiPercent = null;
            if (buyPrice.HasValue && buyPrice.Value > 0 && projectedIskPerHour > 0)
            {
                var dailyProfit = projectedIskPerHour * 24m;
                paybackDays = buyPrice.Value / dailyProfit;
                roiPercent = dailyProfit * 30m / buyPrice.Value * 100m;
            }

            recommendations.Add(new BpoPurchaseRecommendation(
                BlueprintTypeId: bpTypeId,
                BlueprintName: bpName,
                ProducedTypeName: producedName,
                PhaseNumber: recommendationPhase,
                NpcSeededPrice: npcPrice,
                PlayerMarketPrice: playerPrice,
                ProjectedIskPerHour: projectedIskPerHour,
                PaybackPeriodDays: paybackDays,
                RoiPercent: roiPercent,
                HasMarketData: projectedIskPerHour > 0,
                ErrorMessage: null));
        }

        return recommendations
            .OrderByDescending(r => r.ProjectedIskPerHour)
            .ToImmutableArray();
    }

    private ImmutableArray<ResearchRecommendation> GenerateResearchRecommendations(
        List<(CharacterBlueprint Blueprint, BlueprintActivity Activity)> eligibleBlueprints,
        PortfolioConfiguration config,
        Dictionary<int, MarketSnapshot> procurementSnapshots,
        Dictionary<int, MarketSnapshot>? sellingSnapshots,
        Dictionary<int, string> typeNames,
        double costIndex,
        Dictionary<int, decimal> adjustedPrices)
    {
        var recommendations = new List<ResearchRecommendation>();

        foreach (var (bp, activity) in eligibleBlueprints)
        {
            if (bp.MaterialEfficiency >= 10 && bp.TimeEfficiency >= 20)
                continue;

            var currentIskPerHour = CalculateIskPerHour(CalculateDetailed(
                activity, bp.MaterialEfficiency, bp.TimeEfficiency,
                config, procurementSnapshots, costIndex, sellingSnapshots, adjustedPrices));

            var projectedIskPerHour = CalculateIskPerHour(CalculateDetailed(
                activity, 10, 20, config, procurementSnapshots, costIndex, sellingSnapshots, adjustedPrices));

            var gain = projectedIskPerHour - currentIskPerHour;
            if (gain <= 0) continue;

            var gainPercent = currentIskPerHour > 0 ? gain / currentIskPerHour * 100m : 0m;
            var producedName = typeNames.GetValueOrDefault(activity.ProducedTypeId, $"Type {activity.ProducedTypeId}");

            recommendations.Add(new ResearchRecommendation(
                Blueprint: bp,
                ProducedTypeName: producedName,
                CurrentIskPerHour: currentIskPerHour,
                ProjectedIskPerHour: projectedIskPerHour,
                IskPerHourGain: gain,
                GainPercent: gainPercent,
                CurrentME: bp.MaterialEfficiency,
                CurrentTE: bp.TimeEfficiency,
                TargetME: 10,
                TargetTE: 20));
        }

        return recommendations
            .OrderByDescending(r => r.IskPerHourGain)
            .Take(10)
            .ToImmutableArray();
    }

    private record CalculationResult(
        decimal MaterialCost,
        decimal ProductRevenue,
        decimal BuyingBrokerFee,
        decimal SellingBrokerFee,
        decimal SalesTax,
        decimal SystemCostFee,
        decimal FacilityTax,
        decimal SccSurcharge,
        decimal GrossProfit,
        double ProductionTimeSeconds,
        double AverageDailyVolume,
        bool HasMarketData,
        bool HasAllMaterialPrices);

    private static CalculationResult CalculateDetailed(
        BlueprintActivity activity,
        int me, int te,
        PortfolioConfiguration config,
        Dictionary<int, MarketSnapshot> procurementSnapshots,
        double costIndex,
        Dictionary<int, MarketSnapshot>? sellingSnapshots = null,
        Dictionary<int, decimal>? adjustedPrices = null)
    {
        var materialCost = 0m;
        var estimatedItemValue = 0m;
        var hasAllMaterialPrices = true;
        foreach (var mat in activity.Materials)
        {
            var adjustedQty = Math.Max(1, (int)Math.Ceiling(mat.BaseQuantity * (1.0 - me / 100.0)));
            if (procurementSnapshots.TryGetValue(mat.TypeId, out var matSnapshot) && matSnapshot.HighestBuyPrice.HasValue)
                materialCost += matSnapshot.HighestBuyPrice.Value * adjustedQty;
            else
                hasAllMaterialPrices = false;

            if (adjustedPrices != null && adjustedPrices.TryGetValue(mat.TypeId, out var adjPrice))
                estimatedItemValue += adjPrice * mat.BaseQuantity;
        }

        var productSnapshots = sellingSnapshots ?? procurementSnapshots;
        var productSnapshot = productSnapshots.GetValueOrDefault(activity.ProducedTypeId);
        var unitPrice = productSnapshot?.LowestSellPrice ?? 0m;
        var productRevenue = unitPrice * activity.ProducedQuantity;
        var hasMarketData = unitPrice > 0;
        var averageDailyVolume = productSnapshot?.AverageDailyVolume ?? 0.0;

        var buyingBrokerFee = materialCost * (config.BuyingBrokerFeePercent / 100m);
        var sellingBrokerFee = productRevenue * (config.SellingBrokerFeePercent / 100m);
        var salesTax = productRevenue * (config.SalesTaxPercent / 100m);
        var systemCostFee = estimatedItemValue * (decimal)costIndex;
        var facilityTax = estimatedItemValue * (config.FacilityTaxPercent / 100m);
        var sccSurcharge = estimatedItemValue * (config.SccSurchargePercent / 100m);

        var grossProfit = productRevenue - materialCost - buyingBrokerFee - sellingBrokerFee - salesTax - systemCostFee - facilityTax - sccSurcharge;
        var productionTimeSeconds = activity.BaseTime * (1.0 - te / 100.0);

        return new CalculationResult(materialCost, productRevenue, buyingBrokerFee, sellingBrokerFee,
            salesTax, systemCostFee, facilityTax, sccSurcharge, grossProfit, productionTimeSeconds,
            averageDailyVolume, hasMarketData, hasAllMaterialPrices);
    }

    private static decimal CalculateIskPerHour(CalculationResult calc)
    {
        return calc.ProductionTimeSeconds > 0
            ? calc.GrossProfit / (decimal)(calc.ProductionTimeSeconds / 3600.0) : 0m;
    }

    private async Task<Dictionary<int, MarketSnapshot>> FetchMarketSnapshotsAsync(
        HashSet<int> typeIds, int regionId, CancellationToken cancellationToken)
    {
        var snapshots = new Dictionary<int, MarketSnapshot>();
        var tasks = typeIds.Select(async typeId =>
        {
            await ConcurrencyLimiter.WaitAsync(cancellationToken);
            try
            {
                var snapshot = await _marketClient.GetMarketSnapshotAsync(regionId, typeId, cancellationToken);
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

        var results = await Task.WhenAll(tasks);
        foreach (var (typeId, snapshot) in results)
        {
            if (snapshot != null)
                snapshots[typeId] = snapshot;
        }

        return snapshots;
    }

    private async Task<Dictionary<int, decimal>> GetAdjustedPricesAsync(CancellationToken cancellationToken)
    {
        const string cacheKey = "esi:adjustedprices";
        if (_cache.TryGetValue(cacheKey, out Dictionary<int, decimal>? cached) && cached != null)
            return cached;

        try
        {
            var prices = await _apiClient.Markets.Prices.GetAsync(cancellationToken: cancellationToken);
            if (prices == null)
                return new Dictionary<int, decimal>();

            var result = new Dictionary<int, decimal>();
            foreach (var p in prices)
            {
                if (p.TypeId.HasValue && p.AdjustedPrice.HasValue)
                    result[(int)p.TypeId.Value] = (decimal)p.AdjustedPrice.Value;
            }

            _cache.Set(cacheKey, result, CostIndexCacheDuration);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch adjusted prices");
            return new Dictionary<int, decimal>();
        }
    }

    private async Task<double> GetManufacturingCostIndexAsync(
        int solarSystemId, CancellationToken cancellationToken)
    {
        var cacheKey = "esi:costindex";
        if (_cache.TryGetValue(cacheKey, out Dictionary<int, double>? costIndices) && costIndices != null)
        {
            return costIndices.GetValueOrDefault(solarSystemId);
        }

        try
        {
            var systems = await _apiClient.Industry.Systems.GetAsync(cancellationToken: cancellationToken);
            if (systems == null)
                return 0.0;

            var indices = new Dictionary<int, double>();
            foreach (var system in systems)
            {
                if (system.SolarSystemId == null) continue;
                var mfgActivity = system.CostIndices?.FirstOrDefault(c =>
                    c.Activity == EveStableInfrastructure.Models.IndustrySystemsGet_cost_indices_activity.Manufacturing);
                if (mfgActivity?.CostIndex != null)
                    indices[(int)system.SolarSystemId] = (double)mfgActivity.CostIndex;
            }

            _cache.Set(cacheKey, indices, CostIndexCacheDuration);
            return indices.GetValueOrDefault(solarSystemId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch industry cost indices");
            return 0.0;
        }
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
            foreach (var batch in idsToFetch.Chunk(1000))
            {
                await ResolveBatchNamesAsync(batch, nameCache, cancellationToken);
            }

            foreach (var id in idsToFetch)
                nameCache.TryAdd(id, $"Type {id}");
        }

        return nameCache;
    }

    private async Task ResolveBatchNamesAsync(
        int[] batch, Dictionary<int, string> nameCache, CancellationToken cancellationToken)
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
            _logger.LogWarning(ex, "Failed to resolve names for batch of {Count} IDs, retrying individually", batch.Length);

            // ESI /universe/names returns 404 if any ID is invalid — retry one by one
            if (batch.Length > 1)
            {
                foreach (var id in batch)
                {
                    if (nameCache.ContainsKey(id)) continue;
                    try
                    {
                        var singleBody = new List<long?> { id };
                        var singleResult = await _apiClient.Universe.Names.PostAsync(singleBody, cancellationToken: cancellationToken);
                        if (singleResult != null)
                        {
                            foreach (var entry in singleResult)
                            {
                                if (entry.Id.HasValue && entry.Name != null)
                                    nameCache[(int)entry.Id.Value] = entry.Name;
                            }
                        }
                    }
                    catch
                    {
                        // ID doesn't exist in ESI — fallback will be set later
                    }
                }
            }
        }
    }

    private bool MeetsSkillRequirements(int blueprintTypeId, Dictionary<int, int> characterSkills)
    {
        if (!_skillRequirements.Value.TryGetValue(blueprintTypeId, out var requirements))
            return true; // No requirements listed = allowed

        foreach (var req in requirements)
        {
            if (!characterSkills.TryGetValue(req.SkillId, out var trainedLevel) || trainedLevel < req.Level)
                return false;
        }

        return true;
    }

    private static IReadOnlyDictionary<int, ImmutableArray<SkillRequirement>> LoadSkillRequirements()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "EveMarketAnalysisClient.Data.skill-requirements.json";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
            return new Dictionary<int, ImmutableArray<SkillRequirement>>();

        using var document = JsonDocument.Parse(stream);
        var requirements = document.RootElement.GetProperty("requirements");
        var result = new Dictionary<int, ImmutableArray<SkillRequirement>>();

        foreach (var property in requirements.EnumerateObject())
        {
            var blueprintTypeId = int.Parse(property.Name);
            var skills = property.Value.EnumerateArray()
                .Select(s => new SkillRequirement(
                    s.GetProperty("skillId").GetInt32(),
                    s.GetProperty("level").GetInt32()))
                .ToImmutableArray();
            result[blueprintTypeId] = skills;
        }

        return result;
    }

    private static BlueprintRankingEntry CreateErrorEntry(CharacterBlueprint bp, string errorMessage)
    {
        return new BlueprintRankingEntry(
            Blueprint: bp,
            ProducedTypeName: string.Empty,
            ProducedTypeId: 0,
            PhaseNumber: null,
            MaterialCost: 0,
            ProductRevenue: 0,
            BuyingBrokerFee: 0,
            SellingBrokerFee: 0,
            SalesTax: 0,
            SystemCostFee: 0,
            FacilityTax: 0,
            SccSurcharge: 0,
            GrossProfit: 0,
            ProfitMarginPercent: 0,
            ProductionTimeSeconds: 0,
            IskPerHour: 0,
            AverageDailyVolume: 0,
            IsCurrentPhase: false,
            MeetsThreshold: false,
            HasMarketData: false,
            ErrorMessage: errorMessage);
    }

    private static PortfolioAnalysis CreateEmptyAnalysis(bool portfolioSizeWarning)
    {
        return new PortfolioAnalysis(
            Rankings: ImmutableArray<BlueprintRankingEntry>.Empty,
            PhaseStatuses: ImmutableArray<PhaseStatus>.Empty,
            CurrentPhaseNumber: 1,
            PhaseOverrideActive: false,
            BpoRecommendations: ImmutableArray<BpoPurchaseRecommendation>.Empty,
            ResearchRecommendations: ImmutableArray<ResearchRecommendation>.Empty,
            TotalBlueprintsEvaluated: 0,
            SuccessCount: 0,
            ErrorCount: 0,
            PortfolioSizeWarning: portfolioSizeWarning,
            FetchedAt: DateTimeOffset.UtcNow);
    }

    private record SkillRequirement(int SkillId, int Level);
}
