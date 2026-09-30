import React from 'react';
import { Terminal, Play, RotateCcw, Volume2, Mic, MicOff } from 'lucide-react';
import { AgentVisualState } from '../3d/HologramCoreCanvas';

interface SandboxConsoleProps {
  logs: string[];
  onRunTestCode?: () => void;
  isRunning?: boolean;
}

export const SandboxConsoleWidget: React.FC<SandboxConsoleProps> = ({
  logs,
  onRunTestCode,
  isRunning = false
}) => {
  return (
    <div className="hud-panel" style={{ width: '460px' }}>
      <div className="hud-panel-header">
        <span style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
          <Terminal size={14} color="#00f0ff" /> CODE SANDBOX & AUTO-EVOLUÇÃO (SELF-CODING)
        </span>
        <div style={{ display: 'flex', gap: '6px' }}>
          <button
            onClick={onRunTestCode}
            disabled={isRunning}
            style={{
              background: 'rgba(0, 240, 255, 0.2)',
              border: '1px solid var(--hud-cyan)',
              color: '#fff',
              cursor: 'pointer',
              padding: '2px 8px',
              borderRadius: '3px',
              fontSize: '0.65rem',
              fontFamily: 'var(--hud-font-orbitron)',
              display: 'flex',
              alignItems: 'center',
              gap: '4px'
            }}
          >
            {isRunning ? <RotateCcw size={10} className="hud-pulse-glow" /> : <Play size={10} />}
            {isRunning ? 'EXECUTANDO...' : 'TEST SANDBOX'}
          </button>
        </div>
      </div>

      <div className="hud-console">
        {logs.map((log, index) => (
          <div key={index} style={{ marginBottom: '4px' }}>
            {log}
          </div>
        ))}
        {logs.length === 0 && (
          <div style={{ color: 'rgba(0, 240, 255, 0.4)' }}>
            [DOCKER SANDBOX & AUTO-EVOLUÇÃO] Aguardando pipeline de compilação ou comando de auto-modificação de código...
          </div>
        )}
      </div>
    </div>
  );
};

export const AudioVisualizerWidget: React.FC<{ audioLevel?: number }> = ({ audioLevel = 0.2 }) => {
  const barCount = 36;
  const bars = Array.from({ length: barCount }, (_, i) => {
    // Generate organic wave variation
    const base = Math.sin((i / barCount) * Math.PI) * 100;
    const randomized = base * (0.3 + audioLevel * 0.7) + (Math.random() * 15 * (audioLevel + 0.1));
    return Math.max(8, Math.min(100, randomized));
  });

  return (
    <div style={{ width: '100%', maxWidth: '700px', margin: '0 auto' }}>
      <div className="hud-spectrum-container">
        {bars.map((height, idx) => (
          <div
            key={idx}
            className="hud-spectrum-bar"
            style={{
              height: `${height}%`,
              backgroundColor: idx % 2 === 0 ? '#00f0ff' : '#0077ff',
              boxShadow: height > 60 ? '0 0 8px rgba(0, 240, 255, 0.8)' : 'none'
            }}
          />
        ))}
      </div>
    </div>
  );
};

interface HudStatusBarProps {
  statusText: string;
  agentState: AgentVisualState;
  isListening: boolean;
  onToggleVoice: () => void;
  onSendTextMessage: (text: string) => void;
}

export const HudStatusBar: React.FC<HudStatusBarProps> = ({
  statusText,
  agentState,
  isListening,
  onToggleVoice,
  onSendTextMessage
}) => {
  const [inputText, setInputText] = React.useState('');

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (inputText.trim()) {
      onSendTextMessage(inputText.trim());
      setInputText('');
    }
  };

  const getStateBadgeClass = () => {
    switch (agentState) {
      case 'LISTENING': return 'hud-badge-listening';
      case 'THINKING': return 'hud-badge-thinking';
      case 'SPEAKING': return 'hud-badge-speaking';
      case 'EXECUTING': return 'hud-badge-warning';
      default: return 'hud-badge-active';
    }
  };

  return (
    <div
      style={{
        position: 'absolute',
        bottom: 0,
        left: 0,
        width: '100%',
        padding: '12px 24px',
        background: 'linear-gradient(to top, rgba(2, 6, 16, 0.95), rgba(4, 12, 28, 0.85))',
        borderTop: '1px solid rgba(0, 240, 255, 0.3)',
        backdropFilter: 'blur(12px)',
        zIndex: 50,
        display: 'flex',
        flexDirection: 'column',
        gap: '8px'
      }}
    >
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
          <span className={`hud-badge ${getStateBadgeClass()}`}>
            {agentState}
          </span>
          <span style={{ color: '#fff', fontSize: '0.85rem', fontFamily: 'var(--hud-font-orbitron)', letterSpacing: '1px' }}>
            STATUS: {statusText}
          </span>
        </div>

        <div style={{ color: 'rgba(0, 240, 255, 0.7)', fontSize: '0.75rem', fontFamily: 'var(--hud-font-mono)' }}>
          WAKE-WORD MULTILÍNGUE: "EI, SEXTA-FEIRA" / "HEY FRIDAY"
        </div>
      </div>

      <div style={{ display: 'flex', gap: '10px', alignItems: 'center' }}>
        <button
          onClick={onToggleVoice}
          style={{
            background: isListening ? 'rgba(0, 255, 136, 0.2)' : 'rgba(0, 240, 255, 0.1)',
            border: `1px solid ${isListening ? 'var(--hud-green)' : 'var(--hud-cyan)'}`,
            color: '#fff',
            padding: '8px 16px',
            borderRadius: '4px',
            cursor: 'pointer',
            display: 'flex',
            alignItems: 'center',
            gap: '8px',
            fontFamily: 'var(--hud-font-orbitron)',
            fontSize: '0.75rem'
          }}
        >
          {isListening ? <Mic size={16} color="#00ff88" className="hud-pulse-glow" /> : <MicOff size={16} color="#00f0ff" />}
          {isListening ? 'OUVINDO VOZ (PT-BR)...' : 'FALAR COM SEXTA-FEIRA'}
        </button>

        <form onSubmit={handleSubmit} style={{ flex: 1, display: 'flex', gap: '8px' }}>
          <input
            type="text"
            value={inputText}
            onChange={(e) => setInputText(e.target.value)}
            placeholder="Digite uma diretriz ou comando para a Sexta-Feira (ex: 'Pesquisar restaurantes em Piracaia, SP')..."
            style={{
              flex: 1,
              background: 'rgba(4, 14, 30, 0.8)',
              border: '1px solid rgba(0, 240, 255, 0.3)',
              borderRadius: '4px',
              padding: '8px 14px',
              color: '#d8f4ff',
              fontFamily: 'var(--hud-font-rajdhani)',
              fontSize: '0.9rem',
              outline: 'none'
            }}
          />
          <button
            type="submit"
            style={{
              background: 'rgba(0, 240, 255, 0.2)',
              border: '1px solid var(--hud-cyan)',
              color: '#00f0ff',
              padding: '8px 20px',
              borderRadius: '4px',
              fontFamily: 'var(--hud-font-orbitron)',
              fontSize: '0.75rem',
              cursor: 'pointer',
              fontWeight: 600
            }}
          >
            ENVIAR
          </button>
        </form>
      </div>
    </div>
  );
};
