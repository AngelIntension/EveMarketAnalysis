namespace EveMarketAnalysisClient.Middleware;

public class EsiRateLimitHandler : DelegatingHandler
{
    private const int ErrorLimitThreshold = 10;
    private volatile int _lastErrorLimitRemain = 100;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_lastErrorLimitRemain < ErrorLimitThreshold)
        {
            // Back off briefly when approaching error limit
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (response.Headers.TryGetValues("X-ESI-Error-Limit-Remain", out var remainValues))
        {
            if (int.TryParse(remainValues.FirstOrDefault(), out var remain))
                _lastErrorLimitRemain = remain;
        }

        return response;
    }
}
