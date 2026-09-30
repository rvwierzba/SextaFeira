# 🧬 Protocolo de Auto-Evolução do Código (Self-Coding)

## 🎯 Objetivo
Permitir que a assistente Sexta-Feira adicione novas funcionalidades, corrija bugs ou integre novos conectores ao seu próprio código-fonte enquanto está em execução, mediante comando de voz ou texto.

## 🔄 Fluxo do Protocolo

```
[Comando do Usuário] ──► [CognitiveOrchestrator] ──► [MCP: self_evolve_codebase]
                                                            │
                                                            ▼
                                                [SelfEvolutionService]
                                                            │
                                       ┌────────────────────┴────────────────────┐
                                       ▼                                         ▼
                             [1. Geração de Código]                  [2. Backup do Arquivo]
                                       │                                         │
                                       └────────────────────┬────────────────────┘
                                                            ▼
                                               [3. Aplicação das Mudanças]
                                                            │
                                                            ▼
                                               [4. Validação dotnet build]
                                                            │
                                              ┌─────────────┴─────────────┐
                                              ▼                           ▼
                                      [Sucesso (Build OK)]         [Falha (Build Fail)]
                                              │                           │
                                              ▼                           ▼
                                     [Atualizar CHANGELOG]      [Loop Self-Healing (3x)]
                                              │                           │
                                              ▼                           ▼
                                      [Retornar OK no HUD]    [Rollback do Backup (.bak)]
```

## 🛡️ Medidas de Segurança
1. **Backup Temporário:** Todo arquivo modificado gera uma cópia `.bak` antes do salvamento.
2. **Validação Estrita:** Se o comando `dotnet build` falhar após a edição, entra no loop de correção da LLM (até 3 tentativas). Se não resolver, restaura automaticamente o backup original.
3. **Registro Transparente:** Todas as evoluções bem-sucedidas são registradas no histórico `.agents/CHANGELOG.md`.
