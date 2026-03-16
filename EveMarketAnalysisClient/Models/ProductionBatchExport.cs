using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace EveMarketAnalysisClient.Models;

public record ProductionBatchExport(
    [property: JsonPropertyName("items")] ImmutableArray<BlueprintRunExport> Items)
{
    public static ImmutableArray<BlueprintRunExport> MergeSelections(
        ImmutableArray<BlueprintRunExport> existing,
        ImmutableArray<BlueprintRunExport> incoming)
    {
        var merged = existing.ToDictionary(e => e.TypeId);
        foreach (var item in incoming)
            merged[item.TypeId] = item;
        return merged.Values.ToImmutableArray();
    }
}

public record BlueprintRunExport(
    [property: JsonPropertyName("typeId")] int TypeId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("runs")] int Runs)
{
    public static int ClampRuns(int runs) => Math.Max(1, runs);
}
