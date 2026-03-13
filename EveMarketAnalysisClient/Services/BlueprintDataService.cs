using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using EveMarketAnalysisClient.Models;
using EveMarketAnalysisClient.Services.Interfaces;

namespace EveMarketAnalysisClient.Services;

public class BlueprintDataService : IBlueprintDataService
{
    private readonly Lazy<IReadOnlyDictionary<int, BlueprintActivity>> _activities;

    public BlueprintDataService()
    {
        _activities = new Lazy<IReadOnlyDictionary<int, BlueprintActivity>>(LoadActivities);
    }

    public BlueprintActivity? GetBlueprintActivity(int blueprintTypeId)
    {
        _activities.Value.TryGetValue(blueprintTypeId, out var activity);
        return activity;
    }

    public IReadOnlyDictionary<int, BlueprintActivity> GetAllBlueprintActivities()
    {
        return _activities.Value;
    }

    private static IReadOnlyDictionary<int, BlueprintActivity> LoadActivities()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "EveMarketAnalysisClient.Data.blueprints.json";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");

        using var document = JsonDocument.Parse(stream);
        var result = new Dictionary<int, BlueprintActivity>();

        foreach (var property in document.RootElement.EnumerateObject())
        {
            var blueprintTypeId = int.Parse(property.Name);
            var element = property.Value;

            var materials = element.GetProperty("materials")
                .EnumerateArray()
                .Select(m => new MaterialRequirement(
                    TypeId: m.GetProperty("typeId").GetInt32(),
                    TypeName: string.Empty,
                    BaseQuantity: m.GetProperty("quantity").GetInt32(),
                    AdjustedQuantity: 0))
                .ToImmutableArray();

            result[blueprintTypeId] = new BlueprintActivity(
                BlueprintTypeId: blueprintTypeId,
                ProducedTypeId: element.GetProperty("producedTypeId").GetInt32(),
                ProducedQuantity: element.GetProperty("producedQuantity").GetInt32(),
                BaseTime: element.GetProperty("time").GetInt32(),
                Materials: materials);
        }

        return result;
    }
}
