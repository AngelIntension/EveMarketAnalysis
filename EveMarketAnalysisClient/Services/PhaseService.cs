using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using EveMarketAnalysisClient.Models;
using EveMarketAnalysisClient.Services.Interfaces;

namespace EveMarketAnalysisClient.Services;

public class PhaseService : IPhaseService
{
    private readonly IBlueprintDataService _blueprintData;
    private readonly Lazy<ImmutableArray<PhaseDefinition>> _phases;
    private readonly Lazy<Dictionary<int, PhaseDefinition>> _typeIdToPhase;

    public PhaseService(IBlueprintDataService blueprintData)
    {
        _blueprintData = blueprintData;
        _phases = new Lazy<ImmutableArray<PhaseDefinition>>(LoadPhaseDefinitions);
        _typeIdToPhase = new Lazy<Dictionary<int, PhaseDefinition>>(BuildTypeIdMap);
    }

    public ImmutableArray<PhaseDefinition> GetAllPhases() => _phases.Value;

    public PhaseDefinition? GetPhaseForTypeId(int blueprintTypeId)
    {
        _typeIdToPhase.Value.TryGetValue(blueprintTypeId, out var phase);
        return phase;
    }

    public ImmutableArray<int> GetCandidateTypeIdsForPhase(int phaseNumber)
    {
        return _typeIdToPhase.Value
            .Where(kvp => kvp.Value.PhaseNumber == phaseNumber)
            .Select(kvp => kvp.Key)
            .ToImmutableArray();
    }

    private Dictionary<int, PhaseDefinition> BuildTypeIdMap()
    {
        var phases = _phases.Value;
        var allActivities = _blueprintData.GetAllBlueprintActivities();
        var map = new Dictionary<int, PhaseDefinition>();

        foreach (var (bpTypeId, activity) in allActivities)
        {
            var phaseNumber = ClassifyBlueprint(activity);
            var phase = phases.FirstOrDefault(p => p.PhaseNumber == phaseNumber);
            if (phase != null)
                map[bpTypeId] = phase;
        }

        return map;
    }

    /// <summary>
    /// Classifies a blueprint into a phase based on build time thresholds:
    /// Phase 1: under 900s (15 min) — ammo, charges, small items
    /// Phase 2: 900s to 7200s (15 min to 2 hr) — frigates, destroyers, modules
    /// Phase 3: 7200s to 21600s (2 hr to 6 hr) — cruisers, battlecruisers
    /// Phase 4: 21600s to 86400s (6 hr to 24 hr) — battleships, large items
    /// Phase 5: over 86400s (24 hr) — capitals, T2, advanced
    /// </summary>
    private static int ClassifyBlueprint(BlueprintActivity activity)
    {
        return activity.BaseTime switch
        {
            < 900 => 1,
            < 7200 => 2,
            < 21600 => 3,
            < 86400 => 4,
            _ => 5
        };
    }

    private static ImmutableArray<PhaseDefinition> LoadPhaseDefinitions()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "EveMarketAnalysisClient.Data.phases.json";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");

        using var document = JsonDocument.Parse(stream);
        var phasesArray = document.RootElement.GetProperty("phases");

        return phasesArray.EnumerateArray()
            .Select(element => new PhaseDefinition(
                PhaseNumber: element.GetProperty("phaseNumber").GetInt32(),
                Name: element.GetProperty("name").GetString()!,
                Description: element.GetProperty("description").GetString()!,
                CandidateTypeIds: ImmutableArray<int>.Empty))
            .OrderBy(p => p.PhaseNumber)
            .ToImmutableArray();
    }
}
