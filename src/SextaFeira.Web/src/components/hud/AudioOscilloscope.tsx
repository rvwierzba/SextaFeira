import React, { useEffect, useRef } from 'react';

interface AudioOscilloscopeProps {
  audioLevel: number; // 0.0 to 1.0
  isListening: boolean;
  isSpeaking: boolean;
  width?: number;
  height?: number;
}

export const AudioOscilloscope: React.FC<AudioOscilloscopeProps> = ({
  audioLevel,
  isListening,
  isSpeaking,
  width = 520,
  height = 70
}) => {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const phaseRef = useRef(0);
  const audioLevelRef = useRef(audioLevel);

  useEffect(() => {
    audioLevelRef.current = audioLevel;
  }, [audioLevel]);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    let animId: number;

    const render = () => {
      animId = requestAnimationFrame(render);
      phaseRef.current += 0.04;
      const phase = phaseRef.current;
      const currentLevel = audioLevelRef.current;

      const w = canvas.width;
      const h = canvas.height;
      const centerY = h / 2;

      ctx.clearRect(0, 0, w, h);

      // Baseline glow
      ctx.beginPath();
      ctx.moveTo(0, centerY);
      ctx.lineTo(w, centerY);
      ctx.strokeStyle = 'rgba(0, 240, 255, 0.12)';
      ctx.lineWidth = 1;
      ctx.setLineDash([4, 6]);
      ctx.stroke();
      ctx.setLineDash([]);

      const activeAmp = (isListening || isSpeaking)
        ? Math.max(12, currentLevel * (h * 0.42))
        : 3.5;

      // Draw 3 layers of harmonic waves (electric cyan + arc blue)
      const layers = [
        { color: 'rgba(0, 240, 255, 0.85)', glow: 'rgba(0, 240, 255, 0.6)', width: 2.2, freq: 0.025, speed: 1.0, ampMul: 1.0 },
        { color: 'rgba(0, 119, 255, 0.7)', glow: 'rgba(0, 119, 255, 0.4)', width: 1.6, freq: 0.04, speed: 1.4, ampMul: 0.65 },
        { color: 'rgba(160, 245, 255, 0.9)', glow: 'rgba(0, 240, 255, 0.8)', width: 1.2, freq: 0.015, speed: 0.7, ampMul: 0.4 }
      ];

      layers.forEach((layer) => {
        ctx.beginPath();
        ctx.shadowBlur = (isListening || isSpeaking) ? 14 : 6;
        ctx.shadowColor = layer.glow;
        ctx.strokeStyle = layer.color;
        ctx.lineWidth = layer.width;

        for (let x = 0; x < w; x++) {
          // Windowing envelope to taper edges smoothly
          const envelope = Math.sin((x / w) * Math.PI);
          const y = centerY + Math.sin(x * layer.freq + phase * layer.speed) * activeAmp * layer.ampMul * envelope
            + Math.cos(x * layer.freq * 0.5 + phase * 0.8) * (activeAmp * 0.3) * envelope;

          if (x === 0) {
            ctx.moveTo(x, y);
          } else {
            ctx.lineTo(x, y);
          }
        }
        ctx.stroke();
      });

      // Central Frequency Equalizer mini-bars
      if (isListening || isSpeaking) {
        ctx.shadowBlur = 8;
        ctx.shadowColor = 'rgba(0, 240, 255, 0.8)';
        const barCount = 28;
        const barWidth = 3;
        const barSpacing = 7;
        const totalBarWidth = barCount * (barWidth + barSpacing);
        const startX = (w - totalBarWidth) / 2;

        for (let i = 0; i < barCount; i++) {
          const bx = startX + i * (barWidth + barSpacing);
          const distFromCenter = Math.abs(i - barCount / 2) / (barCount / 2);
          const barHeight = Math.max(3, (1 - distFromCenter * 0.6) * currentLevel * (h * 0.55) * Math.abs(Math.sin(phase * 2 + i * 0.4)));

          const grad = ctx.createLinearGradient(bx, centerY - barHeight / 2, bx, centerY + barHeight / 2);
          grad.addColorStop(0, 'rgba(0, 240, 255, 0.9)');
          grad.addColorStop(0.5, 'rgba(0, 119, 255, 0.95)');
          grad.addColorStop(1, 'rgba(0, 240, 255, 0.9)');

          ctx.fillStyle = grad;
          ctx.fillRect(bx, centerY - barHeight / 2, barWidth, barHeight);
        }
      }

      ctx.shadowBlur = 0;
    };

    render();

    return () => {
      cancelAnimationFrame(animId);
    };
  }, [isListening, isSpeaking]);

  return (
    <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', pointerEvents: 'none' }}>
      <canvas
        ref={canvasRef}
        width={width}
        height={height}
        style={{
          width: `${width}px`,
          height: `${height}px`,
          filter: 'drop-shadow(0 0 10px rgba(0, 240, 255, 0.4))'
        }}
      />
      <div
        style={{
          fontFamily: 'var(--hud-font-mono)',
          fontSize: '0.62rem',
          color: (isListening || isSpeaking) ? '#00f0ff' : 'rgba(0, 240, 255, 0.4)',
          letterSpacing: '3px',
          textTransform: 'uppercase',
          marginTop: '-4px',
          textShadow: (isListening || isSpeaking) ? '0 0 8px rgba(0, 240, 255, 0.7)' : 'none'
        }}
      >
        {isListening ? '● REC // SINAL DE ENTRADA DO OPERADOR ATIVO' : isSpeaking ? '▶ TRANSMITINDO RESPOSTA VOCAL SEXTA-FEIRA' : 'STANDBY // FREQUÊNCIA VOCAL CONTÍNUA'}
      </div>
    </div>
  );
};

