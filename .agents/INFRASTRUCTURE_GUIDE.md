# 🔌 Guia da Camada de Infraestrutura

## 1. Conectores de Serviços (`Integrations/`)
- `PersonalIntegrationsService.cs`: Implementa conectores de dados e streaming para Google (Gmail, Calendar) e Spotify (Player, busca de faixas e listas).

## 2. Barramento MCP (`MCP/`)
- `McpServerRegistry.cs`: Atua como host MCP local despachando chamadas para 13 ferramentas ativas.

## 3. Motor de Auto-Evolução (`Application/Services/SelfEvolutionService.cs`)
- Executa a refatoração do código C# em tempo de execução, valida através de processo de compilação `dotnet build` e atualiza o changelog automaticamente.
