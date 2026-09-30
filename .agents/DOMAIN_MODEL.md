# 📐 Modelo de Domínio — Sexta-Feira

## 1. Entidades Principais (`SextaFeira.Domain.Entities`)
- `ChatMessage`: Mensagens da sessão com papel (user, assistant, tool), timestamp e suporte a áudio Base64.
- `MemoryVector`: Vetor de memória vetorial pgvector com pontuação de importância e flag de reciclagem.
- `SpotifyTrackItem`: Representação de música, artista, álbum e status de reprodução do Spotify.
- `SelfEvolutionResult`: Resultado de operações de auto-modificação de código com logs de `dotnet build`.
- `CodeExecutionJob`: Trabalho de execução isolada em container Docker Sandbox.

## 2. Enums (`SextaFeira.Domain.Enums`)
- `AgentState`: Idle, Listening, Thinking, Speaking, Executing.
- `ToolCategory`: System, Network, StealthBrowser, Sandbox, Calendar, Email, Files, Geolocation, Spotify, GoogleServices, CodeEvolution.
- `LLMProviderType`: OllamaLocal, Phi3LocalOnnx, OpenAI, Anthropic, GoogleGemini, Groq.
