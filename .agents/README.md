# 📖 .agents — Documentação Auto-Evolutiva do Sexta-Feira

Esta pasta contém a **documentação viva** do projeto Sexta-Feira. Ela é mantida tanto por desenvolvedores humanos quanto pelo próprio agente de IA.

## 📚 Índice de Documentos

| Documento | Descrição | Última Atualização |
|-----------|-----------|-------------------|
| [ARCHITECTURE.md](ARCHITECTURE.md) | Decisões arquiteturais, padrões de design, e fluxo de dependências | 2026-09-30 |
| [CONVENTIONS.md](CONVENTIONS.md) | Padrões de código, naming, estrutura de pastas e convenções | 2026-09-30 |
| [DOMAIN_MODEL.md](DOMAIN_MODEL.md) | Modelo de domínio: entidades, value objects, interfaces e enums | 2026-09-30 |
| [INFRASTRUCTURE_GUIDE.md](INFRASTRUCTURE_GUIDE.md) | Guia de implementações na camada de Infrastructure | 2026-09-30 |
| [MCP_TOOLS_REGISTRY.md](MCP_TOOLS_REGISTRY.md) | Catálogo completo de 13 ferramentas MCP registradas | 2026-09-30 |
| [OAUTH_INTEGRATION_GUIDE.md](OAUTH_INTEGRATION_GUIDE.md) | Guia de integração com serviços externos via MCP e OAuth | 2026-09-30 |
| [SELF_EVOLUTION_PROTOCOL.md](SELF_EVOLUTION_PROTOCOL.md) | Protocolo de auto-evolução e auto-desenvolvimento | 2026-09-30 |
| [CHANGELOG.md](CHANGELOG.md) | Log de mudanças evolutivas (mantido automaticamente) | 2026-09-30 |

## 🤖 Auto-Manutenção

Quando o agente Sexta-Feira modifica o codebase, ele **deve** atualizar os documentos relevantes nesta pasta. As regras são:

1. **CHANGELOG.md** — Sempre atualizado após qualquer mudança significativa
2. **DOMAIN_MODEL.md** — Atualizado quando novas entidades/interfaces são criadas
3. **MCP_TOOLS_REGISTRY.md** — Atualizado quando ferramentas MCP são adicionadas/removidas
4. **ARCHITECTURE.md** — Atualizado quando decisões arquiteturais são tomadas

## 🧭 Para Agentes de IA

Se você é um agente de IA trabalhando neste projeto, **leia os seguintes documentos nesta ordem**:

1. `ARCHITECTURE.md` — Entenda a estrutura geral
2. `CONVENTIONS.md` — Siga os padrões de código
3. `DOMAIN_MODEL.md` — Compreenda o modelo de domínio
4. `INFRASTRUCTURE_GUIDE.md` — Saiba como implementar serviços
5. `MCP_TOOLS_REGISTRY.md` — Consulte ferramentas disponíveis

> ⚠️ **Regra de Ouro:** Nunca faça uma mudança no código sem atualizar a documentação correspondente.
