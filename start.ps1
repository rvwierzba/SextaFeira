<#
.SYNOPSIS
  Inicia a Sexta-Feira: compila o HUD (se necessário), sobe o núcleo e o reinicia quando ela se autoprograma.

.EXAMPLE
  ./start.ps1              # rede local liberada (celular/tablet podem usar o microfone)
  ./start.ps1 -LocalOnly   # só este computador
#>
param(
    [switch]$LocalOnly,
    [switch]$RebuildHud
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$web = Join-Path $root 'src/SextaFeira.Web'
$api = Join-Path $root 'src/SextaFeira.WebApi'
$wwwroot = Join-Path $api 'wwwroot/index.html'

function Test-Command($name) { $null -ne (Get-Command $name -ErrorAction SilentlyContinue) }

if (-not (Test-Command 'dotnet')) { throw 'Instale o .NET 10 SDK: https://dotnet.microsoft.com/download' }
if (-not (Test-Command 'ollama')) { Write-Warning 'Ollama não encontrado. Instale em https://ollama.com para o cérebro local.' }

# HUD: instala dependências e compila para wwwroot quando faltar ou quando pedido.
if ($RebuildHud -or -not (Test-Path $wwwroot)) {
    if (-not (Test-Command 'npm')) { throw 'Instale o Node.js (npm) para compilar o HUD: https://nodejs.org' }
    Push-Location $web
    try {
        if (-not (Test-Path 'node_modules')) { npm install }
        npm run build
    } finally { Pop-Location }
}

$args = @()
if ($LocalOnly) { $args += '--ListenOnLan=false' }

# Código 42 = a assistente pediu para reiniciar depois de editar o próprio código (restart_myself).
do {
    Write-Host "`n▶ Iniciando a Sexta-Feira..." -ForegroundColor Cyan
    dotnet run --project $api -c Release -- @args
    $code = $LASTEXITCODE
    if ($code -eq 42) { Write-Host '↻ Reiniciando para aplicar mudanças no código...' -ForegroundColor Yellow }
} while ($code -eq 42)
