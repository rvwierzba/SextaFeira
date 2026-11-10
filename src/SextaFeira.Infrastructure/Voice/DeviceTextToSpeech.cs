using SextaFeira.Domain.Agent;

namespace SextaFeira.Infrastructure.Voice;

/// <summary>
/// TTS padrão: cada dispositivo fala com a voz neural do próprio sistema/navegador
/// (Edge: "Francisca Online (Natural)", Chrome: "Google português do Brasil", iPhone: "Luciana").
/// O servidor só manda o texto, frase a frase. Para usar um TTS no servidor (ex.: um modelo local),
/// implemente <see cref="ITextToSpeech"/> retornando MP3 e registre no Program.cs.
/// </summary>
public sealed class DeviceTextToSpeech : ITextToSpeech
{
    public string Status => "voz do próprio dispositivo";
    public Task<byte[]> SynthesizeAsync(string text, CancellationToken ct = default) => Task.FromResult(Array.Empty<byte>());
}
