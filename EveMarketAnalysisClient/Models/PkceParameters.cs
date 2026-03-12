namespace EveMarketAnalysisClient.Models;

public record PkceParameters(
    string CodeVerifier,
    string CodeChallenge,
    string State);
