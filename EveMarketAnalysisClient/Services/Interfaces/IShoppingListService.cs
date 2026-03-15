using System.Collections.Frozen;
using System.Collections.Immutable;
using EveMarketAnalysisClient.Models;

namespace EveMarketAnalysisClient.Services.Interfaces;

public interface IShoppingListService
{
    FrozenDictionary<int, CharacterBlueprint> BuildOwnedBlueprintMap(
        ImmutableArray<CharacterBlueprint> blueprints);

    MaterialTreeNode ExpandBlueprintToMaterials(
        BlueprintSelection selection,
        FrozenDictionary<int, CharacterBlueprint> ownedMap,
        ImmutableHashSet<int>? visited = null);

    ImmutableArray<ShoppingListItem> AggregateMaterials(
        ImmutableArray<MaterialTreeNode> trees);

    Task<ShoppingListResponse> GenerateShoppingListAsync(
        ImmutableArray<BlueprintSelection> selections,
        int characterId,
        CancellationToken cancellationToken = default);

    Task<FrozenDictionary<int, decimal?>> FetchCostsAsync(
        ImmutableArray<int> typeIds,
        int regionId,
        CancellationToken cancellationToken = default);

    Task<FrozenDictionary<int, double>> FetchVolumesAsync(
        ImmutableArray<int> typeIds,
        CancellationToken cancellationToken = default);

    string GenerateCsv(ImmutableArray<ShoppingListItem> items);

    string GenerateClipboardText(ImmutableArray<ShoppingListItem> items);
}
