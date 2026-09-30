# 🏗 Arquitetura do Sistema — Sexta-Feira AI (v1.1.0)

## 📌 Filosofia Arquitetural

O Sexta-Feira segue **Clean Architecture + Domain-Driven Design (DDD)** com desacoplamento rigoroso entre camadas:

```
┌─────────────────────────────────────────────────────────────┐
│                    🖥️ FRONTEND (HUD)                        │
│         React + Three.js/WebGL + SignalR Client             │
│         Esfera Holográfica · Voz Multilíngue · HUD          │
└───────────────────────┬─────────────────────────────────────┘
                        │ REST API + WebSocket (SignalR)
┌───────────────────────▼─────────────────────────────────────┐
│                  📡 WEB API (ASP.NET Core)                   │
│         Controllers · Hubs · Middleware · CORS              │
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
│  Entities         │◄────────────│  LLM Providers (5+)       │
│  Interfaces       │             │  MCP Server Registry (13) │
│  Enums            │             │  PostgreSQL + pgvector     │
│  Value Objects    │             │  Docker Sandbox            │
│                   │             │  Playwright Stealth        │
│                   │             │  Network Scanner           │
│                   │             │  Voice Engine (STT/TTS)    │
│                   │             │  Spotify & Google Connect  │
└───────────────────┘             └─────────────────────────────┘
```

## 🔄 Fluxo de Processamento Cognitivo

1. **Entrada do Usuário:** Voz detectada via Wake-Word Multilíngue (`"Ei, Sexta feira"`, `"Hey Friday"`) ou input de texto via REST API / SignalR.
2. **Contextualização RAG:** `CognitiveOrchestrator` faz busca semântica vetorial em `PgVectorMemoryStore`.
3. **Seleção de LLM & Fallback:** Tenta nuvem (Gemini / OpenAI / Anthropic) com fallback automático para Ollama local (Phi-3).
4. **Tool Calling & MCP Dispatch:** Despacha ferramentas ativas registradas em `McpServerRegistry`.
5. **Auto-Evolução / Execução:** Se o comando solicitar melhoria no código, aciona `SelfEvolutionService` que refatora o código em C# e valida via `dotnet build`.
6. **Síntese de Saída:** Resposta sintetizada por áudio TTS e telemetria enviada via SignalR Hub para o holograma 3D HUD.
