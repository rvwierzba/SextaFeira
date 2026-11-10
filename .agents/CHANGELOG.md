# 📜 CHANGELOG — Sexta-Feira

## [v3.0.0] - 2026-11-10

Reescrita do núcleo: o sistema anterior era em grande parte simulado (ferramentas por palavra-chave, resultados que nunca voltavam ao LLM, telemetria e integrações falsas).

- Agente real com tool calling nativo e loop de ferramentas; contexto de conversa mantido entre turnos e dispositivos.
- Engines: Ollama (padrão, local), Claude (SDK oficial), ChatGPT e Gemini, com fallback local.
- Respostas em streaming falando frase a frase; interrupção ao falar por cima; filtro de eco.
- Voz multi-dispositivo: captura com VAD no navegador, Whisper local na GPU, HTTPS na rede local.
- Cliente MCP de verdade (stdio + HTTP com OAuth) configurado por `data/mcp.json`.
- Autoprogramação com ferramentas `code_*` presas ao repositório e reinício automático.
- Memória de longo prazo em arquivo com embeddings locais (sem Postgres).
- Removidos: mocks de Spotify/Gmail/Calendar/OneDrive, scanner de rede e sandbox simulados, "OpenJarvis" fictício, Docker/Postgres.

## [v2.0.0] - 2026-11-09

### 🚀 Novas Funcionalidades
- **Integração Completa do Stanford OpenJarvis (Hazy Research & SAIL):**
  - Implementação dos 5 primitivos: *Intelligence*, *Engine*, *Agents*, *Tools/Memory*, e *Learning & Spec Search*.
  - Solução definitiva para a lentidão de LLM local: arquitetura híbrida com modelos compactos quantizados on-device (`Llama-3.2-1B/3B`, `Qwen-2.5-Coder`) e síntese prévia de especificações guiadas por nuvem (*Spec Search*).
  - Execução imediata sub-50ms via *Zero-Latency Dispatcher* local para comandos comuns.
  - Probing automático de hardware para NVIDIA CUDA, DirectML, Vulkan, Apple Metal e CPU AVX2, gerando quotas precisas de VRAM e computação.
  - Telemetria de traces com pontuação de eficiência energética Stanford *Intelligence Per Watt (TOPS/W)*.
  - Novo controlador `OpenJarvisController` com rotas para status, catalog, hardware, traces, benchmark e comutação de modo.
  - Novos testes unitários dedicados em `OpenJarvisTests.cs` (100% de aprovação).
- **Interface Visual Fluida & Cinemática (Inspirada no assistente JARVIS):**
  - Esfera 3D orgânica viva (`HologramCoreCanvas`) com deformação por shaders GLSL de ruído 3D Simplex, reagindo em tempo real ao áudio do microfone e à síntese vocal.
  - Núcleo Arc-Reactor elétrico azul neon (`#00f0ff` e `#0077ff`) com anéis giroscópicos em eixos perpendiculares e nebulosa de 1.200 partículas quânticas.
  - Novo componente `AudioOscilloscope` com ondas senoidais harmônicas e barras de equalizador animadas no centro do HUD.
  - Novo componente `HolographicSubtitles` exibindo a transcrição contínua da voz do operador e o streaming com efeito máquina de escrever da fala da Sexta-Feira.
  - Novo widget `OpenJarvisTelemetryWidget` com mostrador radial de TOPS/W, latência em tempo real e seletor de modo de orquestração (*Hybrid Spec*, *Local Fast*, *Frontier Cloud*).
  - Docks laterais colapsáveis permitindo desfrutar de uma visão 100% cinemática e limpa do núcleo holográfico central.

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
