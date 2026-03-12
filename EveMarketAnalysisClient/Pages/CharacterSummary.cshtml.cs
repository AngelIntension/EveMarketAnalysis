using System.Security.Claims;
using EveMarketAnalysisClient.Models;
using EveMarketAnalysisClient.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EveMarketAnalysisClient.Pages;

[Authorize]
public class CharacterSummaryModel : PageModel
{
    private readonly ICharacterService _characterService;

    public string CharacterName { get; set; } = "Unknown";
    public int? CharacterId { get; set; }
    public string? ErrorMessage { get; set; }

    public CharacterSummaryModel(ICharacterService characterService)
    {
        _characterService = characterService;
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

    public async Task<IActionResult> OnGetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var characterIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var characterName = User.Identity?.Name;

        if (string.IsNullOrEmpty(characterIdClaim) || !int.TryParse(characterIdClaim, out var characterId))
            return new JsonResult(new { error = "Could not determine character identity." }) { StatusCode = 400 };

        try
        {
            var summary = await _characterService.GetCharacterSummaryAsync(
                characterId, characterName ?? "Unknown", cancellationToken);
            return new JsonResult(summary);
        }
        catch (Exception ex)
        {
            return new JsonResult(new { error = $"Failed to load character data: {ex.Message}" }) { StatusCode = 500 };
        }
    }
}
