import React, { useEffect, useRef } from 'react';
import { Mic, MicOff, Send, Square, Wrench, EarOff, AlertTriangle, User, Sparkles } from 'lucide-react';
import { AgentVisualState } from '../3d/HologramCoreCanvas';
import type { LogEntry } from '../../hooks/useAssistant';

/** Histórico real da conversa: falas, ferramentas usadas e falas ignoradas (sem palavra de ativação). */
export const ConversationLog: React.FC<{ entries: LogEntry[]; onClear: () => void }> = ({ entries, onClear }) => {
  // Rola só a lista (scrollIntoView rolaria a página inteira e deslocaria o HUD para o lado).
  const listRef = useRef<HTMLDivElement>(null);
  useEffect(() => { const el = listRef.current; if (el) el.scrollTop = el.scrollHeight; }, [entries]);

  const icon = (e: LogEntry) => {
    switch (e.kind) {
      case 'user': return <User size={12} color="#00ff88" />;
      case 'assistant': return <Sparkles size={12} color="#00f0ff" />;
      case 'tool': return <Wrench size={12} color={e.ok ? '#ffd27f' : '#ff5577'} />;
      case 'ignored': return <EarOff size={12} color="#55708a" />;
      default: return <AlertTriangle size={12} color="#ffd27f" />;
    }
  };

  return (
    <div className="hud-panel" style={{ padding: 12, display: 'flex', flexDirection: 'column', gap: 8, maxHeight: '70vh' }}>
      <div className="hud-panel-header" style={{ display: 'flex', justifyContent: 'space-between' }}>
        <span>CONVERSA</span>
        <button onClick={onClear} style={{ background: 'none', border: 'none', color: '#7fa8c0', cursor: 'pointer', fontSize: '0.7rem' }}>LIMPAR</button>
      </div>
      <div ref={listRef} className="hud-console" style={{ overflowY: 'auto', display: 'flex', flexDirection: 'column', gap: 6, fontFamily: 'var(--hud-font-rajdhani)', fontSize: '0.9rem' }}>
        {entries.length === 0 && <div style={{ color: '#55708a' }}>Nada por aqui ainda.</div>}
        {entries.map(e => (
          <div key={e.id} style={{ display: 'flex', gap: 8, opacity: e.kind === 'ignored' ? 0.55 : 1 }}>
            <span style={{ marginTop: 3 }}>{icon(e)}</span>
            <div style={{ minWidth: 0 }}>
              <div style={{ color: e.kind === 'assistant' ? '#d8f4ff' : e.kind === 'user' ? '#c8ffe4' : '#a9c4d6', wordBreak: 'break-word' }}>
                {e.kind === 'tool' ? <code>{e.text}</code> : e.text}
              </div>
              {(e.detail || e.deviceName) && (
                <div style={{ fontSize: '0.72rem', color: '#55708a', wordBreak: 'break-word' }}>
                  {[e.deviceName, e.kind === 'ignored' ? `ignorado: ${e.detail}` : e.detail].filter(Boolean).join(' · ')}
                </div>
              )}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
};

interface HudStatusBarProps {
  statusText: string;
  agentState: AgentVisualState;
  micOn: boolean;
  connection: 'connecting' | 'online' | 'offline';
  latency?: { engine: string; model: string; ttftMs: number; totalMs: number } | null;
  onToggleVoice: () => void;
  onSendTextMessage: (text: string) => void;
  onInterrupt: () => void;
}

export const HudStatusBar: React.FC<HudStatusBarProps> = ({
  statusText, agentState, micOn, connection, latency, onToggleVoice, onSendTextMessage, onInterrupt,
}) => {
  const [inputText, setInputText] = React.useState('');
  const busy = agentState === 'THINKING' || agentState === 'EXECUTING' || agentState === 'SPEAKING';

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (inputText.trim()) {
      onSendTextMessage(inputText.trim());
      setInputText('');
    }
  };

  const badgeClass = {
    LISTENING: 'hud-badge-listening', THINKING: 'hud-badge-thinking', SPEAKING: 'hud-badge-speaking',
    EXECUTING: 'hud-badge-warning', IDLE: 'hud-badge-active',
  }[agentState];
  const stateLabel = { LISTENING: 'OUVINDO', THINKING: 'PENSANDO', SPEAKING: 'FALANDO', EXECUTING: 'EXECUTANDO', IDLE: 'PRONTA' }[agentState];

  return (
    <div className="hud-status-bar" style={{
      position: 'absolute', bottom: 0, left: 0, width: '100%', boxSizing: 'border-box', padding: '10px 16px',
      background: 'linear-gradient(to top, rgba(2, 6, 16, 0.95), rgba(4, 12, 28, 0.85))',
      borderTop: '1px solid rgba(0, 240, 255, 0.3)', backdropFilter: 'blur(12px)', zIndex: 50,
      display: 'flex', flexDirection: 'column', gap: 8,
    }}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 12, flexWrap: 'wrap' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10, minWidth: 0 }}>
          <span className={`hud-badge ${connection === 'online' ? badgeClass : 'hud-badge-offline'}`}>
            {connection === 'online' ? stateLabel : connection === 'connecting' ? 'CONECTANDO' : 'OFFLINE'}
          </span>
          <span style={{ color: '#fff', fontSize: '0.8rem', fontFamily: 'var(--hud-font-rajdhani)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
            {statusText}
          </span>
        </div>
        {latency && (
          <span style={{ color: 'rgba(0, 240, 255, 0.7)', fontSize: '0.7rem', fontFamily: 'var(--hud-font-mono)' }}>
            {latency.engine}/{latency.model} · 1ª palavra {(latency.ttftMs / 1000).toFixed(1)}s · total {(latency.totalMs / 1000).toFixed(1)}s
          </span>
        )}
      </div>

      <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
        <button onClick={onToggleVoice} title={micOn ? 'Desligar microfone' : 'Ligar microfone'} style={{
          background: micOn ? 'rgba(0, 255, 136, 0.2)' : 'rgba(0, 240, 255, 0.1)',
          border: `1px solid ${micOn ? 'var(--hud-green)' : 'var(--hud-cyan)'}`, color: '#fff', padding: '8px 12px', borderRadius: 4,
          cursor: 'pointer', display: 'flex', alignItems: 'center', gap: 6, fontFamily: 'var(--hud-font-orbitron)', fontSize: '0.7rem', whiteSpace: 'nowrap',
        }}>
          {micOn ? <Mic size={16} color="#00ff88" className="hud-pulse-glow" /> : <MicOff size={16} color="#00f0ff" />}
          <span className="hud-hide-mobile">{micOn ? 'MICROFONE LIGADO' : 'LIGAR MICROFONE'}</span>
        </button>

        {busy && (
          <button onClick={onInterrupt} title="Parar" style={{
            background: 'rgba(255, 60, 90, 0.15)', border: '1px solid #ff5577', color: '#ff99aa', padding: '8px 10px', borderRadius: 4, cursor: 'pointer',
          }}><Square size={14} /></button>
        )}

        <form onSubmit={handleSubmit} style={{ flex: 1, display: 'flex', gap: 8, minWidth: 0 }}>
          <input type="text" value={inputText} onChange={e => setInputText(e.target.value)} placeholder="Digite para a Sexta-Feira..."
            style={{
              flex: 1, minWidth: 0, background: 'rgba(4, 14, 30, 0.8)', border: '1px solid rgba(0, 240, 255, 0.3)', borderRadius: 4,
              padding: '8px 12px', color: '#d8f4ff', fontFamily: 'var(--hud-font-rajdhani)', fontSize: '0.95rem', outline: 'none',
            }} />
          <button type="submit" title="Enviar" style={{
            background: 'rgba(0, 240, 255, 0.2)', border: '1px solid var(--hud-cyan)', color: '#00f0ff', padding: '8px 12px', borderRadius: 4, cursor: 'pointer',
          }}><Send size={14} /></button>
        </form>
      </div>
    </div>
  );
};
