#!/usr/bin/env bash
# Inicia a Sexta-Feira no Linux ou macOS: compila o HUD (se necessário), sobe o núcleo
# e o reinicia quando ela se autoprograma.
#
#   ./start.sh               # rede local liberada (celular/tablet podem usar o microfone)
#   ./start.sh --local-only  # só este computador
#   ./start.sh --rebuild-hud # recompila o HUD
set -euo pipefail

root="$(cd "$(dirname "$0")" && pwd)"
web="$root/src/SextaFeira.Web"
api="$root/src/SextaFeira.WebApi"
args=()
rebuild=0

for arg in "$@"; do
  case "$arg" in
    --local-only) args+=("--ListenOnLan=false") ;;
    --rebuild-hud) rebuild=1 ;;
    *) echo "Opção desconhecida: $arg" >&2; exit 1 ;;
  esac
done

command -v dotnet >/dev/null || { echo "Instale o .NET 10 SDK: https://dotnet.microsoft.com/download" >&2; exit 1; }
command -v ollama >/dev/null || echo "Aviso: Ollama não encontrado. Instale em https://ollama.com para o cérebro local." >&2

if [[ $rebuild -eq 1 || ! -f "$api/wwwroot/index.html" ]]; then
  command -v npm >/dev/null || { echo "Instale o Node.js (npm) para compilar o HUD: https://nodejs.org" >&2; exit 1; }
  (cd "$web" && { [[ -d node_modules ]] || npm install; } && npm run build)
fi

# Código 42 = a assistente pediu para reiniciar depois de editar o próprio código (restart_myself).
while true; do
  echo -e "\n▶ Iniciando a Sexta-Feira..."
  set +e
  dotnet run --project "$api" -c Release -- "${args[@]}"
  code=$?
  set -e
  [[ $code -eq 42 ]] || exit $code
  echo "↻ Reiniciando para aplicar mudanças no código..."
done
