# Sexta-Feira em container (Linux x64 ou arm64). Whisper roda na CPU; o LLM fica no Ollama (host ou serviço "ollama").
# Uso normal: docker compose up -d   (veja docker-compose.yml)

# ── 1. HUD (React) ─────────────────────────────────────────────────────────
FROM node:22-bookworm-slim AS hud
WORKDIR /src/SextaFeira.Web
COPY src/SextaFeira.Web/package.json src/SextaFeira.Web/package-lock.json ./
RUN npm ci
COPY src/SextaFeira.Web/ ./
# O vite.config.ts publica em ../SextaFeira.WebApi/wwwroot
RUN npm run build

# ── 2. Núcleo (.NET) ───────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY SextaFeira.sln ./
COPY src/SextaFeira.Domain/SextaFeira.Domain.csproj src/SextaFeira.Domain/
COPY src/SextaFeira.Application/SextaFeira.Application.csproj src/SextaFeira.Application/
COPY src/SextaFeira.Infrastructure/SextaFeira.Infrastructure.csproj src/SextaFeira.Infrastructure/
COPY src/SextaFeira.WebApi/SextaFeira.WebApi.csproj src/SextaFeira.WebApi/
RUN dotnet restore src/SextaFeira.WebApi/SextaFeira.WebApi.csproj
COPY src/ src/
COPY --from=hud /src/SextaFeira.WebApi/wwwroot src/SextaFeira.WebApi/wwwroot
RUN dotnet publish src/SextaFeira.WebApi/SextaFeira.WebApi.csproj -c Release -o /app --no-restore

# ── 3. Execução ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0
# libgomp1: paralelismo do Whisper na CPU · nodejs/npm: servidores MCP via "npx" · git: ferramentas de código
RUN apt-get update \
 && apt-get install -y --no-install-recommends libgomp1 nodejs npm git ca-certificates tzdata \
 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app ./
ENV DataPath=/data \
    TZ=America/Sao_Paulo \
    SelfCode__Enabled=false \
    DOTNET_gcServer=0
VOLUME /data
EXPOSE 5000 5001
ENTRYPOINT ["dotnet", "SextaFeira.WebApi.dll"]
