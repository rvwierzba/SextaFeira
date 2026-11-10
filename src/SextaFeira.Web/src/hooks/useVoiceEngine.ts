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
  const mediaRecorderRef = useRef<MediaRecorder | null>(null);
  const recordedChunksRef = useRef<Blob[]>([]);
  const animationFrameRef = useRef<number | null>(null);
  const silenceTimerRef = useRef<any>(null);
  const accumulatedTranscriptRef = useRef<string>('');
  const currentUtteranceRef = useRef<SpeechSynthesisUtterance | null>(null);
  const voicesRef = useRef<SpeechSynthesisVoice[]>([]);
  const isFallbackRecordingRef = useRef(false);
  const isSpeakingRef = useRef(false);

  // Carregar e indexar vozes do navegador
  useEffect(() => {
    if ('speechSynthesis' in window) {
      const updateVoices = () => {
        voicesRef.current = window.speechSynthesis.getVoices();
      };
      updateVoices();
      window.speechSynthesis.onvoiceschanged = updateVoices;
    }
  }, []);

  // Verificar se há suporte nativo à Web Speech API
  useEffect(() => {
    const SpeechRecognition = (window as any).SpeechRecognition || (window as any).webkitSpeechRecognition;
    setHasSpeechRecognition(!!SpeechRecognition);
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
      analyser.smoothingTimeConstant = 0.35;
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
          const level = Math.min(1.0, (avg / 255) * 1.85);
          setAudioLevel(level);

          // VAD do gravador MediaRecorder (Firefox fallback)
          if (isFallbackRecordingRef.current && !isSpeakingRef.current) {
            if (level > 0.08) {
              // Usuário está falando: cancela temporizador de silêncio
              if (silenceTimerRef.current) {
                clearTimeout(silenceTimerRef.current);
                silenceTimerRef.current = null;
              }
            } else if (level <= 0.05 && recordedChunksRef.current.length > 2 && !silenceTimerRef.current) {
              // Pausa natural de 1.4s após falar antes de finalizar
              silenceTimerRef.current = setTimeout(() => {
                stopAndProcessFallbackAudio();
              }, 1400);
            }
          }
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
      audioContextRef.current.close().catch(() => { });
      audioContextRef.current = null;
    }
    setAudioLevel(0.05);
  };

  // Limpar texto de comando (remover wake-word se presente)
  const cleanTranscriptCommand = (text: string): string => {
    let clean = text.trim();
    clean = clean.replace(/^[,\s.-]+/, '');
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

    // Se a assistente já estiver processando ou falando, ignora
    if (isSpeakingRef.current) {
      accumulatedTranscriptRef.current = '';
      setInterimTranscript('');
      return;
    }

    const cleaned = cleanTranscriptCommand(rawText);
    accumulatedTranscriptRef.current = '';
    setInterimTranscript('');

    if (cleaned.length >= 2) {
      // Muta imediatamente o microfone durante o ciclo de inferência para evitar qualquer eco ou auto-captura
      isSpeakingRef.current = true;
      if (recognitionRef.current) {
        try { recognitionRef.current.stop(); } catch { }
      }

      console.log('🎙️ [VOICE ENGINE] Comando completo capturado:', cleaned);
      onCommandDetected(cleaned);
    }
  }, [onCommandDetected]);

  // Fallback MediaRecorder para navegadores sem Web Speech
  const startMediaRecorderFallback = useCallback(async (stream: MediaStream) => {
    try {
      recordedChunksRef.current = [];
      const mimeType = MediaRecorder.isTypeSupported('audio/webm;codecs=opus')
        ? 'audio/webm;codecs=opus'
        : MediaRecorder.isTypeSupported('audio/webm')
          ? 'audio/webm'
          : 'audio/ogg';

      const recorder = new MediaRecorder(stream, { mimeType });
      recorder.ondataavailable = (event) => {
        if (event.data && event.data.size > 0 && !isSpeakingRef.current) {
          recordedChunksRef.current.push(event.data);
        }
      };

      recorder.start(250);
      mediaRecorderRef.current = recorder;
      isFallbackRecordingRef.current = true;
      setIsListening(true);
      isListeningRef.current = true;
      onStateChange('LISTENING');
      setInterimTranscript('Ouvindo você falar...');
    } catch (err) {
      console.warn('Erro ao iniciar MediaRecorder fallback:', err);
    }
  }, [onStateChange]);

  const stopAndProcessFallbackAudio = useCallback(async () => {
    if (!mediaRecorderRef.current || mediaRecorderRef.current.state === 'inactive') return;
    if (isSpeakingRef.current) return;

    try {
      mediaRecorderRef.current.stop();
      isFallbackRecordingRef.current = false;

      // Se a gravação foi minúscula (< 4 chunks), descarta sem enviar
      if (recordedChunksRef.current.length < 3) {
        recordedChunksRef.current = [];
        if (isListeningRef.current && mediaStreamRef.current) {
          startMediaRecorderFallback(mediaStreamRef.current);
        }
        return;
      }

      onStateChange('THINKING');
      setInterimTranscript('Processando fala...');

      const audioBlob = new Blob(recordedChunksRef.current, { type: 'audio/webm' });
      recordedChunksRef.current = [];

      const reader = new FileReader();
      reader.onloadend = async () => {
        const base64Audio = (reader.result as string)?.split(',')[1];
        if (base64Audio) {
          try {
            const res = await fetch('http://localhost:5000/api/core/transcribe', {
              method: 'POST',
              headers: { 'Content-Type': 'application/json' },
              body: JSON.stringify({ audioBase64: base64Audio, language: 'pt-BR' })
            });

            if (res.ok) {
              const data = await res.json();
              if (data.transcript && data.transcript.trim().length >= 2) {
                dispatchCommand(data.transcript);
                return;
              }
            }
          } catch (e) {
            console.warn('Falha no STT backend:', e);
          }
        }

        // Se não conseguiu transcrever, retorna silenciosamente ao modo de escuta
        if (isListeningRef.current && mediaStreamRef.current && !isSpeakingRef.current) {
          startMediaRecorderFallback(mediaStreamRef.current);
        }
      };
      reader.readAsDataURL(audioBlob);

      // Reinicia gravação contínua se ainda estiver ouvindo
      if (isListeningRef.current && mediaStreamRef.current) {
        setTimeout(() => {
          if (isListeningRef.current && mediaStreamRef.current && !isSpeakingRef.current) {
            startMediaRecorderFallback(mediaStreamRef.current);
          }
        }, 1200);
      }
    } catch (err) {
      console.warn('Erro ao processar áudio fallback:', err);
    }
  }, [dispatchCommand, onStateChange, startMediaRecorderFallback]);

  // Inicializar Reconhecimento Contínuo Sem Cortes Prematuros
  const startVoiceRecognition = useCallback(async () => {
    const SpeechRecognition = (window as any).SpeechRecognition || (window as any).webkitSpeechRecognition;

    try {
      const stream = await startAudioAnalyzer();
      if (!stream) return;

      if (!SpeechRecognition) {
        console.log('🎙️ [VOICE ENGINE] Usando modo de escuta direta (Firefox)...');
        await startMediaRecorderFallback(stream);
        return;
      }

      if (recognitionRef.current) {
        try { recognitionRef.current.stop(); } catch { }
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
        console.log('🎙️ [VOICE ENGINE] Reconhecimento ativo contínuo (PT-BR).');
      };

      recognition.onresult = (event: any) => {
        // Se a Sexta-Feira estiver falando, não processe áudio para evitar eco/interrupção
        if (isSpeakingRef.current) {
          return;
        }

        // Itera por todos os índices de resultados para não perder a frase completa
        let completeTranscript = '';
        for (let i = 0; i < event.results.length; ++i) {
          completeTranscript += event.results[i][0].transcript;
        }

        const currentSpeech = completeTranscript.trim();
        if (currentSpeech.length > 0) {
          accumulatedTranscriptRef.current = currentSpeech;
          setInterimTranscript(currentSpeech);

          if (silenceTimerRef.current) clearTimeout(silenceTimerRef.current);

          // Se o usuário falou apenas a wake-word ("sexta-feira", "ei sexta"),
          // dá uma janela (2.0s) para ele fazer a pergunta sem cortar. Para comandos normais,
          // usa 950ms para um diálogo rápido e fluido estilo Jarvis.
          const cleaned = cleanTranscriptCommand(currentSpeech);
          const isJustWakeWord = !cleaned || cleaned === 'sexta-feira' || cleaned === 'sexta feira' || cleaned === 'friday';
          const waitTime = isJustWakeWord ? 2000 : 950;

          silenceTimerRef.current = setTimeout(() => {
            if (accumulatedTranscriptRef.current.trim().length >= 2 && !isSpeakingRef.current) {
              const toDispatch = accumulatedTranscriptRef.current;
              accumulatedTranscriptRef.current = '';
              dispatchCommand(toDispatch);
            }
          }, waitTime);
        }
      };

      // onspeechend NÃO deve disparar comando prematuro pois navegadores acionam entre pausas breves
      recognition.onspeechend = () => {
        // Apenas permite que o timer de 1.4s conclua naturalmente
      };

      recognition.onerror = (event: any) => {
        if (event.error === 'no-speech') return;
        console.warn('⚠️ [VOICE ENGINE] Evento SpeechRecognition:', event.error);
        if (event.error === 'network' || event.error === 'service-not-allowed') {
          if (mediaStreamRef.current && !isFallbackRecordingRef.current) {
            startMediaRecorderFallback(mediaStreamRef.current);
          }
        }
      };

      recognition.onend = () => {
        if (isListeningRef.current && !isSpeakingRef.current) {
          setTimeout(() => {
            if (isListeningRef.current && !isSpeakingRef.current) {
              try {
                recognition.start();
              } catch (e) {
                // já ativo
              }
            }
          }, 150);
        } else if (!isSpeakingRef.current) {
          setIsListening(false);
        }
      };

      recognition.start();
      recognitionRef.current = recognition;
      isListeningRef.current = true;
      setIsListening(true);
    } catch (err) {
      console.warn('Fallback para escuta de áudio (Firefox):', err);
      if (mediaStreamRef.current) {
        await startMediaRecorderFallback(mediaStreamRef.current);
      }
    }
  }, [onStateChange, dispatchCommand, startMediaRecorderFallback]);

  const stopVoiceRecognition = useCallback(() => {
    isListeningRef.current = false;
    isFallbackRecordingRef.current = false;
    setIsListening(false);

    if (silenceTimerRef.current) {
      clearTimeout(silenceTimerRef.current);
      silenceTimerRef.current = null;
    }

    if (recognitionRef.current) {
      try {
        recognitionRef.current.stop();
      } catch { }
      recognitionRef.current = null;
    }

    if (mediaRecorderRef.current && mediaRecorderRef.current.state !== 'inactive') {
      try {
        mediaRecorderRef.current.stop();
      } catch { }
      mediaRecorderRef.current = null;
    }

    stopAudioAnalyzer();
    setInterimTranscript('');
    onStateChange('IDLE');
  }, [onStateChange]);

  // Síntese de Voz Atômica (TTS) com canal único, sem sobreposição nem auto-captura
  const speakResponse = useCallback((text: string) => {
    const cleanText = text.replace(/[*#_`]/g, '').trim();
    if (!cleanText) {
      isSpeakingRef.current = false;
      onStateChange('IDLE');
      return;
    }

    try {
      // 1. Muta o microfone imediatamente para impedir que ela ouça a própria voz
      isSpeakingRef.current = true;
      if (recognitionRef.current) {
        try { recognitionRef.current.stop(); } catch { }
      }

      // 2. Interrompe qualquer fala anterior
      if ('speechSynthesis' in window) {
        window.speechSynthesis.cancel();
      }

      if (!('speechSynthesis' in window)) {
        isSpeakingRef.current = false;
        onStateChange('IDLE');
        return;
      }

      if (window.speechSynthesis.paused) {
        window.speechSynthesis.resume();
      }

      const utterance = new SpeechSynthesisUtterance(cleanText);
      utterance.lang = 'pt-BR';
      utterance.volume = 1.0;

      const voices = voicesRef.current.length > 0 ? voicesRef.current : window.speechSynthesis.getVoices();

      const isStrictlyPtBr = (v: SpeechSynthesisVoice) => {
        const lang = (v.lang || '').toLowerCase().replace('_', '-');
        const name = (v.name || '').toLowerCase();
        const isBr = lang.includes('pt-br') || name.includes('brazil') || name.includes('brasil');
        const isPtPt = lang.includes('pt-pt') || name.includes('portugal') || name.includes('helena') || name.includes('fernanda') || name.includes('raquel') || name.includes('duarte');
        return isBr && !isPtPt;
      };

      const isMale = (v: SpeechSynthesisVoice) => {
        const name = (v.name || '').toLowerCase();
        return name.includes('daniel') || name.includes('antonio') || name.includes('antónio') ||
          name.includes('felipe') || name.includes('ricardo') || name.includes('male') ||
          name.includes('homem') || name.includes('david') || name.includes('george') ||
          name.includes('fabio') || name.includes('fábio');
      };

      // Prioridade máxima para vozes neurais brasileiras de alta fluidez
      const ptBrFemaleKeywords = [
        'google português do brasil',
        'francisca',
        'leticia',
        'letícia',
        'thalita',
        'luciana',
        'vitória',
        'vitoria',
        'maria'
      ];

      let selectedVoice: SpeechSynthesisVoice | undefined;
      for (const kw of ptBrFemaleKeywords) {
        selectedVoice = voices.find(v => isStrictlyPtBr(v) && v.name.toLowerCase().includes(kw) && !isMale(v));
        if (selectedVoice) break;
      }

      if (!selectedVoice) {
        selectedVoice = voices.find(v => isStrictlyPtBr(v) && !isMale(v));
      }

      if (selectedVoice) {
        utterance.voice = selectedVoice;
        if (selectedVoice.name.toLowerCase().includes('maria')) {
          utterance.pitch = 0.95;
          utterance.rate = 1.15;
        } else {
          utterance.pitch = 1.0;
          utterance.rate = 1.25;
        }
      } else {
        utterance.pitch = 1.0;
        utterance.rate = 1.20;
      }

      currentUtteranceRef.current = utterance;

      utterance.onstart = () => {
        isSpeakingRef.current = true;
        onStateChange('SPEAKING');
        setAudioLevel(0.72);
      };

      utterance.onend = () => {
        currentUtteranceRef.current = null;
        onStateChange('IDLE');
        setAudioLevel(0.05);

        // Aguarda 400ms para o som residual do ambiente dissipar antes de reativar o microfone
        setTimeout(() => {
          isSpeakingRef.current = false;
          if (isListeningRef.current && recognitionRef.current) {
            try {
              recognitionRef.current.start();
            } catch { }
          }
        }, 400);
      };

      utterance.onerror = () => {
        currentUtteranceRef.current = null;
        onStateChange('IDLE');
        setAudioLevel(0.05);
        setTimeout(() => {
          isSpeakingRef.current = false;
          if (isListeningRef.current && recognitionRef.current) {
            try { recognitionRef.current.start(); } catch { }
          }
        }, 300);
      };

      window.speechSynthesis.speak(utterance);
    } catch (err) {
      console.warn('Erro na síntese vocal:', err);
      isSpeakingRef.current = false;
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