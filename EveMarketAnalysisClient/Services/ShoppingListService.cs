using System.Collections.Frozen;
using System.Collections.Immutable;
using EveMarketAnalysisClient.Models;
using EveMarketAnalysisClient.Services.Interfaces;
using EveStableInfrastructure;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace EveMarketAnalysisClient.Services;

public class ShoppingListService : IShoppingListService
{
    private readonly IBlueprintDataService _blueprintData;
    private readonly IEsiCharacterClient _esiClient;
    private readonly IEsiMarketClient _marketClient;
    private readonly ApiClient _apiClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ShoppingListService> _logger;
    private static readonly SemaphoreSlim ConcurrencyLimiter = new(20, 20);
    private static readonly TimeSpan NameCacheDuration = TimeSpan.FromHours(24);

    public ShoppingListService(
        IBlueprintDataService blueprintData,
        IEsiCharacterClient esiClient,
        IEsiMarketClient marketClient,
        ApiClient apiClient,
        IMemoryCache cache,
        ILogger<ShoppingListService> logger)
    {
        _blueprintData = blueprintData;
        _esiClient = esiClient;
        _marketClient = marketClient;
        _apiClient = apiClient;
        _cache = cache;
        _logger = logger;
    }

    public FrozenDictionary<int, CharacterBlueprint> BuildOwnedBlueprintMap(
        ImmutableArray<CharacterBlueprint> blueprints)
    {
        var map = new Dictionary<int, CharacterBlueprint>();

        foreach (var bp in blueprints)
        {
            var activity = _blueprintData.GetBlueprintActivity(bp.TypeId);
            if (activity == null) continue;

            var producedTypeId = activity.ProducedTypeId;
            if (!map.TryGetValue(producedTypeId, out var existing) ||
                bp.MaterialEfficiency > existing.MaterialEfficiency)
            {
                map[producedTypeId] = bp;
            }
        }

        return map.ToFrozenDictionary();
    }

    public MaterialTreeNode ExpandBlueprintToMaterials(
        BlueprintSelection selection,
        FrozenDictionary<int, CharacterBlueprint> ownedMap,
        ImmutableHashSet<int>? visited = null)
    {
        visited ??= ImmutableHashSet<int>.Empty;

        var activity = _blueprintData.GetBlueprintActivity(selection.BlueprintTypeId);
        if (activity == null)
        {
            return new MaterialTreeNode(
                TypeId: selection.ProducedTypeId,
                TypeName: selection.BlueprintName,
                BaseQuantity: 0,
                AdjustedQuantity: 0,
                Runs: selection.Runs,
                TotalQuantity: 0,
                IsExpanded: false,
                SourceBlueprintTypeId: selection.BlueprintTypeId,
                Children: ImmutableArray<MaterialTreeNode>.Empty);
        }

        // Prevent cycles by tracking visited producedTypeIds
        var newVisited = visited.Add(selection.ProducedTypeId);

        var children = activity.Materials.Select(mat =>
        {
            var adjustedQty = Math.Max(1, (int)Math.Ceiling(mat.BaseQuantity * (1.0 - selection.MaterialEfficiency / 100.0)));
            var totalQty = (long)adjustedQty * selection.Runs;

            // Recursive expansion when ProduceComponents is true
            if (selection.ProduceComponents &&
                ownedMap.TryGetValue(mat.TypeId, out var ownedBp) &&
                !newVisited.Contains(mat.TypeId))
            {
                var subActivity = _blueprintData.GetBlueprintActivity(ownedBp.TypeId);
                if (subActivity != null)
                {
                    // Calculate how many runs we need to produce the required quantity
                    var runsNeeded = (int)Math.Ceiling((double)totalQty / subActivity.ProducedQuantity);

                    var subSelection = new BlueprintSelection(
                        BlueprintTypeId: ownedBp.TypeId,
                        BlueprintName: ownedBp.TypeName,
                        MaterialEfficiency: ownedBp.MaterialEfficiency,
                        TimeEfficiency: ownedBp.TimeEfficiency,
                        Runs: runsNeeded,
                        MaxRuns: ownedBp.Runs,
                        IsCopy: ownedBp.IsCopy,
                        ProduceComponents: true,
                        ProducedTypeId: mat.TypeId);

                    var expandedNode = ExpandBlueprintToMaterials(subSelection, ownedMap, newVisited);

                    return new MaterialTreeNode(
                        TypeId: mat.TypeId,
                        TypeName: string.Empty,
                        BaseQuantity: mat.BaseQuantity,
                        AdjustedQuantity: adjustedQty,
                        Runs: selection.Runs,
                        TotalQuantity: totalQty,
                        IsExpanded: true,
                        SourceBlueprintTypeId: selection.BlueprintTypeId,
                        Children: expandedNode.Children.IsEmpty
                            ? ImmutableArray.Create(expandedNode)
                            : expandedNode.Children);
                }
            }

            return new MaterialTreeNode(
                TypeId: mat.TypeId,
                TypeName: string.Empty,
                BaseQuantity: mat.BaseQuantity,
                AdjustedQuantity: adjustedQty,
                Runs: selection.Runs,
                TotalQuantity: totalQty,
                IsExpanded: false,
                SourceBlueprintTypeId: selection.BlueprintTypeId,
                Children: ImmutableArray<MaterialTreeNode>.Empty);
        }).ToImmutableArray();

        return new MaterialTreeNode(
            TypeId: selection.ProducedTypeId,
            TypeName: selection.BlueprintName,
            BaseQuantity: 0,
            AdjustedQuantity: 0,
            Runs: selection.Runs,
            TotalQuantity: 0,
            IsExpanded: true,
            SourceBlueprintTypeId: selection.BlueprintTypeId,
            Children: children);
    }

    public ImmutableArray<ShoppingListItem> AggregateMaterials(
        ImmutableArray<MaterialTreeNode> trees)
    {
        var grouped = new Dictionary<int, (string TypeName, long TotalQuantity, List<MaterialSource> Sources)>();

        foreach (var tree in trees)
        {
            CollectLeafNodes(tree, tree.SourceBlueprintTypeId, string.Empty, grouped);
        }

        return grouped.Select(kvp => new ShoppingListItem(
            TypeId: kvp.Key,
            TypeName: kvp.Value.TypeName,
            Category: string.Empty,
            TotalQuantity: kvp.Value.TotalQuantity,
            Volume: 0.0,
            TotalVolume: 0.0,
            EstimatedUnitCost: null,
            EstimatedTotalCost: null,
            Sources: kvp.Value.Sources.ToImmutableArray()
        )).ToImmutableArray();
    }

    private static void CollectLeafNodes(
        MaterialTreeNode node,
        int rootBlueprintTypeId,
        string rootBlueprintName,
        Dictionary<int, (string TypeName, long TotalQuantity, List<MaterialSource> Sources)> grouped)
    {
        if (node.Children.IsEmpty && node.TotalQuantity > 0)
        {
            // This is a leaf node (raw material to purchase)
            if (grouped.TryGetValue(node.TypeId, out var existing))
            {
                grouped[node.TypeId] = (
                    existing.TypeName,
                    existing.TotalQuantity + node.TotalQuantity,
                    existing.Sources);
                existing.Sources.Add(new MaterialSource(
                    rootBlueprintName,
                    rootBlueprintTypeId,
                    node.TotalQuantity));
            }
            else
            {
                var sources = new List<MaterialSource>
                {
                    new(rootBlueprintName, rootBlueprintTypeId, node.TotalQuantity)
                };
                grouped[node.TypeId] = (node.TypeName, node.TotalQuantity, sources);
            }
        }
        else if (!node.Children.IsEmpty)
        {
            // For expanded nodes, only collect from children (leaf nodes)
            foreach (var child in node.Children)
            {
                if (child.IsExpanded)
                {
                    // Recurse into expanded children
                    CollectLeafNodes(child, rootBlueprintTypeId, rootBlueprintName, grouped);
                }
                else if (child.TotalQuantity > 0)
                {
                    // Leaf child
                    if (grouped.TryGetValue(child.TypeId, out var existing))
                    {
                        grouped[child.TypeId] = (
                            existing.TypeName,
                            existing.TotalQuantity + child.TotalQuantity,
                            existing.Sources);
                        existing.Sources.Add(new MaterialSource(
                            rootBlueprintName,
                            rootBlueprintTypeId,
                            child.TotalQuantity));
                    }
                    else
                    {
                        var sources = new List<MaterialSource>
                        {
                            new(rootBlueprintName, rootBlueprintTypeId, child.TotalQuantity)
                        };
                        grouped[child.TypeId] = (child.TypeName, child.TotalQuantity, sources);
                    }
                }
            }
        }
    }

    public async Task<ShoppingListResponse> GenerateShoppingListAsync(
        ImmutableArray<BlueprintSelection> selections,
        int characterId,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        // Fetch character's blueprints
        var characterBlueprints = await _esiClient.GetCharacterBlueprintsAsync(characterId, cancellationToken);

        // Build owned blueprint map
        var ownedMap = BuildOwnedBlueprintMap(characterBlueprints);

        // Expand each selection
        var trees = new List<MaterialTreeNode>();
        foreach (var selection in selections)
        {
            try
            {
                var tree = ExpandBlueprintToMaterials(selection, ownedMap);
                trees.Add(tree);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to expand blueprint {TypeId}", selection.BlueprintTypeId);
                errors.Add($"Failed to expand {selection.BlueprintName}: {ex.Message}");
            }
        }

        // Aggregate materials
        var items = AggregateMaterials(trees.ToImmutableArray());

        // Resolve type names via bulk POST /universe/names
        var typeIds = items.Select(i => i.TypeId).Distinct().ToHashSet();
        Dictionary<int, string> typeNames;
        try
        {
            typeNames = await ResolveTypeNamesAsync(typeIds, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve type names");
            typeNames = new Dictionary<int, string>();
        }

        // Update items with resolved names
        items = items.Select(item =>
        {
            var name = typeNames.TryGetValue(item.TypeId, out var n) ? n : $"Type {item.TypeId}";
            return item with { TypeName = name };
        }).ToImmutableArray();

        return new ShoppingListResponse(
            Items: items,
            TotalEstimatedCost: null,
            TotalVolume: 0.0,
            BlueprintCount: selections.Length,
            GeneratedAt: DateTimeOffset.UtcNow,
            Errors: errors.ToImmutableArray());
    }

    public async Task<FrozenDictionary<int, decimal?>> FetchCostsAsync(
        ImmutableArray<int> typeIds,
        int regionId,
        CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<int, decimal?>();

        var tasks = typeIds.Select(async typeId =>
        {
            await ConcurrencyLimiter.WaitAsync(cancellationToken);
            try
            {
                var snapshot = await _marketClient.GetMarketSnapshotAsync(regionId, typeId, cancellationToken);
                return (typeId, price: snapshot.LowestSellPrice);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch cost for type {TypeId}", typeId);
                return (typeId, price: (decimal?)null);
            }
            finally
            {
                ConcurrencyLimiter.Release();
            }
        });

        var snapshots = await Task.WhenAll(tasks);
        foreach (var (typeId, price) in snapshots)
            results[typeId] = price;

        return results.ToFrozenDictionary();
    }

    public async Task<FrozenDictionary<int, double>> FetchVolumesAsync(
        ImmutableArray<int> typeIds,
        CancellationToken cancellationToken = default)
    {
        const string cacheKey = "esi:typevolumes";
        var volumeCache = _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = NameCacheDuration;
            return new Dictionary<int, double>();
        })!;

        var idsToFetch = typeIds.Where(id => !volumeCache.ContainsKey(id)).ToList();

        if (idsToFetch.Count > 0)
        {
            var volumeTasks = idsToFetch.Select(async typeId =>
            {
                await ConcurrencyLimiter.WaitAsync(cancellationToken);
                try
                {
                    var typeInfo = await _apiClient.Universe.Types[typeId].GetAsync(cancellationToken: cancellationToken);
                    return (typeId, volume: typeInfo?.Volume ?? 0.0);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch volume for type {TypeId}", typeId);
                    return (typeId, volume: 0.0);
                }
                finally
                {
                    ConcurrencyLimiter.Release();
                }
            });

            var results = await Task.WhenAll(volumeTasks);
            foreach (var (typeId, volume) in results)
                volumeCache.TryAdd(typeId, volume);
        }

        return typeIds.ToDictionary(id => id, id => volumeCache.GetValueOrDefault(id)).ToFrozenDictionary();
    }

    public string GenerateCsv(ImmutableArray<ShoppingListItem> items)
    {
        var hasCosts = items.Any(i => i.EstimatedUnitCost.HasValue);
        var sb = new System.Text.StringBuilder();

        // Header
        sb.Append("Material Name,Quantity,Category");
        if (hasCosts)
            sb.Append(",Estimated Unit Cost,Estimated Total Cost,Volume (m³),Total Volume (m³)");
        sb.AppendLine();

        foreach (var item in items)
        {
            sb.Append(CsvEscape(item.TypeName));
            sb.Append(',');
            sb.Append(item.TotalQuantity);
            sb.Append(',');
            sb.Append(CsvEscape(item.Category));
            if (hasCosts)
            {
                sb.Append(',');
                sb.Append(item.EstimatedUnitCost?.ToString("F2") ?? "N/A");
                sb.Append(',');
                sb.Append(item.EstimatedTotalCost?.ToString("F2") ?? "N/A");
                sb.Append(',');
                sb.Append(item.Volume.ToString("F2"));
                sb.Append(',');
                sb.Append(item.TotalVolume.ToString("F2"));
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    public string GenerateClipboardText(ImmutableArray<ShoppingListItem> items)
    {
        var hasCosts = items.Any(i => i.EstimatedUnitCost.HasValue);
        var sb = new System.Text.StringBuilder();

        // Header
        sb.Append("Material Name\tQuantity\tCategory");
        if (hasCosts)
            sb.Append("\tEstimated Unit Cost\tEstimated Total Cost\tVolume (m³)\tTotal Volume (m³)");
        sb.AppendLine();

        foreach (var item in items)
        {
            sb.Append(item.TypeName);
            sb.Append('\t');
            sb.Append(item.TotalQuantity);
            sb.Append('\t');
            sb.Append(item.Category);
            if (hasCosts)
            {
                sb.Append('\t');
                sb.Append(item.EstimatedUnitCost?.ToString("F2") ?? "N/A");
                sb.Append('\t');
                sb.Append(item.EstimatedTotalCost?.ToString("F2") ?? "N/A");
                sb.Append('\t');
                sb.Append(item.Volume.ToString("F2"));
                sb.Append('\t');
                sb.Append(item.TotalVolume.ToString("F2"));
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string CsvEscape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
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
}
