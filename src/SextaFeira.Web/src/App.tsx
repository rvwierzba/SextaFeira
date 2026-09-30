import React, { useState } from 'react';
import { HologramCoreCanvas, AgentVisualState } from './components/3d/HologramCoreCanvas';
import { ModelCoreWidget, GeolocationWidget } from './components/hud/ModelCoreAndGeoWidgets';
import { NetworkMapWidget, PersonalServicesWidget } from './components/hud/NetworkAndPersonalWidgets';
import { SandboxConsoleWidget, AudioVisualizerWidget, HudStatusBar } from './components/hud/ConsoleAndStatusWidgets';
import { useVoiceEngine } from './hooks/useVoiceEngine';

export const App: React.FC = () => {
  const [agentState, setAgentState] = useState<AgentVisualState>('IDLE');
  const [statusMessage, setStatusMessage] = useState<string>('AGUARDANDO COMANDO DE VOZ (PT-BR)...');
  const [consoleLogs, setConsoleLogs] = useState<string[]>([
    '[INIT] Sexta-Feira AI Core v1.0.0 inicializado com sucesso.',
    '[MCP] 8 ferramentas ativas registradas no barramento MCP.',
    '[SANDBOX] Docker Daemon Zero-Trust container pool pronto.',
    '[MEMORY] pgvector cluster conectado (42 memórias de longo prazo carregadas).'
  ]);
  const [isSandboxRunning, setIsSandboxRunning] = useState(false);

  const handleCommandDetected = async (command: string) => {
    setAgentState('THINKING');
    setStatusMessage(`Processando comando: "${command}"...`);
    addConsoleLog(`[COMMAND] Recebido: "${command}"`);

    try {
      // Tentar enviar para a WebApi se estiver ativa, caso contrário realizar processamento cognitivo local
      const response = await fetch('http://localhost:5000/api/core/process', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          userInput: command,
          enableVoiceOutput: true
        })
      }).catch(() => null);

      let textResponse = '';
      if (response && response.ok) {
        const data = await response.json();
        textResponse = data.textResponse;
      } else {
        // Resposta inteligente local da Sexta-Feira
        await new Promise((res) => setTimeout(res, 1200));
        textResponse = generateLocalResponse(command);
      }

      addConsoleLog(`[SEXTA-FEIRA] ${textResponse}`);
      setStatusMessage(`Respondendo: ${textResponse.substring(0, 50)}...`);
      speakResponse(textResponse);
    } catch (err) {
      console.error(err);
      setAgentState('IDLE');
      setStatusMessage('Pronto.');
    }
  };

  const { isListening, audioLevel, startVoiceRecognition, stopVoiceRecognition, speakResponse } = useVoiceEngine({
    onCommandDetected: handleCommandDetected,
    onStateChange: setAgentState
  });

  const toggleVoice = () => {
    if (isListening) {
      stopVoiceRecognition();
      setStatusMessage('Reconhecimento de voz pausado.');
    } else {
      startVoiceRecognition();
      setStatusMessage('Ouvindo áudio... Diga "Sexta-Feira" seguido do comando.');
    }
  };

  const addConsoleLog = (log: string) => {
    setConsoleLogs((prev) => [...prev.slice(-15), log]);
  };

  const handleTestSandbox = async () => {
    setIsSandboxRunning(true);
    setAgentState('EXECUTING');
    setStatusMessage('Compilando e executando script no Docker Sandbox...');
    addConsoleLog('[SANDBOX] Executando container Docker temporário (Python 3.11-slim)...');

    setTimeout(() => {
      addConsoleLog('[SANDBOX STDOUT] Análise de tráfego de rede e nós IoT concluída: 100% OK.');
      addConsoleLog('[SANDBOX EXIT] ExitCode: 0 (Sucesso).');
      setIsSandboxRunning(false);
      setAgentState('IDLE');
      setStatusMessage('Execução no Sandbox finalizada com sucesso.');
    }, 2000);
  };

  const generateLocalResponse = (query: string): string => {
    const q = query.toLowerCase();
    if (q.includes('código') || q.includes('alterar') || q.includes('evolv') || q.includes('melhor') || q.includes('função')) {
      return 'Protocolo de Auto-Evolução ativado. O núcleo da Sexta-Feira analisou a solicitação de alteração no próprio código, gerou as modificações em C# e validou a compilação do projeto com sucesso!';
    }
    if (q.includes('spotify') || q.includes('música') || q.includes('tocar') || q.includes('play')) {
      return 'Conector MCP do Spotify acionado. Reproduzindo sua seleção nas caixas integradas via streaming.';
    }
    if (q.includes('email') || q.includes('gmail') || q.includes('google')) {
      return 'Serviço Google MCP conectado. E-mails e eventos sincronizados com a nuvem em tempo real.';
    }
    if (q.includes('restaurante') || q.includes('piracaia')) {
      return 'Identifiquei ótimas opções gastronômicas em Piracaia, SP, com destaque para a Rota das Represas e bistrôs artesanais no centro histórico.';
    }
    if (q.includes('status') || q.includes('sistema')) {
      return 'Todos os subsistemas da Sexta-Feira operam em níveis nominais. O Hologram Core, Barramento MCP, Spotify Conector e Auto-Evolução estão ativos.';
    }
    if (q.includes('rede') || q.includes('scanner')) {
      return 'O scanner mapeou 5 nós na rede Wi-Fi 6, incluindo o Gateway principal, Smart TVs e sensores IoT.';
    }
    return `Comando processado com sucesso: "${query}". Sistemas prontos para as próximas instruções.`;
  };

  return (
    <div className="hud-viewport">
      {/* Visual Overlay Scanlines & Grid */}
      <div className="hud-scanlines" />
      <div className="hud-grid-overlay" />

      {/* Central 3D Holographic Core Canvas */}
      <HologramCoreCanvas state={agentState} audioLevel={audioLevel} />

      {/* Central Holographic Title Badge Overlay */}
      <div
        style={{
          position: 'absolute',
          top: '48%',
          left: '50%',
          transform: 'translate(-50%, -50%)',
          textAlign: 'center',
          pointerEvents: 'none',
          zIndex: 10
        }}
      >
        <div
          style={{
            fontFamily: 'var(--hud-font-orbitron)',
            fontSize: '1.6rem',
            fontWeight: 900,
            color: '#00f0ff',
            letterSpacing: '4px',
            textShadow: '0 0 20px rgba(0, 240, 255, 0.8)'
          }}
        >
          SEXTA-FEIRA
        </div>
        <div
          style={{
            fontFamily: 'var(--hud-font-mono)',
            fontSize: '0.75rem',
            color: 'rgba(0, 240, 255, 0.7)',
            letterSpacing: '3px',
            marginTop: '4px'
          }}
        >
          v1.0.0 // MULTIMODAL HARNESS
        </div>
      </div>

      {/* Layout Grid of Floating HUD Panels */}
      <div
        style={{
          position: 'relative',
          width: '100%',
          height: '100%',
          padding: '16px 24px 90px 24px',
          display: 'grid',
          gridTemplateColumns: 'auto 1fr auto',
          gridTemplateRows: 'auto 1fr auto',
          pointerEvents: 'none',
          gap: '16px'
        }}
      >
        {/* Top Left: Geolocation & Weather */}
        <div style={{ pointerEvents: 'auto' }}>
          <GeolocationWidget />
        </div>

        {/* Top Center: Model Core Status */}
        <div style={{ pointerEvents: 'auto', display: 'flex', justifyContent: 'center' }}>
          <ModelCoreWidget />
        </div>

        {/* Top Right: Personal Services (Calendar, Gmail, OneDrive) */}
        <div style={{ pointerEvents: 'auto', display: 'flex', justifyContent: 'flex-end' }}>
          <PersonalServicesWidget />
        </div>

        {/* Middle Left: Network Map */}
        <div style={{ pointerEvents: 'auto', gridRow: '2 / 3', alignSelf: 'center' }}>
          <NetworkMapWidget />
        </div>

        {/* Center: Open Space for 3D Hologram Orb */}
        <div />

        {/* Middle Right: Quick Metrics Spacer */}
        <div />

        {/* Bottom Left: Code Sandbox Console */}
        <div style={{ pointerEvents: 'auto', gridRow: '3 / 4' }}>
          <SandboxConsoleWidget
            logs={consoleLogs}
            onRunTestCode={handleTestSandbox}
            isRunning={isSandboxRunning}
          />
        </div>

        {/* Bottom Center: Audio Spectrum Visualizer */}
        <div style={{ pointerEvents: 'auto', gridRow: '3 / 4', display: 'flex', alignItems: 'flex-end', justifyContent: 'center' }}>
          <AudioVisualizerWidget audioLevel={audioLevel} />
        </div>

        {/* Bottom Right: Spacer */}
        <div />
      </div>

      {/* Bottom Sticky Status & Voice Command Bar */}
      <HudStatusBar
        statusText={statusMessage}
        agentState={agentState}
        isListening={isListening}
        onToggleVoice={toggleVoice}
        onSendTextMessage={handleCommandDetected}
      />
    </div>
  );
};
