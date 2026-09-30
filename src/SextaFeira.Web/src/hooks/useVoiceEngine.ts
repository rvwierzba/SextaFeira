import { useState, useEffect, useRef, useCallback } from 'react';
import { AgentVisualState } from '../components/3d/HologramCoreCanvas';

interface UseVoiceEngineOptions {
  onCommandDetected: (command: string) => void;
  onStateChange: (state: AgentVisualState) => void;
}

export const useVoiceEngine = ({ onCommandDetected, onStateChange }: UseVoiceEngineOptions) => {
  const [isListening, setIsListening] = useState(false);
  const [audioLevel, setAudioLevel] = useState(0.1);
  const recognitionRef = useRef<any>(null);
  const audioContextRef = useRef<AudioContext | null>(null);
  const analyserRef = useRef<AnalyserNode | null>(null);
  const mediaStreamRef = useRef<MediaStream | null>(null);
  const animationFrameRef = useRef<number | null>(null);

  // Inicializar Web Audio API Analyser para reatividade do Holograma
  const startAudioAnalyzer = async () => {
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
      mediaStreamRef.current = stream;

      const audioCtx = new (window.AudioContext || (window as any).webkitAudioContext)();
      audioContextRef.current = audioCtx;

      const source = audioCtx.createMediaStreamSource(stream);
      const analyser = audioCtx.createAnalyser();
      analyser.fftSize = 64;
      source.connect(analyser);
      analyserRef.current = analyser;

      const dataArray = new Uint8Array(analyser.frequencyBinCount);

      const updateAudioLevel = () => {
        if (analyserRef.current) {
          analyserRef.current.getByteFrequencyData(dataArray);
          const average = dataArray.reduce((acc, val) => acc + val, 0) / dataArray.length;
          setAudioLevel(average / 255); // 0.0 to 1.0
        }
        animationFrameRef.current = requestAnimationFrame(updateAudioLevel);
      };

      updateAudioLevel();
    } catch (err) {
      console.warn('Microfone não acessível ou sem permissão. Usando oscilação simulada.', err);
    }
  };

  const stopAudioAnalyzer = () => {
    if (animationFrameRef.current) cancelAnimationFrame(animationFrameRef.current);
    if (mediaStreamRef.current) {
      mediaStreamRef.current.getTracks().forEach((track) => track.stop());
    }
    if (audioContextRef.current) {
      audioContextRef.current.close();
    }
  };

  // Inicializar Reconhecimento de Voz (STT) com Wake-Word
  const startVoiceRecognition = useCallback(() => {
    const SpeechRecognition = (window as any).SpeechRecognition || (window as any).webkitSpeechRecognition;
    if (!SpeechRecognition) {
      console.warn('SpeechRecognition API não suportada neste navegador.');
      return;
    }

    const recognition = new SpeechRecognition();
    recognition.lang = 'pt-BR';
    recognition.continuous = true;
    recognition.interimResults = true;

    recognition.onstart = () => {
      setIsListening(true);
      onStateChange('LISTENING');
    };

    recognition.onresult = (event: any) => {
      const current = event.resultIndex;
      const transcript = event.results[current][0].transcript.toLowerCase();

      // Detecção Multilíngue de Wake-Word ("Ei, Sexta feira", "Hey Friday", "Ei Sexta", "Oye Sexta feira", "Bonjour Friday")
      const wakeWordRegex = /(?:ei|hey|hi|ok|oye|bonjour|ehi)?\s*(?:sexta-feira|sexta\s*feira|friday|sexta)/gi;

      if (wakeWordRegex.test(transcript)) {
        let cleanCommand = transcript.replace(wakeWordRegex, '').trim();
        // Remover vírgulas ou pontuações no início
        cleanCommand = cleanCommand.replace(/^[,\s.-]+/, '');

        if (cleanCommand.length > 2) {
          onCommandDetected(cleanCommand);
        }
      }
    };

    recognition.onerror = (e: any) => {
      console.warn('Erro de reconhecimento de voz:', e);
    };

    recognition.onend = () => {
      if (isListening) {
        try {
          recognition.start();
        } catch { }
      }
    };

    try {
      recognition.start();
      recognitionRef.current = recognition;
      startAudioAnalyzer();
    } catch (err) {
      console.warn('Falha ao iniciar SpeechRecognition:', err);
    }
  }, [isListening, onCommandDetected, onStateChange]);

  const stopVoiceRecognition = useCallback(() => {
    setIsListening(false);
    if (recognitionRef.current) {
      recognitionRef.current.stop();
    }
    stopAudioAnalyzer();
    onStateChange('IDLE');
  }, [onStateChange]);

  // Síntese de Voz (TTS) em Português do Brasil
  const speakResponse = useCallback((text: string) => {
    if (!('speechSynthesis' in window)) return;

    window.speechSynthesis.cancel();
    const utterance = new SpeechSynthesisUtterance(text);
    utterance.lang = 'pt-BR';
    utterance.rate = 1.05;
    utterance.pitch = 1.0;

    const voices = window.speechSynthesis.getVoices();
    const ptVoice = voices.find((v) => v.lang.includes('pt') || v.name.includes('Brazil') || v.name.includes('Portuguese'));
    if (ptVoice) utterance.voice = ptVoice;

    utterance.onstart = () => {
      onStateChange('SPEAKING');
      // Simulação de oscilação do buffer de áudio na saída
      setAudioLevel(0.75);
    };

    utterance.onend = () => {
      onStateChange('IDLE');
      setAudioLevel(0.1);
    };

    window.speechSynthesis.speak(utterance);
  }, [onStateChange]);

  useEffect(() => {
    return () => {
      stopAudioAnalyzer();
    };
  }, []);

  return {
    isListening,
    audioLevel,
    startVoiceRecognition,
    stopVoiceRecognition,
    speakResponse
  };
};
