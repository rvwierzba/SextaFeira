# 📜 Convenções de Código & Naming

## 1. Naming Standards (C# / .NET 10)
- **Namespaces:** PascalCase alinhado com o caminho da pasta (`SextaFeira.Domain.Interfaces`, `SextaFeira.Infrastructure.Integrations`).
- **Interfaces:** Prefixadas com `I` (`ISelfEvolutionService`, `IPersonalIntegrationsService`, `IMcpHost`).
- **Injeção de Dependência:** Declarar via construtor principal com `ILogger<TClass>`.

## 2. Padrões de Exceção & Zero-Breakage
- Ferramentas MCP nunca lançam exceções não tratadas; capturam o erro e retornam `ToolExecutionResult(success: false, error: ex.Message)`.
- Serviços de integração (Spotify, Google) implementam fallbacks elegantes quando credenciais não estão presentes no ambiente.

## 3. Estrutura Frontend (React + TypeScript)
- Componentes UI organizados em `src/components/hud` e `src/components/3d`.
- Hook de voz em `src/hooks/useVoiceEngine.ts` lidando com Web Audio API Analyser + SpeechRecognition multilíngue.
