using System.Text.Json;
using Microsoft.Extensions.Logging;
using SextaFeira.Domain.Enums;
using SextaFeira.Domain.Interfaces;

namespace SextaFeira.Infrastructure.Voice;

public class VoiceEngineService : IVoiceEngine
{
    private readonly ILogger<VoiceEngineService> _logger;

    public VoiceEngineService(ILogger<VoiceEngineService> logger)
    {
        _logger = logger;
    }

    public Task<string> TranscribeAudioAsync(byte[] audioData, string language = "pt-BR", CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processando áudio ({Size} bytes) via pipeline de STT...", audioData.Length);
        // Em produção aqui conecta com Whisper ONNX local ou serviço STT
        return Task.FromResult("Comando de voz recebido: 'Sexta-Feira, apresentar status do sistema'");
    }

    public Task<byte[]> SynthesizeSpeechAsync(string text, string voice = "pt-BR-FranciscaNeural", CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Sintetizando fala para: {Text}", text.Length > 60 ? text.Substring(0, 60) + "..." : text);
        
        // Em produção gera áudio PCM/WAV real ou delega para Web Speech API no client
        // Retornamos um cabeçalho WAV simples de 44 bytes para streaming
        var wavHeader = new byte[]
        {
            0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00,
            0x57, 0x41, 0x56, 0x45, 0x66, 0x6D, 0x74, 0x20,
            0x10, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00,
            0x44, 0xAC, 0x00, 0x00, 0x88, 0x58, 0x01, 0x00,
            0x02, 0x00, 0x10, 0x00, 0x64, 0x61, 0x74, 0x61,
            0x00, 0x00, 0x00, 0x00
        };

        return Task.FromResult(wavHeader);
    }
}
