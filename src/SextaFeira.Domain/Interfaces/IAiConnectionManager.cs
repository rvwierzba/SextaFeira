using SextaFeira.Domain.Entities;
using SextaFeira.Domain.Enums;

namespace SextaFeira.Domain.Interfaces;

public interface IAiConnectionManager
{
    Task<List<AiAccountConnection>> GetAllConnectionsAsync(CancellationToken cancellationToken = default);
    Task<AiAccountConnection> ConnectAsync(
        LLMProviderType provider, 
        string credentialOrToken, 
        string? accountName = null, 
        string? modelName = null, 
        string? authType = null,
        CancellationToken cancellationToken = default);
    Task<bool> DisconnectAsync(LLMProviderType provider, CancellationToken cancellationToken = default);
    Task<AiAccountConnection> SetActiveProviderAsync(LLMProviderType provider, CancellationToken cancellationToken = default);
    Task<LLMProviderType> GetActiveProviderTypeAsync(CancellationToken cancellationToken = default);
    Task<string?> GetCredentialAsync(LLMProviderType provider, CancellationToken cancellationToken = default);
    Task<int> TestConnectionLatencyAsync(LLMProviderType provider, CancellationToken cancellationToken = default);
}

