<p align="center">
  <img src="docs/assets/logo-placeholder.svg" width="120" alt="Sexta-Feira Logo"/>
  <br/>
  <strong>🧠 SEXTA-FEIRA (F.R.I.D.A.Y.)</strong>
  <br/>
  <em>Harness de IA Autônoma Multimodal</em>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/version-1.0.0-blue?style=for-the-badge" alt="Version"/>
  <img src="https://img.shields.io/badge/.NET-10.0-purple?style=for-the-badge&logo=dotnet" alt=".NET 10"/>
  <img src="https://img.shields.io/badge/React-18+-61DAFB?style=for-the-badge&logo=react" alt="React"/>
  <img src="https://img.shields.io/badge/Three.js-WebGL-black?style=for-the-badge&logo=threedotjs" alt="Three.js"/>
  <img src="https://img.shields.io/badge/PostgreSQL-pgvector-336791?style=for-the-badge&logo=postgresql" alt="PostgreSQL"/>
  <img src="https://img.shields.io/badge/Docker-Container-2496ED?style=for-the-badge&logo=docker" alt="Docker"/>
  <img src="https://img.shields.io/badge/license-MIT-green?style=for-the-badge" alt="License"/>
</p>

---

## 📖 Sumário

- [Visão Geral](#-visão-geral)
- [Arquitetura](#-arquitetura)
- [Stack Tecnológica](#-stack-tecnológica)
- [Funcionalidades Principais](#-funcionalidades-principais)
- [Estrutura do Projeto](#-estrutura-do-projeto)
- [Pré-Requisitos](#-pré-requisitos)
- [Instalação e Setup](#-instalação-e-setup)
- [Execução Local](#-execução-local)
- [Variáveis de Ambiente](#-variáveis-de-ambiente)
- [API Endpoints](#-api-endpoints)
- [Integrações de Serviços (OAuth)](#-integrações-de-serviços-oauth)
- [Auto-Evolução do Código](#-auto-evolução-do-código)
- [Documentação do Agente (.agents)](#-documentação-do-agente-agents)
- [Roadmap](#-roadmap)
- [Contribuição](#-contribuição)
- [Licença](#-licença)

---

## 🌟 Visão Geral

**Sexta-Feira** é um Harness de Inteligência Artificial autônomo, multimodal e cross-platform, inspirado na assistente F.R.I.D.A.Y. do universo Marvel. O sistema opera como um **PWA com interface HUD Sci-Fi holográfica**, com interação prioritária por **voz em Português do Brasil (PT-BR)** e capacidade de agir no mundo real através de **agentes autônomos** e integração via **Model Context Protocol (MCP)**.

### Filosofia Central

- **Desacoplado de LLMs específicas** — Suporte a múltiplos provedores (Ollama local, OpenAI, Anthropic, Google Gemini, Groq) com fallback inteligente automático
- **Zero-Trust Execution** — Todo código gerado pela IA é executado em containers Docker isolados
- **Memória de Longo Prazo** — PostgreSQL + pgvector para busca semântica vetorial com reciclagem automática de contexto
- **Self-Healing** — Motor de auto-correção que testa, identifica erros e refatora código automaticamente
- **Voz-First** — Interação natural em PT-BR com STT (Whisper) e TTS (Azure Neural)

---

## 🏗 Arquitetura

O projeto segue **Clean Architecture + Domain-Driven Design (DDD)**, garantindo separação clara de responsabilidades:

```
┌─────────────────────────────────────────────────────────────┐
│                    🖥️ FRONTEND (HUD)                        │
│         React + Three.js/WebGL + SignalR Client             │
│         Esfera Holográfica · Voz · Painel de Controle       │
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
│   MemoryRecyclerService · Use Cases                         │
└───────┬───────────────────────────────────┬─────────────────┘
        │                                   │
┌───────▼───────────┐             ┌─────────▼─────────────────┐
│  📐 DOMAIN        │             │  🔌 INFRASTRUCTURE         │
│  Entities         │◄────────────│  LLM Providers (5+)       │
│  Interfaces       │             │  MCP Server Registry      │
│  Enums            │             │  PostgreSQL + pgvector     │
│  Value Objects    │             │  Docker Sandbox            │
│                   │             │  Playwright Stealth        │
│                   │             │  Network Scanner           │
│                   │             │  Voice Engine (STT/TTS)    │
│                   │             │  Geolocation (GPS/IP)      │
│                   │             │  OAuth Service Connectors  │
└───────────────────┘             └─────────────────────────────┘
```

### Fluxo de Dependências
```
WebApi → Application → Domain ← Infrastructure
```

---

## 🔧 Stack Tecnológica

| Camada | Tecnologia | Versão |
|--------|-----------|--------|
| **Backend** | C# / .NET | 10.0 |
| **API** | ASP.NET Core | 10.0 |
| **Realtime** | SignalR Core | 10.0 |
| **Frontend** | React + TypeScript | 18+ |
| **3D/WebGL** | Three.js | Latest |
| **Database** | PostgreSQL + pgvector | 16 + 0.5 |
| **ORM** | Entity Framework Core | 9.0 |
| **LLM Local** | Ollama (Phi-3 Mini) | Latest |
| **LLM Cloud** | OpenAI, Anthropic, Google Gemini, Groq | Latest |
| **Containerização** | Docker + Docker Compose | 24+ |
| **Web Scraping** | Playwright Stealth | Latest |
| **Voz STT** | Whisper API | Latest |
| **Voz TTS** | Azure Cognitive (pt-BR-FranciscaNeural) | Latest |
| **Build Frontend** | Vite | Latest |

---

## ⚡ Funcionalidades Principais

### 🧠 Core de IA
- **Multi-Provider LLM** com fallback automático (Local → Cloud → Contingência)
- **RAG (Retrieval-Augmented Generation)** com memória vetorial pgvector
- **Tool Calling** nativo com dispatch via MCP para execução no mundo real
- **Self-Healing Engine** — Ciclo de debug-fix-retry automático em Docker Sandbox

### 🎙️ Voz
- **Wake Word Detection** via WebSocket
- **Speech-to-Text (STT)** — Whisper em PT-BR
- **Text-to-Speech (TTS)** — Azure Cognitive Voice (FranciscaNeural PT-BR)
- **Streaming de áudio** bidirecional via SignalR

### 🌐 Agentes & Ferramentas MCP
| Ferramenta | Descrição |
|-----------|-----------|
| `network_scan` | Scanner de rede local (Wi-Fi/Ethernet) |
| `stealth_search` | Busca web furtiva com evasão anti-bot |
| `stealth_extract_url` | Extração de conteúdo de páginas web |
| `docker_execute_code` | Execução isolada de código em Docker |
| `calendar_get_upcoming` | Próximos eventos do Google Calendar |
| `email_get_recent` | E-mails recentes (Gmail/Outlook) |
| `onedrive_list_files` | Arquivos do OneDrive |
| `get_geolocation_weather` | Geolocalização + clima em tempo real |

### 🎨 Interface HUD Sci-Fi
- **Esfera Holográfica** reativa (Three.js/WebGL) que pulsa conforme o estado do agente
- **Painel de telemetria** em tempo real via SignalR
- **Terminal integrado** para saída de sandbox/ferramentas
- **Design glassmorphism** com efeitos de partículas e glow

### 🔗 Integrações de Serviços
- Google (Gmail, YouTube, Drive, Calendar)
- LinkedIn
- Spotify
- E mais via arquitetura extensível de OAuth Providers

---

## 📁 Estrutura do Projeto

```
SextaFeira/
├── 📄 README.md                              ← Você está aqui
├── 📄 SextaFeira.sln                         ← Solution .NET
├── 📄 docker-compose.yml                     ← Orquestração Docker
├── 📄 Dockerfile.backend                     ← Build do backend
├── 📁 .agents/                               ← Documentação auto-evolutiva do agente
│   ├── 📄 ARCHITECTURE.md                    ← Decisões arquiteturais
│   ├── 📄 CONVENTIONS.md                     ← Padrões e convenções de código
│   ├── 📄 DOMAIN_MODEL.md                    ← Modelo de domínio
│   ├── 📄 INFRASTRUCTURE_GUIDE.md            ← Guia de infraestrutura
│   ├── 📄 MCP_TOOLS_REGISTRY.md              ← Catálogo de ferramentas MCP
│   ├── 📄 OAUTH_INTEGRATION_GUIDE.md         ← Guia de integração OAuth
│   ├── 📄 SELF_EVOLUTION_PROTOCOL.md         ← Protocolo de auto-evolução
│   └── 📄 CHANGELOG.md                      ← Log de mudanças automático
├── 📁 src/
│   ├── 📁 SextaFeira.Domain/                 ← Entidades, Interfaces, Enums
│   ├── 📁 SextaFeira.Application/            ← Orquestradores, Use Cases
│   ├── 📁 SextaFeira.Infrastructure/         ← Implementações concretas
│   │   ├── 📁 Geolocation/                   ← GPS/IP em tempo real
│   │   ├── 📁 Integrations/                  ← Conectores de serviços
│   │   ├── 📁 LLMs/                          ← Provedores de LLM
│   │   ├── 📁 MCP/                           ← Registry de ferramentas MCP
│   │   ├── 📁 Network/                       ← Scanner de rede local
│   │   ├── 📁 Persistence/                   ← EF Core + pgvector
│   │   ├── 📁 Sandbox/                       ← Docker container runner
│   │   ├── 📁 Stealth/                       ← Playwright anti-bot
│   │   ├── 📁 Voice/                         ← STT/TTS engines
│   │   └── 📁 WebAutomation/                 ← Browser automation
│   ├── 📁 SextaFeira.WebApi/                 ← ASP.NET Core API + SignalR
│   │   ├── 📁 Controllers/                   ← REST endpoints
│   │   ├── 📁 Hubs/                          ← SignalR hubs (Telemetria + Voz)
│   │   ├── 📄 Program.cs                     ← DI Container + Pipeline
│   │   └── 📄 appsettings.json               ← Configuração
│   └── 📁 SextaFeira.Web/                    ← Frontend React + Three.js
│       ├── 📁 src/
│       │   ├── 📄 App.tsx                     ← Componente principal HUD
│       │   ├── 📁 components/                 ← Componentes UI
│       │   ├── 📁 hooks/                      ← Custom hooks React
│       │   └── 📁 styles/                     ← CSS/estilos
│       ├── 📄 index.html                      ← Entry point
│       ├── 📄 vite.config.ts                  ← Configuração Vite
│       └── 📄 package.json                    ← Dependências frontend
└── 📁 tests/
    └── 📁 SextaFeira.Tests/                   ← Testes unitários e de integração
```

---

## 📋 Pré-Requisitos

| Requisito | Versão Mínima | Necessário |
|-----------|--------------|------------|
| [.NET SDK](https://dotnet.microsoft.com/download) | 10.0+ | ✅ |
| [Node.js](https://nodejs.org/) | 18+ | ✅ |
| [Docker Desktop](https://www.docker.com/products/docker-desktop) | 24+ | ✅ |
| [PostgreSQL](https://www.postgresql.org/) | 16+ | ⚙️ (via Docker) |
| [Ollama](https://ollama.ai/) | Latest | 🔧 (opcional p/ LLM local) |

---

## 🚀 Instalação e Setup

### 1. Clonar o Repositório
```bash
git clone https://github.com/seu-usuario/SextaFeira.git
cd SextaFeira
```

### 2. Subir Infraestrutura Docker
```bash
docker-compose up -d
```
Isso inicia:
- **PostgreSQL + pgvector** na porta `5432`
- **Ollama** na porta `11434`

### 3. (Opcional) Baixar Modelo Local
```bash
docker exec -it sextafeira-ollama ollama pull phi3:mini
```

### 4. Restaurar Backend
```bash
dotnet restore
dotnet build
```

### 5. Instalar Frontend
```bash
cd src/SextaFeira.Web
npm install
```

---

## ▶️ Execução Local

### Backend (API + SignalR)
```bash
cd src/SextaFeira.WebApi
dotnet run
```
API disponível em: `http://localhost:5000`

Swagger UI: `http://localhost:5000/swagger`

### Frontend (HUD React)
```bash
cd src/SextaFeira.Web
npm run dev
```
HUD disponível em: `http://localhost:5173`

### Tudo via Docker Compose
```bash
docker-compose up --build
```

---

## 🔐 Variáveis de Ambiente

| Variável | Descrição | Default |
|----------|-----------|---------|
| `ConnectionStrings__PostgresVector` | Connection string PostgreSQL | `Host=localhost;...` |
| `Ollama__BaseUrl` | URL do servidor Ollama | `http://localhost:11434` |
| `Ollama__Model` | Modelo Ollama padrão | `phi3:mini` |
| `LLM__GoogleGemini__ApiKey` | API Key Google Gemini | *(vazio)* |
| `LLM__OpenAI__ApiKey` | API Key OpenAI | *(vazio)* |
| `LLM__Anthropic__ApiKey` | API Key Anthropic | *(vazio)* |
| `LLM__Groq__ApiKey` | API Key Groq | *(vazio)* |
| `OAuth__Google__ClientId` | OAuth Google Client ID | *(vazio)* |
| `OAuth__Google__ClientSecret` | OAuth Google Client Secret | *(vazio)* |
| `OAuth__LinkedIn__ClientId` | OAuth LinkedIn Client ID | *(vazio)* |
| `OAuth__Spotify__ClientId` | OAuth Spotify Client ID | *(vazio)* |

---

## 📡 API Endpoints

### Core
| Método | Rota | Descrição |
|--------|------|-----------|
| `POST` | `/api/core/process` | Processa input do usuário (texto/voz) |
| `GET` | `/api/core/status` | Status do sistema e telemetria |

### Sandbox
| Método | Rota | Descrição |
|--------|------|-----------|
| `POST` | `/api/sandbox/execute` | Executa código em Docker isolado |
| `POST` | `/api/sandbox/self-heal` | Loop de auto-correção de código |

### Rede
| Método | Rota | Descrição |
|--------|------|-----------|
| `GET` | `/api/network/devices` | Escaneia dispositivos na rede local |

### Integrações Pessoais
| Método | Rota | Descrição |
|--------|------|-----------|
| `GET` | `/api/personal/calendar` | Próximos eventos do calendário |
| `GET` | `/api/personal/emails` | E-mails recentes |
| `GET` | `/api/personal/onedrive` | Arquivos do OneDrive |

### Geolocalização
| Método | Rota | Descrição |
|--------|------|-----------|
| `GET` | `/api/geolocation/live?lat=&lon=` | Localização + clima em tempo real |

### Memória Vetorial
| Método | Rota | Descrição |
|--------|------|-----------|
| `GET` | `/api/memory/list` | Listar memórias ativas |
| `POST` | `/api/memory/recycle` | Executar garbage collection de memórias |

### SignalR Hubs
| Hub | Rota | Descrição |
|-----|------|-----------|
| `HudTelemetryHub` | `/hubs/telemetry` | Telemetria e estado do agente em tempo real |
| `VoiceStreamHub` | `/hubs/voice` | Streaming bidirecional de áudio |

---

## 🔗 Integrações de Serviços (OAuth)

O Sexta-Feira suporta conexão fácil com múltiplos serviços externos. A arquitetura de OAuth foi desenhada para ser extensível — basta adicionar um novo `IOAuthProvider` na camada de Infrastructure.

### Serviços Suportados (Roadmap)
| Serviço | Escopo | Status |
|---------|--------|--------|
| **Google** (Gmail, YouTube, Drive, Calendar) | Completo | 🔜 Em implementação |
| **LinkedIn** | Perfil + Feed | 🔜 Em implementação |
| **Spotify** | Reprodução + Playlists | 🔜 Em implementação |
| **GitHub** | Repos + Issues + PRs | 📋 Planejado |
| **Microsoft 365** | Outlook + OneDrive + Teams | 📋 Planejado |
| **Discord** | Mensagens + Canais | 📋 Planejado |
| **Notion** | Páginas + Databases | 📋 Planejado |

### Como Funciona
1. **Comando de Voz:** O usuário diz *"Sexta-Feira, conectar ao meu Google"*
2. **Orquestrador:** Detecta a intenção e dispara o fluxo OAuth
3. **HUD:** Exibe uma tela de login segura (popup/redirect) para o serviço
4. **Token Storage:** Tokens são armazenados de forma segura (criptografados) na memória persistente
5. **Pronto:** Serviço fica disponível como ferramenta MCP

---

## 🧬 Auto-Evolução do Código

O Sexta-Feira é capaz de se **auto-desenvolver**, gerando e integrando novas funcionalidades autonomamente:

### Protocolo de Auto-Evolução
1. **Análise de Contexto** — O agente analisa a documentação em `.agents/` para entender a arquitetura
2. **Geração de Código** — Usa o LLM para gerar implementações seguindo as convenções do projeto
3. **Sandbox Testing** — Testa o código gerado em Docker isolado (Zero-Trust)
4. **Self-Healing** — Se falhar, entra no loop de auto-correção (até 3 iterações)
5. **Integração** — Se aprovado, integra o código ao projeto e atualiza a documentação
6. **Documentação Automática** — Atualiza `.agents/CHANGELOG.md` e docs relevantes

> ⚠️ **Segurança:** Todo código gerado é executado em containers isolados antes de qualquer integração ao codebase principal.

---

## 📖 Documentação do Agente (.agents)

A pasta `.agents/` contém documentação viva que o próprio sistema mantém atualizada. Ela serve como **contexto de referência** para que o agente (e desenvolvedores humanos) compreendam a arquitetura, convenções e estado atual do projeto.

Veja [`.agents/README.md`](.agents/README.md) para o índice completo.

---

## 🗺️ Roadmap

- [x] Clean Architecture + DDD scaffolding
- [x] CognitiveOrchestrator com fallback multi-provider
- [x] SelfHealingEngine com Docker Sandbox
- [x] McpServerRegistry com 8 ferramentas
- [x] HUD Sci-Fi holográfica (React + Three.js)
- [x] Geolocalização em tempo real (GPS/IP + clima)
- [x] Network Scanner (rede local)
- [x] SignalR telemetria em tempo real
- [ ] OAuth Provider Framework (Google, LinkedIn, Spotify)
- [ ] Protocolo de Auto-Evolução do Código
- [ ] Testes E2E com Playwright
- [ ] PWA + Service Worker offline
- [ ] Whisper local (sem cloud)
- [ ] Plugin system MCP dinâmico
- [ ] Dashboard admin de tokens OAuth

---

## 🤝 Contribuição

1. Fork o repositório
2. Crie uma branch (`git checkout -b feature/nova-feature`)
3. Faça commit das mudanças (`git commit -m 'feat: Descrição da feature'`)
4. Push para a branch (`git push origin feature/nova-feature`)
5. Abra um Pull Request

### Convenções de Commit
- `feat:` Nova funcionalidade
- `fix:` Correção de bug
- `docs:` Atualização de documentação
- `refactor:` Refatoração de código
- `test:` Adição ou correção de testes
- `chore:` Tarefas de manutenção

---

## 📄 Licença

Este projeto está licenciado sob a **MIT License** — veja [LICENSE](LICENSE) para detalhes.

---

<p align="center">
  <strong>🧠 Sexta-Feira AI</strong> — <em>"Estou ao seu dispor, senhor."</em>
  <br/>
  Feito com 💙 e muita IA
</p>
