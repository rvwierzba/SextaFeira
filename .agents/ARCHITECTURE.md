# 🏗 Arquitetura do Sistema — Sexta-Feira AI (v2.0)
*Integração Stanford OpenJarvis (Hazy Research & SAIL) & HUD Fluido Cinematográfico*

## 📌 Filosofia Arquitetural

O Sexta-Feira segue **Clean Architecture + Domain-Driven Design (DDD)** com desacoplamento rigoroso entre camadas, agora expandido com o motor local-first **OpenJarvis da Universidade de Stanford** e uma experiência visual fluida holográfica inspirada em reatores de fusão e assistentes cinemáticos:

```
┌─────────────────────────────────────────────────────────────┐
│                    🖥️ FRONTEND (HUD FLUIDO)                 │
│         React 18 + Three.js (Shaders Simplex Noise)         │
│         Esfera Orgânica Viva Ondulante · Osciloscópio Áudio │
│         Legendas Holográficas Stream · Docks Retráteis      │
│         Painel Telemetria Stanford OpenJarvis & Multi-Hub   │
└───────────────────────┬─────────────────────────────────────┘
                        │ REST API + WebSocket (SignalR)
┌───────────────────────▼─────────────────────────────────────┐
│                  📡 WEB API (ASP.NET Core)                   │
│         Controllers (OpenJarvis, AiConnections, Core, etc)  │
│         Hubs (HudTelemetry, VoiceStream) · CORS             │
└───────────────────────┬─────────────────────────────────────┘
                        │
┌───────────────────────▼─────────────────────────────────────┐
│               🧠 APPLICATION (Orchestrators)                 │
│   CognitiveOrchestrator · IOpenJarvisEngine                 │
│   SelfHealingEngine · SelfEvolutionService                  │
└───────┬───────────────────────────────────┬─────────────────┘
        │                                   │
┌───────▼───────────┐             ┌─────────▼─────────────────┐
│  📐 DOMAIN        │             │  🔌 INFRASTRUCTURE         │
│  Entities         │◄────────────│  OpenJarvisInferenceEngine │
│  OpenJarvisModels │             │  AiConnectionManager       │
│  HardwareQuota    │             │  GoogleGeminiCloudProvider │
│  SpecSearchPlan   │             │  OpenAiGptCloudProvider    │
│  IntelligenceSpec │             │  AnthropicClaudeCloudProv. │
│  OpenJarvisTrace  │             │  MCP Server Registry (13) │
│  Interfaces       │             │  PostgreSQL + pgvector     │
│  Enums (OpenJarvis│             │  Docker Sandbox            │
│  Local = 6)       │             │  Voice Engine (STT/TTS)    │
└───────────────────┘             └─────────────────────────────┘
```

---

## 🏛️ Os 5 Primitivos do Stanford OpenJarvis

Inspirado na arquitetura de agentes autônomos e no princípio **"Intelligence Per Watt"** do Stanford Hazy Research / Stanford AI Lab (SAIL):

### 1. Intelligence (Model Catalog & Quantization)
- Catálogo de inteligência on-device de alta densidade: `Llama-3.2-1B`, `Llama-3.2-3B`, `Qwen-2.5-Coder-1.5B`, `Phi-3.5-mini`.
- Execução prioritária em quantizações `Q4_K_M` e `Q4_0` para caber em VRAM dedicada (2GB a 8GB) com throughput superior a 60 tokens/segundo.

### 2. Engine (Hardware-Aware Execution Runtime)
- Probing automático de aceleradores de hardware (`ProbeHardwareAsync`): detecção de NVIDIA CUDA via `nvidia-smi`, Apple Silicon Metal, DirectX/Vulkan ou CPU AVX2 multi-core.
- Cálculo de quota de memória física e GPU para evitar saturação e paginação de memória.

### 3. Agents (Hierarchical & Zero-Latency Dispatcher)
- Desacoplamento entre orquestrador e tarefas especializadas.
- Resposta imediata sub-50ms para comandos comuns, eliminando a lentidão clássica de chamadas monolíticas em LLMs pesados.

### 4. Tools & Memory (MCP Protocol + pgvector)
- Integração bidirecional com o catálogo de 13 ferramentas MCP (Google Calendar, Gmail, Spotify, Varredura de Rede, Sandbox Docker).
- Memória de longo prazo contextualizada com embeddings e pgvector.

### 5. Learning & Spec Search (Closed-Loop Evolution)
- **Ciclo Fechado: `Diagnose -> Plan -> Execute -> Gate`**:
  - Quando um objetivo complexo é solicitado pela primeira vez, um modelo de fronteira na nuvem (Gemini / GPT-4o / Claude) sintetiza uma especificação enxuta (`SpecSearchPlan`).
  - O plano é compilado e armazenado em cache (`openjarvis_specs_cache.json`).
  - Execuções subsequentes rodam **100% on-device** através do plano compilado, com latência sub-50ms e consumo energético mínimo.
  - Telemetria de traces mede a métrica Stanford de **Intelligence Per Watt (TOPS/W)**.

---

## 🎨 Frontend Fluido e Estética Cinemática

O design visual foi completamente renovado para proporcionar uma experiência orgânica viva:

1. **Esfera Fluida 3D (Three.js Simplex Shader):**
   - Malha de alta resolução que deforma organicamente em tempo real via ruído 3D Simplex e reage à amplitude da voz.
   - Núcleo Arc-Reactor elétrico pulsante em tom ciano neon (`#00f0ff` e `#0077ff`).
   - 1.200 partículas quânticas em órbita celeste dinâmica e anéis giroscópicos em eixos perpendiculares.
2. **Osciloscópio de Áudio Real-Time:**
   - Visualizador de ondas senoidais e barras harmônicas que dança sincronizado ao microfone e à síntese vocal.
3. **Legendas Holográficas Flutuantes:**
   - Stream de texto com efeito máquina de escrever para a fala da Sexta-Feira e transcrição da voz do operador.
4. **Asas de Docks Retráteis:**
   - Painéis laterais colapsáveis com um clique, permitindo visualização totalmente limpa do núcleo holográfico central.
