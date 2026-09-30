# 📜 CHANGELOG — Sexta-Feira

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
