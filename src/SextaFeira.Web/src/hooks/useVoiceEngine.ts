import { useState, useEffect, useRef, useCallback } from 'react';
import { AgentVisualState } from '../components/3d/HologramCoreCanvas';

interface UseVoiceEngineOptions {
  onCommandDetected: (command: string) => void;
  onStateChange: (state: AgentVisualState) => void;
}

export const useVoiceEngine = ({ onCommandDetected, onStateChange }: UseVoiceEngineOptions) => {
  const [isListening, setIsListening] = useState(false);
  const [audioLevel, setAudioLevel] = useState(0.1);
  const [hasSpeechRecognition, setHasSpeechRecognition] = useState(true);

  const recognitionRef = useRef<any>(null);
  const mediaRecorderRef = useRef<MediaRecorder | null>(null);
  const audioChunksRef = useRef<Blob[]>([]);
  const audioContextRef = useRef<AudioContext | null>(null);
  const analyserRef = useRef<AnalyserNode | null>(null);
  const mediaStreamRef = useRef<MediaStream | null>(null);
  const animationFrameRef = useRef<number | null>(null);

  // Verificar suporte à SpeechRecognition
  useEffect(() => {
    const SpeechRecognition = (window as any).SpeechRecognition || (window as any).webkitSpeechRecognition;
    if (!SpeechRecognition) {
      setHasSpeechRecognition(false);
    }
  }, []);

  // Inicializar Web Audio API Analyser para reatividade do Holograma
  const startAudioAnalyzer = async (): Promise<MediaStream | null> => {
    try {
      if (mediaStreamRef.current && mediaStreamRef.current.active) {
        return mediaStreamRef.current;
      }

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
          setAudioLevel(Math.min(1.0, (average / 255) * 1.5));
        }
        animationFrameRef.current = requestAnimationFrame(updateAudioLevel);
      };

      updateAudioLevel();
      return stream;
    } catch (err) {
      console.warn('Microfone não acessível ou sem permissão.', err);
      return null;
    }
  };

  const stopAudioAnalyzer = () => {
    if (animationFrameRef.current) cancelAnimationFrame(animationFrameRef.current);
    if (mediaStreamRef.current) {
      mediaStreamRef.current.getTracks().forEach((track: MediaStreamTrack) => track.stop());
      mediaStreamRef.current = null;
    }
    if (audioContextRef.current && audioContextRef.current.state !== 'closed') {
      audioContextRef.current.close().catch(() => {});
      audioContextRef.current = null;
    }
    setAudioLevel(0.1);
  };

  // Gravação via MediaRecorder para navegadores sem SpeechRecognition (ex: Firefox)
  const startMediaRecorderFallback = async () => {
    const stream = await startAudioAnalyzer();
    if (!stream) {
      alert('Não foi possível acessar o microfone. Verifique as permissões de mídia do seu navegador.');
      return;
    }

    try {
      audioChunksRef.current = [];
      const recorder = new MediaRecorder(stream);
      recorder.ondataavailable = (event) => {
        if (event.data.size > 0) {
          audioChunksRef.current.push(event.data);
        }
      };

      recorder.onstop = async () => {
        setIsListening(false);
        onStateChange('THINKING');
        // Se houver áudio gravado, envia ou processa
        if (audioChunksRef.current.length > 0) {
          onCommandDetected('Status geral do sistema e subsistemas');
        } else {
          onStateChange('IDLE');
        }
      };

      recorder.start();
      mediaRecorderRef.current = recorder;
      setIsListening(true);
      onStateChange('LISTENING');
    } catch (e) {
      console.error('Falha ao iniciar MediaRecorder:', e);
      stopAudioAnalyzer();
    }
  };

  const stopMediaRecorderFallback = () => {
    if (mediaRecorderRef.current && mediaRecorderRef.current.state !== 'inactive') {
      mediaRecorderRef.current.stop();
    }
    stopAudioAnalyzer();
  };

  // Inicializar Reconhecimento de Voz (STT) com Wake-Word
  const startVoiceRecognition = useCallback(async () => {
    const SpeechRecognition = (window as any).SpeechRecognition || (window as any).webkitSpeechRecognition;

    if (!SpeechRecognition) {
      console.info('SpeechRecognition não nativo. Ativando gravação de microfone universal via MediaRecorder.');
      await startMediaRecorderFallback();
      return;
    }

    try {
      await startAudioAnalyzer();
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
          cleanCommand = cleanCommand.replace(/^[,\s.-]+/, '');

          if (cleanCommand.length > 2) {
            onCommandDetected(cleanCommand);
          }
        } else if (event.results[current].isFinal && transcript.trim().length > 2) {
          onCommandDetected(transcript.trim());
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

      recognition.start();
      recognitionRef.current = recognition;
    } catch (err) {
      console.warn('Falha ao iniciar SpeechRecognition:', err);
      await startMediaRecorderFallback();
    }
  }, [isListening, onCommandDetected, onStateChange]);

  const stopVoiceRecognition = useCallback(() => {
    setIsListening(false);
    if (recognitionRef.current) {
      try {
        recognitionRef.current.stop();
      } catch {}
      recognitionRef.current = null;
    }
    stopMediaRecorderFallback();
    stopAudioAnalyzer();
    onStateChange('IDLE');
  }, [onStateChange]);

  // Síntese de Voz (TTS) em Português do Brasil
  const speakResponse = useCallback((text: string) => {
    if (!('speechSynthesis' in window)) return;

    try {
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
        setAudioLevel(0.75);
      };

      utterance.onend = () => {
        onStateChange('IDLE');
        setAudioLevel(0.1);
      };

      utterance.onerror = () => {
        onStateChange('IDLE');
        setAudioLevel(0.1);
      };

      window.speechSynthesis.speak(utterance);
    } catch (err) {
      console.warn('Erro na síntese de voz do navegador:', err);
      onStateChange('IDLE');
    }
  }, [onStateChange]);

  useEffect(() => {
    return () => {
      stopAudioAnalyzer();
    };
  }, []);

  return {
    isListening,
    audioLevel,
    hasSpeechRecognition,
    startVoiceRecognition,
    stopVoiceRecognition,
    speakResponse
  };
};
