import { useState, useEffect, useRef, useCallback } from 'react';
import { AgentVisualState } from '../components/3d/HologramCoreCanvas';

interface UseVoiceEngineOptions {
  onCommandDetected: (command: string) => void;
  onStateChange: (state: AgentVisualState) => void;
}

export const useVoiceEngine = ({ onCommandDetected, onStateChange }: UseVoiceEngineOptions) => {
  const [isListening, setIsListening] = useState(false);
  const [audioLevel, setAudioLevel] = useState(0.05);
  const [hasSpeechRecognition, setHasSpeechRecognition] = useState(true);
  const [interimTranscript, setInterimTranscript] = useState('');

  const recognitionRef = useRef<any>(null);
  const isListeningRef = useRef(false);
  const audioContextRef = useRef<AudioContext | null>(null);
  const analyserRef = useRef<AnalyserNode | null>(null);
  const mediaStreamRef = useRef<MediaStream | null>(null);
  const animationFrameRef = useRef<number | null>(null);
  const silenceTimerRef = useRef<any>(null);
  const accumulatedTranscriptRef = useRef<string>('');
  const currentUtteranceRef = useRef<SpeechSynthesisUtterance | null>(null);
  const voicesRef = useRef<SpeechSynthesisVoice[]>([]);

  // Carregar e armazenar vozes do navegador
  useEffect(() => {
    if ('speechSynthesis' in window) {
      const updateVoices = () => {
        voicesRef.current = window.speechSynthesis.getVoices();
      };
      updateVoices();
      window.speechSynthesis.onvoiceschanged = updateVoices;
    }
  }, []);

  // Verificar suporte à Web Speech API
  useEffect(() => {
    const SpeechRecognition = (window as any).SpeechRecognition || (window as any).webkitSpeechRecognition;
    if (!SpeechRecognition) {
      setHasSpeechRecognition(false);
    }
  }, []);

  // Analisador Web Audio API para reatividade dos pontos 3D e do osciloscópio
  const startAudioAnalyzer = async (): Promise<MediaStream | null> => {
    try {
      if (mediaStreamRef.current && mediaStreamRef.current.active) {
        return mediaStreamRef.current;
      }

      const stream = await navigator.mediaDevices.getUserMedia({
        audio: {
          echoCancellation: true,
          noiseSuppression: true,
          autoGainControl: true
        }
      });
      mediaStreamRef.current = stream;

      const AudioCtx = window.AudioContext || (window as any).webkitAudioContext;
      const audioCtx = new AudioCtx();
      audioContextRef.current = audioCtx;

      if (audioCtx.state === 'suspended') {
        await audioCtx.resume();
      }

      const source = audioCtx.createMediaStreamSource(stream);
      const analyser = audioCtx.createAnalyser();
      analyser.fftSize = 64;
      analyser.smoothingTimeConstant = 0.4;
      source.connect(analyser);
      analyserRef.current = analyser;

      const dataArray = new Uint8Array(analyser.frequencyBinCount);

      const updateAudioLevel = () => {
        if (analyserRef.current) {
          analyserRef.current.getByteFrequencyData(dataArray);
          let sum = 0;
          for (let i = 0; i < dataArray.length; i++) {
            sum += dataArray[i];
          }
          const avg = sum / dataArray.length;
          // Normalize gently so it's not erratic
          const level = Math.min(1.0, (avg / 255) * 1.8);
          setAudioLevel(level);
        }
        animationFrameRef.current = requestAnimationFrame(updateAudioLevel);
      };

      updateAudioLevel();
      return stream;
    } catch (err) {
      console.warn('Microfone não acessível ou permissão negada:', err);
      return null;
    }
  };

  const stopAudioAnalyzer = () => {
    if (animationFrameRef.current) {
      cancelAnimationFrame(animationFrameRef.current);
      animationFrameRef.current = null;
    }
    if (mediaStreamRef.current) {
      mediaStreamRef.current.getTracks().forEach((track) => track.stop());
      mediaStreamRef.current = null;
    }
    if (audioContextRef.current && audioContextRef.current.state !== 'closed') {
      audioContextRef.current.close().catch(() => {});
      audioContextRef.current = null;
    }
    setAudioLevel(0.05);
  };

  // Limpar texto de comando (remover wake-word se presente)
  const cleanTranscriptCommand = (text: string): string => {
    let clean = text.trim();
    // Remove pontuações iniciais
    clean = clean.replace(/^[,\s.-]+/, '');
    // Expressão sem flag /g para evitar bug de lastIndex
    const wakeWordPattern = /^(?:ei|hey|hi|ok|oye)?\s*(?:sexta-feira|sexta\s*feira|friday|sexta)[,\s.-]*/i;
    clean = clean.replace(wakeWordPattern, '').trim();
    return clean || text.trim();
  };

  // Disparar comando detectado e reiniciar buffer
  const dispatchCommand = useCallback((rawText: string) => {
    if (silenceTimerRef.current) {
      clearTimeout(silenceTimerRef.current);
      silenceTimerRef.current = null;
    }

    const cleaned = cleanTranscriptCommand(rawText);
    accumulatedTranscriptRef.current = '';
    setInterimTranscript('');

    if (cleaned.length >= 2) {
      console.log('🎙️ [VOICE ENGINE] Comando detectado:', cleaned);
      onCommandDetected(cleaned);
    }
  }, [onCommandDetected]);

  // Inicializar Reconhecimento Contínuo
  const startVoiceRecognition = useCallback(async () => {
    const SpeechRecognition = (window as any).SpeechRecognition || (window as any).webkitSpeechRecognition;

    if (!SpeechRecognition) {
      alert('Seu navegador não possui suporte à API de reconhecimento de fala Web Speech. Use o Google Chrome, Edge ou Brave.');
      return;
    }

    try {
      await startAudioAnalyzer();

      if (recognitionRef.current) {
        try { recognitionRef.current.stop(); } catch {}
      }

      const recognition = new SpeechRecognition();
      recognition.lang = 'pt-BR';
      recognition.continuous = true;
      recognition.interimResults = true;
      recognition.maxAlternatives = 1;

      recognition.onstart = () => {
        isListeningRef.current = true;
        setIsListening(true);
        onStateChange('LISTENING');
        console.log('🎙️ [VOICE ENGINE] Microfone ATIVO e escutando continuamente.');
      };

      recognition.onresult = (event: any) => {
        let currentInterim = '';
        let currentFinal = '';

        for (let i = event.resultIndex; i < event.results.length; ++i) {
          const item = event.results[i];
          const transcriptChunk = item[0].transcript;
          if (item.isFinal) {
            currentFinal += transcriptChunk;
          } else {
            currentInterim += transcriptChunk;
          }
        }

        const currentSpeech = (currentFinal || currentInterim).trim();
        if (currentSpeech.length > 0) {
          accumulatedTranscriptRef.current = currentSpeech;
          setInterimTranscript(currentSpeech);

          // Voice Activity Detection (VAD): se o usuário pausar por 1.2s, dispara automaticamente
          if (silenceTimerRef.current) clearTimeout(silenceTimerRef.current);
          silenceTimerRef.current = setTimeout(() => {
            if (accumulatedTranscriptRef.current.trim().length >= 2) {
              dispatchCommand(accumulatedTranscriptRef.current);
            }
          }, 1200);

          // Se o browser marcou como final imediatamente
          if (currentFinal.trim().length >= 2) {
            dispatchCommand(currentFinal);
          }
        }
      };

      recognition.onerror = (event: any) => {
        // Ignora 'no-speech' pois é comum quando o usuário está quieto
        if (event.error !== 'no-speech') {
          console.warn('⚠️ [VOICE ENGINE] Erro de reconhecimento:', event.error);
        }
      };

      recognition.onend = () => {
        // Se ainda deve estar ouvindo, reinicia automaticamente
        if (isListeningRef.current) {
          setTimeout(() => {
            if (isListeningRef.current) {
              try {
                recognition.start();
              } catch (e) {
                // já iniciado
              }
            }
          }, 120);
        } else {
          setIsListening(false);
        }
      };

      recognition.start();
      recognitionRef.current = recognition;
      isListeningRef.current = true;
      setIsListening(true);
    } catch (err) {
      console.error('Falha ao iniciar SpeechRecognition:', err);
      setIsListening(false);
      isListeningRef.current = false;
    }
  }, [onStateChange, dispatchCommand]);

  const stopVoiceRecognition = useCallback(() => {
    isListeningRef.current = false;
    setIsListening(false);

    if (silenceTimerRef.current) {
      clearTimeout(silenceTimerRef.current);
      silenceTimerRef.current = null;
    }

    if (recognitionRef.current) {
      try {
        recognitionRef.current.stop();
      } catch {}
      recognitionRef.current = null;
    }

    stopAudioAnalyzer();
    setInterimTranscript('');
    onStateChange('IDLE');
  }, [onStateChange]);

  // Síntese de Voz Robusta (TTS) com correção para Chromium
  const speakResponse = useCallback((text: string) => {
    if (!('speechSynthesis' in window)) {
      console.warn('speechSynthesis não suportado');
      onStateChange('IDLE');
      return;
    }

    try {
      window.speechSynthesis.cancel();
      if (window.speechSynthesis.paused) {
        window.speechSynthesis.resume();
      }

      const utterance = new SpeechSynthesisUtterance(text);
      utterance.lang = 'pt-BR';
      utterance.rate = 1.05;
      utterance.pitch = 1.0;
      utterance.volume = 1.0;

      // Selecionar voz brasileira mais natural
      const voices = voicesRef.current.length > 0 ? voicesRef.current : window.speechSynthesis.getVoices();
      const ptVoice = voices.find((v) => 
        (v.lang.includes('pt-BR') || v.lang.includes('pt_BR') || v.lang.includes('pt')) &&
        (v.name.includes('Google') || v.name.includes('Microsoft') || v.name.includes('Luciana') || v.name.includes('Francisca') || v.name.includes('Brazil'))
      ) || voices.find((v) => v.lang.includes('pt'));

      if (ptVoice) {
        utterance.voice = ptVoice;
      }

      currentUtteranceRef.current = utterance;

      utterance.onstart = () => {
        onStateChange('SPEAKING');
        setAudioLevel(0.65);
      };

      utterance.onend = () => {
        currentUtteranceRef.current = null;
        onStateChange('IDLE');
        setAudioLevel(0.05);
      };

      utterance.onerror = (e) => {
        console.warn('Erro na síntese vocal:', e);
        currentUtteranceRef.current = null;
        onStateChange('IDLE');
        setAudioLevel(0.05);
      };

      // Workaround para bug do Chromium onde falas longas congelam após 14 segundos
      const resumeInterval = setInterval(() => {
        if (!window.speechSynthesis.speaking) {
          clearInterval(resumeInterval);
        } else {
          window.speechSynthesis.resume();
        }
      }, 5000);

      window.speechSynthesis.speak(utterance);
    } catch (err) {
      console.warn('Erro ao disparar fala:', err);
      onStateChange('IDLE');
    }
  }, [onStateChange]);

  useEffect(() => {
    return () => {
      stopAudioAnalyzer();
      if (silenceTimerRef.current) clearTimeout(silenceTimerRef.current);
      if ('speechSynthesis' in window) window.speechSynthesis.cancel();
    };
  }, []);

  return {
    isListening,
    audioLevel,
    hasSpeechRecognition,
    interimTranscript,
    startVoiceRecognition,
    stopVoiceRecognition,
    speakResponse
  };
};
