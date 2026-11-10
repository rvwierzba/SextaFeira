namespace SextaFeira.Infrastructure.Tools;

/// <summary>
/// Repete requisições GET/HEAD que falham por problema de rede transitório (handshake TLS interrompido,
/// conexão recusada, 502/503/504). APIs abertas como a Open-Meteo têm essas oscilações.
/// </summary>
public sealed class TransientRetryHandler : DelegatingHandler
{
    private const int MaxAttempts = 3;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var idempotent = request.Method == HttpMethod.Get || request.Method == HttpMethod.Head;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var response = await base.SendAsync(request, ct);
                if (!idempotent || attempt >= MaxAttempts || (int)response.StatusCode is not (502 or 503 or 504))
                    return response;
                response.Dispose();
            }
            catch (HttpRequestException) when (idempotent && attempt < MaxAttempts && !ct.IsCancellationRequested)
            {
                // tenta de novo abaixo
            }
            await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt), ct);
        }
    }
}
