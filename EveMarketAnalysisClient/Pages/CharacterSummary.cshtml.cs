using System.Security.Claims;
using EveMarketAnalysisClient.Models;
using EveMarketAnalysisClient.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EveMarketAnalysisClient.Pages;

[Authorize]
public class CharacterSummaryModel : PageModel
{
    private readonly ICharacterService _characterService;

    public CharacterSummary? Summary { get; set; }
    public string? ErrorMessage { get; set; }

    public CharacterSummaryModel(ICharacterService characterService)
    {
        _characterService = characterService;
    }

    public async Task OnGetAsync(CancellationToken cancellationToken = default)
    {
        var characterIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var characterName = User.Identity?.Name;

        if (string.IsNullOrEmpty(characterIdClaim) || !int.TryParse(characterIdClaim, out var characterId))
        {
            ErrorMessage = "Could not determine character identity. Please log in again.";
            return;
        }

        try
        {
            Summary = await _characterService.GetCharacterSummaryAsync(
                characterId, characterName ?? "Unknown", cancellationToken);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load character data: {ex.Message}";
        }
    }
}
