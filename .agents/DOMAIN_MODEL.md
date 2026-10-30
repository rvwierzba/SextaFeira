# 📐 Modelo de Domínio — Sexta-Feira

## 1. Entidades Principais (`SextaFeira.Domain.Entities`)
- `AiAccountConnection`: Gerenciamento de vínculo e estado de contas de provedores de IA (Google Gemini, OpenAI, Anthropic) com latência, tipo de autenticação (OAuth/Direct) e status ativo.
- `ChatMessage`: Mensagens da sessão com papel (user, assistant, tool), timestamp e suporte a áudio Base64.
- `MemoryVector`: Vetor de memória vetorial pgvector com pontuação de importância e flag de reciclagem.
- `SpotifyTrackItem`: Representação de música, artista, álbum e status de reprodução do Spotify.
- `SelfEvolutionResult`: Resultado de operações de auto-modificação de código com logs de `dotnet build`.
- `CodeExecutionJob`: Trabalho de execução isolada em container Docker Sandbox.

## 2. Interfaces de Domínio (`SextaFeira.Domain.Interfaces`)
- `IAiConnectionManager`: Gerenciamento unificado de conexões de IA, teste de latência, troca a quente de provedor e cofre seguro.
- `ILLMProvider`: Interface desacoplada para provedores cognitivos (Google Gemini, OpenAI GPT, Anthropic Claude, Ollama local).
- `IMemoryStore`: Interface para busca semântica vetorial e reciclagem de memória pgvector.
- `ISandboxService`: Execução segura de scripts em Docker.
- `IVoiceEngine`: Síntese (TTS) e transcrição (STT) de voz.

## 3. Enums (`SextaFeira.Domain.Enums`)
- `AgentState`: Idle, Listening, Thinking, Speaking, Executing.
- `ToolCategory`: System, Network, StealthBrowser, Sandbox, Calendar, Email, Files, Geolocation, Spotify, GoogleServices, CodeEvolution.
- `LLMProviderType`: OllamaLocal, Phi3LocalOnnx, OpenAI, Anthropic, GoogleGemini, Groq.
