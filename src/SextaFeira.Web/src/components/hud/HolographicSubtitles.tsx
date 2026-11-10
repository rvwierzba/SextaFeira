import React, { useEffect, useState } from 'react';
import { Sparkles, Terminal, Volume2, Mic } from 'lucide-react';

interface HolographicSubtitlesProps {
  lastUserSpeech?: string;
  lastAiResponse?: string;
  isListening: boolean;
  isSpeaking: boolean;
  isThinking: boolean;
  activeProvider: string;
}

export const HolographicSubtitles: React.FC<HolographicSubtitlesProps> = ({
  lastUserSpeech,
  lastAiResponse,
  isListening,
  isSpeaking,
  isThinking,
  activeProvider
}) => {
  const [displayedAiText, setDisplayedAiText] = useState('');

  // Typewriter stream effect for AI response
  useEffect(() => {
    if (!lastAiResponse) {
      setDisplayedAiText('');
      return;
    }

    let i = 0;
    setDisplayedAiText('');
    const fullText = lastAiResponse;
    const interval = setInterval(() => {
      i++;
      setDisplayedAiText(fullText.slice(0, i));
      if (i >= fullText.length) {
        clearInterval(interval);
      }
    }, 18);

    return () => clearInterval(interval);
  }, [lastAiResponse]);

  const hasContent = isListening || isThinking || isSpeaking || lastUserSpeech || displayedAiText;

  if (!hasContent) {
    return (
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          gap: '8px',
          background: 'rgba(4, 15, 30, 0.45)',
          border: '1px solid rgba(0, 240, 255, 0.15)',
          borderRadius: '20px',
          padding: '6px 16px',
          backdropFilter: 'blur(8px)',
          fontFamily: 'var(--hud-font-rajdhani)',
          fontSize: '0.85rem',
          color: 'rgba(0, 240, 255, 0.6)',
          letterSpacing: '1px',
          boxShadow: '0 4px 20px rgba(0, 0, 0, 0.3)'
        }}
      >
        <span style={{ width: '6px', height: '6px', borderRadius: '50%', background: '#00f0ff', boxShadow: '0 0 8px #00f0ff' }} />
        Diga <strong style={{ color: '#00f0ff', margin: '0 4px' }}>"Sexta-Feira"</strong> ou envie uma mensagem para iniciar
      </div>
    );
  }

  return (
    <div
      style={{
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        maxWidth: '720px',
        width: '90%',
        gap: '8px',
        pointerEvents: 'none'
      }}
    >
      {/* User Input Bubble */}
      {(isListening || lastUserSpeech) && (
        <div
          style={{
            alignSelf: 'center',
            background: 'rgba(6, 24, 48, 0.75)',
            border: '1px solid rgba(0, 240, 255, 0.4)',
            borderRadius: '16px 16px 4px 16px',
            padding: '8px 18px',
            backdropFilter: 'blur(12px)',
            boxShadow: '0 6px 24px rgba(0, 0, 0, 0.4), inset 0 0 10px rgba(0, 240, 255, 0.1)',
            display: 'flex',
            alignItems: 'center',
            gap: '10px',
            animation: 'fadeInUp 0.3s ease'
          }}
        >
          <Mic size={14} color="#00f0ff" style={{ animation: isListening ? 'pulse 1s infinite' : 'none' }} />
          <span
            style={{
              fontFamily: 'var(--hud-font-rajdhani)',
              fontSize: '0.95rem',
              fontWeight: 600,
              color: '#d8f4ff',
              letterSpacing: '0.5px'
            }}
          >
            {isListening ? (
              <span style={{ color: '#00f0ff', fontStyle: 'italic' }}>Ouvindo... Fale seu comando...</span>
            ) : (
              lastUserSpeech
            )}
          </span>
          <span
            style={{
              fontFamily: 'var(--hud-font-mono)',
              fontSize: '0.62rem',
              color: 'rgba(0, 240, 255, 0.5)',
              borderLeft: '1px solid rgba(0, 240, 255, 0.25)',
              paddingLeft: '8px'
            }}
          >
            OPERADOR
          </span>
        </div>
      )}

      {/* Thinking Indicator */}
      {isThinking && (
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: '8px',
            background: 'rgba(255, 170, 0, 0.12)',
            border: '1px solid rgba(255, 170, 0, 0.4)',
            borderRadius: '20px',
            padding: '5px 14px',
            fontFamily: 'var(--hud-font-mono)',
            fontSize: '0.72rem',
            color: '#ffaa00',
            letterSpacing: '1px',
            boxShadow: '0 0 12px rgba(255, 170, 0, 0.2)'
          }}
        >
          <Sparkles size={12} className="animate-spin" />
          <span>SÍNTESE COGNITIVA // PROCESSANDO NO MODELO {activeProvider.toUpperCase()}...</span>
        </div>
      )}

      {/* AI Assistant Holographic Speech Bubble */}
      {displayedAiText && (
        <div
          style={{
            background: 'linear-gradient(135deg, rgba(4, 18, 38, 0.85) 0%, rgba(2, 10, 24, 0.9) 100%)',
            border: '1px solid #00f0ff',
            borderRadius: '4px 18px 18px 18px',
            padding: '12px 22px',
            backdropFilter: 'blur(16px)',
            boxShadow: '0 8px 32px rgba(0, 0, 0, 0.6), 0 0 20px rgba(0, 240, 255, 0.25), inset 0 0 15px rgba(0, 240, 255, 0.08)',
            display: 'flex',
            flexDirection: 'column',
            gap: '6px',
            width: '100%',
            animation: 'fadeInUp 0.3s ease'
          }}
        >
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <span
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: '6px',
                fontFamily: 'var(--hud-font-orbitron)',
                fontSize: '0.72rem',
                fontWeight: 800,
                color: '#00f0ff',
                letterSpacing: '2px',
                textShadow: '0 0 8px rgba(0, 240, 255, 0.8)'
              }}
            >
              <Volume2 size={13} color="#00f0ff" /> SEXTA-FEIRA // RESPOSTA
            </span>
            <span
              style={{
                fontFamily: 'var(--hud-font-mono)',
                fontSize: '0.62rem',
                color: 'rgba(0, 240, 255, 0.7)',
                background: 'rgba(0, 240, 255, 0.1)',
                border: '1px solid rgba(0, 240, 255, 0.3)',
                padding: '2px 6px',
                borderRadius: '3px'
              }}
            >
              {activeProvider}
            </span>
          </div>

          <div
            style={{
              fontFamily: 'var(--hud-font-rajdhani)',
              fontSize: '1.05rem',
              fontWeight: 500,
              lineHeight: 1.45,
              color: '#f0faff',
              letterSpacing: '0.4px',
              textShadow: '0 0 4px rgba(0, 240, 255, 0.2)'
            }}
          >
            {displayedAiText}
            {isSpeaking && <span style={{ color: '#00f0ff', animation: 'blink 0.8s infinite' }}>▌</span>}
          </div>
        </div>
      )}
    </div>
  );
};
