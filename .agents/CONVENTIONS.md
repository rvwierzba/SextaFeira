# Convenções

- C# / .NET 10, `Nullable` habilitado, namespaces = pastas. Textos para o usuário em português do Brasil.
- Nada de dados simulados: se algo não está configurado ou falhou, diga isso (no log, no `/api/status` ou na fala). Nunca invente resultados.
- Ferramentas retornam texto curto e útil para o modelo; erros começam com "Erro:" em vez de lançar exceção para o agente.
- Chaves e dados pessoais ficam em `data/` (fora do git). Nunca registre chaves em log.
- Frontend: React + TypeScript; chamadas sempre por caminho relativo (`/api`, `/hubs`) para funcionar em qualquer dispositivo da rede.
- Rode `dotnet test` e `npm run build` (em `src/SextaFeira.Web`) antes de concluir uma mudança.
