# 🏗 Arquitetura do Sistema — Sexta-Feira AI (v1.2.0)

## 📌 Filosofia Arquitetural

O Sexta-Feira segue **Clean Architecture + Domain-Driven Design (DDD)** com desacoplamento rigoroso entre camadas:

```
┌─────────────────────────────────────────────────────────────┐
│                    🖥️ FRONTEND (HUD)                        │
│         React + Three.js/WebGL + SignalR Client             │
│         Esfera Holográfica · Voz Multilíngue · HUD          │
│         Modal Holográfico de Conexão de Contas de IA        │
└───────────────────────┬─────────────────────────────────────┘
                        │ REST API + WebSocket (SignalR)
┌───────────────────────▼─────────────────────────────────────┐
│                  📡 WEB API (ASP.NET Core)                   │
│         Controllers (AiConnections, Core, Sandbox, etc)     │
│         Hubs (HudTelemetry, VoiceStream) · CORS             │
└───────────────────────┬─────────────────────────────────────┘
                        │
┌───────────────────────▼─────────────────────────────────────┐
│               🧠 APPLICATION (Orchestrators)                 │
│   CognitiveOrchestrator · SelfHealingEngine                 │
│   SelfEvolutionService · MemoryRecyclerService              │
└───────┬───────────────────────────────────┬─────────────────┘
        │                                   │
┌───────▼───────────┐             ┌─────────▼─────────────────┐
│  📐 DOMAIN        │             │  🔌 INFRASTRUCTURE         │
│  Entities         │◄────────────│  AiConnectionManager       │
│  AiAccountConn    │             │  GoogleGeminiCloudProvider │
│  Interfaces       │             │  OpenAiGptCloudProvider    │
│  Enums            │             │  AnthropicClaudeCloudProv. │
│  Value Objects    │             │  MCP Server Registry (13) │
│                   │             │  PostgreSQL + pgvector     │
│                   │             │  Docker Sandbox            │
│                   │             │  Playwright Stealth        │
│                   │             │  Network Scanner           │
│                   │             │  Voice Engine (STT/TTS)    │
│                   │             │  Spotify & Google Connect  │
└───────────────────┘             └─────────────────────────────┘
```

## 🔄 Fluxo de Processamento Cognitivo

1. **Entrada do Usuário:** Voz detectada via Wake-Word Multilíngue (`"Ei, Sexta feira"`, `"Hey Friday"`) ou input de texto via REST API / SignalR.
2. **Contextualização RAG:** `CognitiveOrchestrator` faz busca semântica vetorial em `PgVectorMemoryStore`.
3. **Seleção de LLM & Nuvem Inteligente:** Utiliza o provedor de IA selecionado pelo usuário (**Google Gemini 2.0 Flash via OAuth 1-clique**, **OpenAI GPT-4o** ou **Anthropic Claude 3.5 Sonnet**) via `IAiConnectionManager`. Suporta alternância dinâmica tanto pela interface gráfica quanto por comando de voz direto (*"Sexta-Feira, use o Claude"*).
4. **Tool Calling & MCP Dispatch:** Despacha ferramentas ativas registradas em `McpServerRegistry`.
5. **Auto-Evolução / Execução:** Se o comando solicitar melhoria no código, aciona `SelfEvolutionService` que refatora o código em C# e valida via `dotnet build`.
6. **Síntese de Saída:** Resposta sintetizada por áudio TTS e telemetria enviada via SignalR Hub para o holograma 3D HUD.
