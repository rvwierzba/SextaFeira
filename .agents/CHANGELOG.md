# 📜 CHANGELOG — Sexta-Feira

## [v1.2.0] - 2026-10-30

### 🚀 Novas Funcionalidades
- **Hub Multi-Provedor de IA em Nuvem (Zero-Setup & Intuitivo):**
  - Descontinuação do modelo local lento como default; migração definitiva para provedores cloud de ultra-baixa latência: **Google Gemini 2.0 Flash**, **OpenAI GPT-4o** e **Anthropic Claude 3.5 Sonnet**.
  - Criação de `IAiConnectionManager` e `AiConnectionManager` com cofre persistente de credenciais (`ai_connections_vault.json`).
  - Implementação oficial de `OpenAiGptCloudProvider` e `AnthropicClaudeCloudProvider` com suporte a Function Calling (MCP) e streaming.
  - Atualização do `GoogleGeminiCloudProvider` para ler tokens do cofre e suportar autenticação OAuth.
  - Adicionado `AiConnectionsController` com rotas para listagem, login Google OAuth 1-clique, smart connect, troca de provedor e teste de ping em tempo real.
- **Troca de Cérebro por Comando de Voz:**
  - O `CognitiveOrchestrator` agora detecta intenções naturais de troca de IA por voz (*"Sexta-Feira, use o Claude"*, *"ativar GPT"*, *"trocar para o Gemini"*), alternando o núcleo ativo a quente e notificando o HUD via SignalR.
- **Interface Holográfica de Gerenciamento de IA no HUD React:**
  - Criado o modal `AiAccountManagerModal` no padrão visual sci-fi HUD da Sexta-Feira.
  - Conexão em 1-clique via Google OAuth, atalhos rápidos com validação em tempo real e botões de teste de latência (ping em ms).
  - Atualizado o card `ModelCoreWidget` com o botão de acesso rápido às contas de IA e telemetria do cérebro ativo.

## [v1.1.0] - 2026-09-30

### 🚀 Novas Funcionalidades
- **Auto-Evolução do Código (Self-Coding / Live Self-Modification):**
  - Implementado `SelfEvolutionService` e interface `ISelfEvolutionService` para permitir que a Sexta-Feira altere, refatore e expanda seu próprio código C#/.NET em tempo de execução via voz ou comandos.
  - Adicionada verificação de compilação automatizada (`dotnet build`) com backup e restauração automática se houver falha de compilação.
  - Criado endpoint `POST /api/evolution/evolve` e a ferramenta MCP `self_evolve_codebase`.
- **Conectores MCP Spotify & Google:**
  - Adicionado suporte nativo a ferramentas MCP para Spotify (`spotify_get_current`, `spotify_play`, `spotify_search`).
  - Adicionadas ferramentas Google Services (`google_gmail_send`, `calendar_get_upcoming`).
  - Integração limpa sem necessidade de chaves de API obrigatórias manuais para demonstração fluida e conexões desacopladas.
- **Voz & Wake-Word Multilíngue:**
  - Atualizado `useVoiceEngine.ts` com suporte a wake-words em múltiplos idiomas:
    - Português: `"Ei, Sexta-Feira"`, `"Ei, Sexta feira"`, `"Sexta-Feira"`
    - Inglês: `"Hey Friday"`, `"Hi Friday"`, `"OK Friday"`
    - Espanhol/Francês: `"Oye Sexta feira"`, `"Bonjour Friday"`
  - Atualizada a interface HUD React com indicador de wake-word multilíngue e suporte a Spotify / Self-Coding.
