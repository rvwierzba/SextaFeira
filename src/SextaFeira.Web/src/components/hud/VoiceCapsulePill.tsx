import React from 'react';
import { Mic, MicOff, Volume2 } from 'lucide-react';
import { AgentVisualState } from '../3d/HologramCoreCanvas';

interface VoiceCapsulePillProps {
  isListening: boolean;
  audioLevel: number;
  agentState: AgentVisualState;
  onToggleVoice: () => void;
  interimTranscript?: string;
}

export const VoiceCapsulePill: React.FC<VoiceCapsulePillProps> = ({
  isListening,
  audioLevel,
  agentState,
  onToggleVoice,
  interimTranscript
}) => {
  const barCount = 18;
  const isSpeaking = agentState === 'SPEAKING';
  const isThinking = agentState === 'THINKING' || agentState === 'EXECUTING';

  return (
    <div
      style={{
        position: 'absolute',
        top: '24px',
        left: '32px',
        zIndex: 40,
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'flex-start',
        gap: '6px'
      }}
    >
      {/* Sleek Top Pill Capsule (Matching Matheus Tilli Video Screenshot) */}
      <div
        onClick={onToggleVoice}
        style={{
          display: 'flex',
          alignItems: 'center',
          gap: '12px',
          background: 'rgba(18, 22, 28, 0.75)',
          border: '1px solid rgba(255, 255, 255, 0.12)',
          borderRadius: '30px',
          padding: '6px 16px 6px 8px',
          backdropFilter: 'blur(16px)',
          boxShadow: isListening
            ? '0 8px 32px rgba(0, 0, 0, 0.6), 0 0 16px rgba(0, 240, 255, 0.35)'
            : '0 8px 32px rgba(0, 0, 0, 0.5)',
          cursor: 'pointer',
          transition: 'all 0.25s ease',
          pointerEvents: 'auto'
        }}
        title={isListening ? 'Clique para pausar voz' : 'Clique para ativar microfone'}
      >
        {/* Mic Circle Icon */}
        <div
          style={{
            width: '32px',
            height: '32px',
            borderRadius: '50%',
            background: isListening
              ? 'linear-gradient(135deg, #00f0ff 0%, #0077ff 100%)'
              : 'rgba(255, 255, 255, 0.1)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            boxShadow: isListening ? '0 0 12px rgba(0, 240, 255, 0.6)' : 'none',
            transition: 'all 0.2s ease'
          }}
        >
          {isSpeaking ? (
            <Volume2 size={16} color="#ffffff" />
          ) : isListening ? (
            <Mic size={16} color="#040814" />
          ) : (
            <MicOff size={16} color="rgba(255, 255, 255, 0.6)" />
          )}
        </div>

        {/* Dynamic Waveform Bars (like in screenshot) */}
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: '3px',
            height: '24px'
          }}
        >
          {Array.from({ length: barCount }).map((_, idx) => {
            const distFromCenter = Math.abs(idx - barCount / 2) / (barCount / 2);
            let height = 3;

            if (isListening || isSpeaking) {
              const wave = Math.sin((idx / barCount) * Math.PI);
              const variance = Math.sin(Date.now() * 0.008 + idx * 0.6) * 0.35 + 0.65;
              height = Math.max(
                3,
                Math.min(22, (audioLevel * 28 + 3) * wave * variance * (1.1 - distFromCenter * 0.4))
              );
            } else if (isThinking) {
              height = Math.max(3, Math.sin(Date.now() * 0.01 + idx * 0.5) * 8 + 10);
            }

            return (
              <span
                key={idx}
                style={{
                  width: '3px',
                  height: `${height}px`,
                  borderRadius: '2px',
                  background: isListening
                    ? 'linear-gradient(to top, #0077ff, #00f0ff)'
                    : isThinking
                    ? '#ffaa00'
                    : 'rgba(255, 255, 255, 0.25)',
                  transition: 'height 0.08s ease',
                  boxShadow: isListening ? '0 0 4px rgba(0, 240, 255, 0.4)' : 'none'
                }}
              />
            );
          })}
        </div>

        {/* Status Text inside pill */}
        <span
          style={{
            fontFamily: 'var(--hud-font-rajdhani)',
            fontSize: '0.8rem',
            fontWeight: 600,
            color: isListening ? '#00f0ff' : 'rgba(255, 255, 255, 0.45)',
            letterSpacing: '1px',
            textTransform: 'uppercase',
            marginLeft: '4px'
          }}
        >
          {isSpeaking ? 'FALANDO' : isListening ? 'OUVINDO' : isThinking ? 'PENSANDO...' : 'MIC OFF'}
        </span>
      </div>

      {/* Live Interim Speech Bubble (Shows immediate feedback when user is speaking!) */}
      {interimTranscript && (
        <div
          style={{
            background: 'rgba(10, 15, 25, 0.85)',
            border: '1px solid rgba(0, 240, 255, 0.3)',
            borderRadius: '12px',
            padding: '4px 12px',
            backdropFilter: 'blur(10px)',
            fontFamily: 'var(--hud-font-rajdhani)',
            fontSize: '0.82rem',
            color: '#00f0ff',
            letterSpacing: '0.5px',
            boxShadow: '0 4px 16px rgba(0, 0, 0, 0.4)'
          }}
        >
          <span style={{ color: 'rgba(255, 255, 255, 0.5)', marginRight: '6px' }}>Ouvindo:</span>
          "{interimTranscript}"
        </div>
      )}
    </div>
  );
};
