# Sexta-Feira

Assistente pessoal por voz, inspirada na F.R.I.D.A.Y. dos filmes do Homem de Ferro. Roda **local por padrão** (Ollama + Whisper na sua GPU), aceita seu LLM favorito quando você quiser (Claude, ChatGPT ou Gemini), conecta em serviços por **MCP**, edita o próprio código e escuta pelo microfone de **qualquer dispositivo da sua rede**.

## Como funciona

```
 Celular / tablet / PC (navegador)                 Computador principal
 ┌──────────────────────────────┐   SignalR    ┌────────────────────────────────────────┐
 │ Microfone → VAD → PCM 16 kHz │ ───────────▶ │ Whisper local (GPU) → palavra de        │
 │                              │              │ ativação → agente (loop de ferramentas) │
 │ Voz do sistema ◀── frases ◀──│ ◀─────────── │ ← LLM em streaming: Ollama | Claude |   │
 │ HUD 3D + legendas            │              │   ChatGPT | Gemini  + MCP + memória      │
 └──────────────────────────────┘              └────────────────────────────────────────┘
```

1. O navegador detecta quando você começa e termina de falar e manda só a fala (PCM 16 kHz) para o núcleo.
2. O Whisper transcreve localmente. Se a frase começa com "Sexta-Feira" (ou chega logo depois de uma resposta), vira um pedido.
3. O agente conversa com o LLM em streaming, executa ferramentas de verdade (clima, busca, memória, MCP, código), devolve os resultados ao modelo e repete até ter a resposta.
4. Cada frase pronta já é falada no aparelho que perguntou, sem esperar o fim da resposta. Falar por cima interrompe.

## Começando

Pré-requisitos: **.NET 10 SDK**, **Node.js**, **Ollama** e uma GPU com Vulkan (opcional, mas recomendada). Prefere Docker? Veja [Docker](#docker).

```bash
ollama pull llama3.1:8b         # ou outro modelo com suporte a ferramentas (veja abaixo)
ollama pull nomic-embed-text    # memória de longo prazo
./start.ps1                     # Windows — no Linux/macOS: ./start.sh
```

Abra `http://localhost:5000`, clique em **Ligar microfone** e diga *"Sexta-Feira, como está o tempo?"*.

Na primeira execução o núcleo baixa o modelo Whisper escolhido (padrão `small`, ~190 MB) para `data/models`.

### Outros dispositivos (celular, tablet, outro PC)

O terminal mostra os endereços da rede, por exemplo `https://192.168.1.100:5001`. Abra no aparelho, aceite o aviso de certificado autoassinado ("Avançado → Continuar") e ligue o microfone. É o HTTPS que permite ao navegador liberar o microfone fora do `localhost`. Para usar só no PC: `./start.ps1 -LocalOnly` (ou `./start.sh --local-only`).

Todos os aparelhos compartilham a mesma conversa; quem pergunta é quem ouve a resposta.

## Plataformas

A Sexta-Feira tem duas partes:

- **Núcleo** (servidor .NET 10 + Ollama + Whisper): roda em **Windows, Linux e macOS** (x64 e ARM, inclusive Apple Silicon e Raspberry Pi 5), ou em **Docker**. Fica num computador da casa: PC, Mac, mini PC, NAS ou servidor.
- **Dispositivos** (HUD no navegador): qualquer aparelho com navegador moderno, ou seja **Android, iPhone/iPad, Windows, macOS, Linux**, TV ou tablet. Eles só capturam o áudio e falam a resposta; o pesado roda no núcleo.

| Como rodar o núcleo | Comando | Reconhecimento de voz |
|---|---|---|
| Windows | `./start.ps1` | GPU (Vulkan) ou CPU |
| Linux | `./start.sh` | GPU (Vulkan) ou CPU |
| macOS | `./start.sh` | CPU (Apple Silicon é rápido o suficiente para `small`) |
| Docker (Linux/servidor) | `docker compose up -d` | CPU |

### Docker

```bash
docker compose up -d                                              # usa o Ollama do host (porta 11434)
OLLAMA_URL=http://ollama:11434 docker compose --profile ollama up -d   # ou sobe um Ollama junto
PUBLIC_HOSTS=192.168.1.50 docker compose up -d                    # IP/nome do servidor na rede, para o HTTPS
```

Variáveis úteis: `OLLAMA_URL`, `OLLAMA_MODEL`, `WHISPER_MODEL` (`base` em CPUs fracas, `small` como padrão), `PUBLIC_HOSTS`, `TZ`. Os dados ficam no volume `sextafeira-data`. No container a autoprogramação fica desligada, porque ele não tem o código-fonte nem o SDK; para ela, rode direto no sistema.

Se o Ollama do host só escuta em `127.0.0.1`, o container não o alcança: inicie-o com `OLLAMA_HOST=0.0.0.0`.

## Cérebro (LLM)

| Engine | Como conectar | Observações |
|---|---|---|
| **Ollama (padrão)** | nada; usa o modelo de `Configurações → Modelos` | 100% local. Precisa de modelo com *tool calling*. |
| Claude | chave nas configurações, ou o perfil do `ant auth login` | SDK oficial da Anthropic; padrão `claude-opus-5-5`, esforço baixo para respostas rápidas. |
| ChatGPT | chave da OpenAI nas configurações | qualquer modelo de chat da OpenAI |
| Gemini | chave do Google AI Studio nas configurações | endpoint compatível com OpenAI |

Se a nuvem falhar, ela responde com o modelo local (dá para desligar).

**Escolha do modelo local:** o Llama 3.1 8B não cabe inteiro em GPUs de 6 GB (parte roda na CPU), então a primeira palavra leva cerca de 3 s. Modelos menores com bom suporte a ferramentas, como `qwen3:4b` ou `qwen2.5:7b`, cabem inteiros e respondem bem mais rápido. Troque em **Configurações**.

## Serviços (MCP)

Edite em **Configurações → Serviços conectados** (ou `data/mcp.json`). O formato é o mesmo do Claude Desktop, Cursor e VS Code:

```jsonc
{
  "mcpServers": {
    "arquivos": { "command": "npx", "args": ["-y", "@modelcontextprotocol/server-filesystem", "C:\\Users\\voce\\Documents"] },
    "github":   { "url": "https://api.githubcopilot.com/mcp/" }
  }
}
```

- Servidores **locais** (`command`) rodam como processos filhos.
- Servidores **remotos** (`url`) com OAuth abrem o login no navegador do PC uma vez; o token fica salvo em `data/mcp-tokens`. É assim que dá para conectar contas sem chave de API, quando o serviço oferece um servidor MCP remoto.
- As ferramentas aparecem para a assistente como `servidor__ferramenta`.

## Ferramentas embutidas

| Ferramenta | O que faz |
|---|---|
| `get_weather` | clima e previsão (Open-Meteo, sem chave) |
| `web_search` / `read_webpage` | Wikipédia + Google Notícias (RSS); opcionalmente seu SearXNG (`SearxngUrl`) |
| `remember` / `forget` | memória de longo prazo local; as memórias relevantes entram sozinhas no contexto |
| `open_on_computer` | abre um link no navegador do PC |
| `code_*`, `restart_myself` | **autoprogramação**: lê, busca, edita e compila o próprio código, desfaz mudanças e reinicia. Nunca faz commit sem você pedir. |

## Voz

- **Reconhecimento:** Whisper local (`tiny` a `large-v3-turbo`), acelerado por Vulkan.
- **Fala:** voz neural do próprio aparelho: no Edge, "Francisca Online (Natural)"; no Chrome, "Google português do Brasil"; no iPhone, "Luciana". Escolha em **Configurações → Voz**. Para um TTS no servidor, implemente `ITextToSpeech`.
- **Palavras de ativação:** "Sexta-Feira", "Sexta", "Ei Sexta", "Friday", tolerantes a erros de transcrição ("Sesta-Feira"). Depois de uma resposta, dá para continuar falando sem repetir o nome por alguns segundos (configurável).

## Estrutura

```
src/SextaFeira.Domain          contratos: mensagens, engines, ferramentas, memória, voz, configurações
src/SextaFeira.Application     agente (loop de ferramentas), prompt, divisor de frases, palavra de ativação
src/SextaFeira.Infrastructure  engines (Ollama/OpenAI/Gemini/Anthropic), MCP, Whisper, memória, ferramentas
src/SextaFeira.WebApi          hub SignalR multi-dispositivo, API de configurações, HTTPS da rede local
src/SextaFeira.Web             HUD React + Three.js (captura de voz, fala, configurações)
tests/SextaFeira.Tests         testes do agente, palavra de ativação e streaming
data/                          (fora do git) configurações, chaves, memória, modelos, certificados
```

## Desenvolvimento

```powershell
dotnet test                                   # testes
cd src/SextaFeira.Web; npm run dev            # HUD com hot reload em :5173 (usa o núcleo em :5000)
dotnet run --project src/SextaFeira.WebApi    # núcleo
```

## Licença

MIT
