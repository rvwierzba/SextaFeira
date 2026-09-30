# 🔐 Guia de Integração de Serviços (MCP & OAuth)

## 📌 Visão Geral
O Sexta-Feira integra serviços como Spotify e Google (Gmail, Calendar, Gemini, YouTube) de forma desacoplada através de ferramentas MCP (Model Context Protocol).

## 🎵 Conector Spotify MCP
Ferramentas disponíveis:
- `spotify_get_current`: Retorna o estado e música atual em reprodução.
- `spotify_play`: Reproduz músicas/playlists especificadas no comando de voz.
- `spotify_search`: Busca faixas e artistas no catálogo.

## 📧 Conector Google MCP
Ferramentas disponíveis:
- `google_gmail_send`: Envia e-mails formatados.
- `calendar_get_upcoming`: Retorna compromissos da agenda.
- `google_gemini_generate`: Inferência via Google Gemini.

> **Resiliência:** Quando as chaves de API não estão configuradas no ambiente, o sistema utiliza mocks/pontes de demonstração inteligente para que as ferramentas MCP respondam sem lançar exceções.
