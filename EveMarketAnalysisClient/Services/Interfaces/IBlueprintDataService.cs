using EveMarketAnalysisClient.Models;

namespace EveMarketAnalysisClient.Services.Interfaces;

public interface IBlueprintDataService
{
    BlueprintActivity? GetBlueprintActivity(int blueprintTypeId);
    IReadOnlyDictionary<int, BlueprintActivity> GetAllBlueprintActivities();
}
