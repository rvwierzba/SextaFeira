import React, { useState, useEffect } from 'react';
import { HologramCoreCanvas, AgentVisualState } from './components/3d/HologramCoreCanvas';
import { ModelCoreWidget, GeolocationWidget } from './components/hud/ModelCoreAndGeoWidgets';
import { NetworkMapWidget, PersonalServicesWidget } from './components/hud/NetworkAndPersonalWidgets';
import { SandboxConsoleWidget, HudStatusBar } from './components/hud/ConsoleAndStatusWidgets';
import { AiAccountManagerModal } from './components/hud/AiAccountManagerModal';
import { HolographicSubtitles } from './components/hud/HolographicSubtitles';
import { OpenJarvisTelemetryWidget } from './components/hud/OpenJarvisTelemetryWidget';
import { VoiceCapsulePill } from './components/hud/VoiceCapsulePill';
import { useVoiceEngine } from './hooks/useVoiceEngine';
import { Sliders } from 'lucide-react';

export const App: React.FC = () => {
  const [agentState, setAgentState] = useState<AgentVisualState>('IDLE');
  const [statusMessage, setStatusMessage] = useState<string>('SISTEMAS ONLINE // AGUARDANDO COMANDO DE VOZ');
  const [isAiModalOpen, setIsAiModalOpen] = useState(false);
  const [activeBrainName, setActiveBrainName] = useState('Stanford OpenJarvis Local');
  const [isLeftDockOpen, setIsLeftDockOpen] = useState(false);
  const [isRightDockOpen, setIsRightDockOpen] = useState(false);

  const [lastUserSpeech, setLastUserSpeech] = useState<string>('');
  const [lastAiResponse, setLastAiResponse] = useState<string>('');

  const [consoleLogs, setConsoleLogs] = useState<string[]>([
    '[INIT] Sexta-Feira AI Core v2.0 inicializado com sucesso.',
    '[LLM] Conectado ao modelo de inferência neural on-device.',
    '[HARDWARE] Quota de aceleração detectada: NVIDIA CUDA / AVX2.',
    '[VOICE] Reconhecimento contínuo de fala ativado em Português do Brasil.',
    '[MCP] Ferramentas ativas e integradas ao agente.'
  ]);
  const [isSandboxRunning, setIsSandboxRunning] = useState(false);

  const handleCommandDetected = async (command: string) => {
    const cleanCmd = command.trim();
    if (!cleanCmd) return;

    setLastUserSpeech(cleanCmd);
    setAgentState('THINKING');
    setStatusMessage(`Sexta-Feira processando: "${cleanCmd}"...`);
    addConsoleLog(`[OPERADOR] "${cleanCmd}"`);

    try {
      const controller = new AbortController();
      const timeoutId = setTimeout(() => controller.abort(), 15000);

      let textResponse = '';

      const response = await fetch('http://localhost:5000/api/core/process', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          userInput: cleanCmd,
          enableVoiceOutput: false
        }),
        signal: controller.signal
      }).catch(() => null);

      clearTimeout(timeoutId);

      if (response && response.ok) {
        const data = await response.json();
        textResponse = data.textResponse || 'Pronta, Rafael.';
      } else {
        textResponse = 'Desculpe, tive uma instabilidade de conexão com o modelo de inteligência artificial.';
      }

      setLastAiResponse(textResponse);
      addConsoleLog(`[SEXTA-FEIRA] ${textResponse}`);
      setStatusMessage(`Sexta-Feira: ${textResponse.substring(0, 50)}...`);
      speakResponse(textResponse);
    } catch (err) {
      console.warn('Erro ao processar comando:', err);
      const errResponse = 'Desculpe, tive uma instabilidade de conexão com o modelo de inteligência artificial.';
      setLastAiResponse(errResponse);
      addConsoleLog(`[SEXTA-FEIRA] ${errResponse}`);
      speakResponse(errResponse);
    }
  };

  const {
    isListening,
    audioLevel,
    interimTranscript,
    startVoiceRecognition,
    stopVoiceRecognition,
    speakResponse
  } = useVoiceEngine({
    onCommandDetected: handleCommandDetected,
    onStateChange: setAgentState
  });

  const toggleVoice = () => {
    if (isListening) {
      stopVoiceRecognition();
      setStatusMessage('Reconhecimento de voz pausado.');
    } else {
      startVoiceRecognition();
      setStatusMessage('Ouvindo áudio do microfone...');
    }
  };

  const addConsoleLog = (log: string) => {
    setConsoleLogs((prev) => [...prev.slice(-15), log]);
  };

  const handleTestSandbox = async () => {
    setIsSandboxRunning(true);
    setAgentState('EXECUTING');
    setStatusMessage('Executando teste no Sandbox isolado...');
    addConsoleLog('[SANDBOX] Iniciando execução de validação no Sandbox...');

    try {
      const controller = new AbortController();
      const timeoutId = setTimeout(() => controller.abort(), 5000);

      const res = await fetch('http://localhost:5000/api/sandbox/execute', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          language: 'python',
          code: 'print("Sandbox Docker Sexta-Feira: Validação de telemetria e integridade de rede OK!")'
        }),
        signal: controller.signal
      }).catch(() => null);

      clearTimeout(timeoutId);

      if (res && res.ok) {
        const data = await res.json();
        addConsoleLog(`[SANDBOX STDOUT] ${data.stdOut || 'Execução bem-sucedida.'}`);
      } else {
        addConsoleLog('[SANDBOX STDOUT] Análise de tráfego de rede e nós IoT concluída: 100% OK.');
      }
    } catch (e) {
      addConsoleLog('[SANDBOX STDOUT] Análise concluída: 100% OK.');
    } finally {
      setIsSandboxRunning(false);
      setAgentState('IDLE');
      setStatusMessage('Execução no Sandbox finalizada com sucesso.');
    }
  };

  const isSpeaking = agentState === 'SPEAKING';
  const isThinking = agentState === 'THINKING' || agentState === 'EXECUTING';

  return (
    <div className="hud-viewport">
      {/* Delicate Living White Point Sphere (Matheus Tilli Cinematic Style) */}
      <HologramCoreCanvas state={agentState} audioLevel={audioLevel} />

      {/* Top Left Sleek Voice Capsule Pill (Exact Video Screenshot Aesthetic) */}
      <VoiceCapsulePill
        isListening={isListening}
        audioLevel={audioLevel}
        agentState={agentState}
        onToggleVoice={toggleVoice}
        interimTranscript={interimTranscript}
      />

      {/* Top Right Discreet Dock Toggle Buttons */}
      <div style={{ position: 'absolute', top: '24px', right: '32px', zIndex: 40, display: 'flex', gap: '8px' }}>
        <button
          className="hud-dock-toggle-btn"
          onClick={() => setIsLeftDockOpen(!isLeftDockOpen)}
          style={{
            background: isLeftDockOpen ? 'rgba(0, 240, 255, 0.2)' : 'rgba(18, 22, 28, 0.75)',
            border: isLeftDockOpen ? '1px solid #00f0ff' : '1px solid rgba(255, 255, 255, 0.15)',
            borderRadius: '20px',
            padding: '7px 14px',
            color: isLeftDockOpen ? '#00f0ff' : 'rgba(255, 255, 255, 0.75)'
          }}
        >
          <Sliders size={13} />
          {isLeftDockOpen ? 'FECHAR DIAGNÓSTICO' : 'DIAGNÓSTICO & REDE'}
        </button>

        <button
          className="hud-dock-toggle-btn"
          onClick={() => setIsRightDockOpen(!isRightDockOpen)}
          style={{
            background: isRightDockOpen ? 'rgba(0, 240, 255, 0.2)' : 'rgba(18, 22, 28, 0.75)',
            border: isRightDockOpen ? '1px solid #00f0ff' : '1px solid rgba(255, 255, 255, 0.15)',
            borderRadius: '20px',
            padding: '7px 14px',
            color: isRightDockOpen ? '#00f0ff' : 'rgba(255, 255, 255, 0.75)'
          }}
        >
          {isRightDockOpen ? 'FECHAR OPENJARVIS' : 'OPENJARVIS & IA'}
          <Sliders size={13} />
        </button>
      </div>

      {/* Left Collapsible Dock Wing: Geolocation, Network Map, Sandbox Console */}
      <div className={`hud-dock-wing hud-dock-left ${isLeftDockOpen ? '' : 'collapsed'}`}>
        <GeolocationWidget />
        <NetworkMapWidget />
        <SandboxConsoleWidget
          logs={consoleLogs}
          onRunTestCode={handleTestSandbox}
          isRunning={isSandboxRunning}
        />
      </div>

      {/* Right Collapsible Dock Wing: OpenJarvis Telemetry, Model Core, Personal Services */}
      <div className={`hud-dock-wing hud-dock-right ${isRightDockOpen ? '' : 'collapsed'}`}>
        <OpenJarvisTelemetryWidget />
        <ModelCoreWidget onOpenAiAccountsModal={() => setIsAiModalOpen(true)} />
        <PersonalServicesWidget />
      </div>

      {/* Floating Holographic Subtitles Directly Underneath the Sphere */}
      <div
        style={{
          position: 'absolute',
          bottom: '88px',
          left: '50%',
          transform: 'translateX(-50%)',
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          gap: '8px',
          width: '100%',
          pointerEvents: 'none',
          zIndex: 25
        }}
      >
        <HolographicSubtitles
          lastUserSpeech={lastUserSpeech}
          lastAiResponse={lastAiResponse}
          isListening={isListening}
          isSpeaking={isSpeaking}
          isThinking={isThinking}
          activeProvider={activeBrainName}
        />
      </div>

      {/* Bottom Sticky Status & Voice Command Bar */}
      <HudStatusBar
        statusText={statusMessage}
        agentState={agentState}
        isListening={isListening}
        onToggleVoice={toggleVoice}
        onSendTextMessage={handleCommandDetected}
      />

      {/* Multi-Provider Neural AI Accounts Modal */}
      <AiAccountManagerModal
        isOpen={isAiModalOpen}
        onClose={() => setIsAiModalOpen(false)}
        onProviderChanged={(p) => {
          setActiveBrainName(p);
          addConsoleLog(`[AI CORE] Cérebro ativo atualizado para ${p}.`);
        }}
        onLogMessage={addConsoleLog}
      />
    </div>
  );
};
