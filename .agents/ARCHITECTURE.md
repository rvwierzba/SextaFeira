# Arquitetura — Sexta-Feira v3

Documento para agentes (inclusive a própria Sexta-Feira, quando ela se autoprograma). Descreve só o que existe no código.

## Camadas

```
WebApi ──▶ Application ──▶ Domain ◀── Infrastructure
```

- **Domain** (`Agent/AgentContracts.cs`, `Agent/AssistantSettings.cs`): `AgentMessage`, `ToolCall`, `ToolSpec`, `ChatRequest`, eventos de streaming (`TextDelta`, `ToolCallsRequested`, `EngineStateCaptured`, `ChatCompleted`), e as interfaces `IChatEngine`, `ITool`, `IToolProvider`, `IMemoryStore`, `ISpeechToText`, `ITextToSpeech`, `ISettingsStore`.
- **Application**
  - `ConversationAgent`: um turno = até 6 rodadas LLM → ferramentas → LLM. Emite `AgentSentence` assim que cada frase fica pronta (para a fala começar cedo). Fallback para o modelo local se a nuvem falhar antes de falar algo. Rodada vazia → tenta de novo sem ferramentas.
  - `SystemPromptBuilder`: prompt **estático** (persona + regras, cacheável) + **nota de contexto** (hora, dispositivo, memórias) anexada só à última fala do usuário.
  - `SentenceSplitter`, `SpeechText` (remove markdown), `WakeWord` (fonética + tolerância a 1 letra só no nome completo).
  - `ToolRegistry` (embutidas + MCP; `code_*` somem se `SelfCode.Enabled=false`), `EngineRouter`.
- **Infrastructure**
  - `Engines/OllamaEngine` (`/api/chat` NDJSON + tools; recupera tool call escrita como texto JSON; embeddings na CPU), `OpenAiCompatibleEngine` (`OpenAiEngine`, `GeminiEngine`), `AnthropicEngine` (SDK oficial `Anthropic`, beta messages, streaming, blocos de raciocínio guardados em `EngineState`).
  - `Mcp/McpManager`: lê `data/mcp.json`, conecta stdio/HTTP (SDK `ModelContextProtocol`), OAuth com callback em `http://localhost:7891/callback` e tokens em `data/mcp-tokens`.
  - `Voice/WhisperSpeechToText` (Whisper.net, Vulkan → CPU, contexto de áudio por faixa de duração), `Voice/DeviceTextToSpeech` (o aparelho fala).
  - `Storage/JsonSettingsStore`, `Storage/FileMemoryStore` (JSON + cosseno sobre embeddings do Ollama).
  - `Tools/BuiltinTools`, `Tools/CodeTools` (presos à raiz do repositório; `data/` e `.git` protegidos; build com `--artifacts-path` para não colidir com o processo em execução; `restart_myself` sai com código 42 e o `start.ps1` reinicia).
- **WebApi**
  - `Realtime/AssistantHub` + `AssistantSessionService`: dispositivos, fala → STT → palavra de ativação / janela de continuação → turno; uma conversa compartilhada; nova fala cancela o turno atual (barge-in); filtro de eco comparando com o que está sendo falado.
  - `Controllers/SystemController`: `/api/status`, `/api/settings` (chaves mascaradas), `/api/mcp/config`, `/api/memory`.
  - `Hosting/LanCertificate` (HTTPS autoassinado com os IPs da rede), `EngineWarmupService` (carrega o LLM depois do Whisper).

## Protocolo SignalR (`/hubs/assistant`)

Cliente → servidor: `Register(deviceId, name, kind)`, `SetMicState(bool)`, `SendUtterance(base64 PCM16 16 kHz)`, `SendText(text)`, `Interrupt()`, `PlaybackFinished()`, `ClearConversation()`.

Servidor → clientes: `state`, `transcript`, `assistantDelta`, `assistantAudio {turnId, seq, text, audio?, targetDeviceId}`, `tool`, `assistantDone`, `stopAudio`, `notice`, `devices`, `conversationCleared`.

## HUD (`src/SextaFeira.Web`)

- `lib/micCapture.ts`: AudioWorklet → 16 kHz, VAD com piso de ruído adaptativo, pré-roll de 300 ms, fim após 800 ms de silêncio; limiar mais alto enquanto a assistente fala.
- `lib/speaker.ts`: fila ordenada de frases; MP3 do servidor se houver, senão `speechSynthesis` com a voz preferida.
- `hooks/useAssistant.ts`: conexão, estados, barge-in. `components/hud/SettingsPanel.tsx`: tudo configurável.

## Como estender

- **Nova ferramenta:** adicione um `FunctionTool` em `BuiltinTools.Create` (nome, descrição curta, JSON Schema, handler). Descrições curtas ajudam modelos locais pequenos.
- **Nova engine:** implemente `IChatEngine` e registre em `Program.cs`; adicione a configuração em `AssistantSettings`.
- **TTS no servidor:** implemente `ITextToSpeech` retornando MP3 e troque o registro de `DeviceTextToSpeech`.
