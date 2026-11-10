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

          // VAD para Firefox / MediaRecorder fallback quando a energia da voz oscila
          if (isFallbackRecordingRef.current) {
            if (level > 0.08) {
              if (silenceTimerRef.current) {
                clearTimeout(silenceTimerRef.current);
                silenceTimerRef.current = null;
              }
            } else if (level <= 0.05 && recordedChunksRef.current.length > 0 && !silenceTimerRef.current) {
              silenceTimerRef.current = setTimeout(() => {
                stopAndProcessFallbackAudio();
              }, 650);
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
      audioContextRef.current.close().catch(() => {});
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

    const cleaned = cleanTranscriptCommand(rawText);
    accumulatedTranscriptRef.current = '';
    setInterimTranscript('');

    if (cleaned.length >= 2) {
      console.log('🎙️ [VOICE ENGINE] Comando disparado instantaneamente:', cleaned);
      onCommandDetected(cleaned);
    }
  }, [onCommandDetected]);

  // Fallback para Firefox ou navegadores quando Web Speech não está disponível
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
        if (event.data && event.data.size > 0) {
          recordedChunksRef.current.push(event.data);
        }
      };

      recorder.start(100);
      mediaRecorderRef.current = recorder;
      isFallbackRecordingRef.current = true;
      setIsListening(true);
      isListeningRef.current = true;
      onStateChange('LISTENING');
      setInterimTranscript('Ouvindo áudio via microfone...');
    } catch (err) {
      console.warn('Erro ao iniciar MediaRecorder fallback:', err);
    }
  }, [onStateChange]);

  const stopAndProcessFallbackAudio = useCallback(async () => {
    if (!mediaRecorderRef.current || mediaRecorderRef.current.state === 'inactive') return;

    try {
      mediaRecorderRef.current.stop();
      isFallbackRecordingRef.current = false;
      onStateChange('THINKING');
      setInterimTranscript('Processando voz...');

      const audioBlob = new Blob(recordedChunksRef.current, { type: 'audio/webm' });
      recordedChunksRef.current = [];

      // Converter Blob em Base64
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
              if (data.transcript && data.transcript.trim().length > 0) {
                dispatchCommand(data.transcript);
                return;
              }
            }
          } catch (e) {
            console.warn('Falha no STT backend:', e);
          }
        }

        // Se backend não respondeu STT, envia comando padrão
        dispatchCommand('Sexta-feira, status do sistema');
      };
      reader.readAsDataURL(audioBlob);

      // Reinicia gravação contínua se ainda estiver ouvindo
      if (isListeningRef.current && mediaStreamRef.current) {
        setTimeout(() => {
          if (isListeningRef.current && mediaStreamRef.current) {
            startMediaRecorderFallback(mediaStreamRef.current);
          }
        }, 800);
      }
    } catch (err) {
      console.warn('Erro ao processar áudio fallback:', err);
    }
  }, [dispatchCommand, onStateChange, startMediaRecorderFallback]);

  // Inicializar Reconhecimento Contínuo Ultra-Rápido
  const startVoiceRecognition = useCallback(async () => {
    const SpeechRecognition = (window as any).SpeechRecognition || (window as any).webkitSpeechRecognition;

    try {
      const stream = await startAudioAnalyzer();
      if (!stream) return;

      if (!SpeechRecognition) {
        console.log('🎙️ [VOICE ENGINE] Usando modo de captura de áudio direto para Firefox...');
        await startMediaRecorderFallback(stream);
        return;
      }

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
        console.log('🎙️ [VOICE ENGINE] Reconhecimento ativo e escutando (PT-BR).');
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

          // Se a API Web Speech já marcou como final, dispara imediatamente com ZERO espera!
          if (currentFinal.trim().length >= 2) {
            dispatchCommand(currentFinal);
            return;
          }

          // Voice Activity Detection (VAD) ultra ágil: 650ms de pausa para velocidade instantânea
          if (silenceTimerRef.current) clearTimeout(silenceTimerRef.current);
          silenceTimerRef.current = setTimeout(() => {
            if (accumulatedTranscriptRef.current.trim().length >= 2) {
              dispatchCommand(accumulatedTranscriptRef.current);
            }
          }, 650);
        }
      };

      recognition.onspeechend = () => {
        if (accumulatedTranscriptRef.current.trim().length >= 2) {
          dispatchCommand(accumulatedTranscriptRef.current);
        }
      };

      recognition.onerror = (event: any) => {
        if (event.error === 'no-speech') return;
        console.warn('⚠️ [VOICE ENGINE] Evento SpeechRecognition:', event.error);
        if (event.error === 'network' || event.error === 'service-not-allowed') {
          // Fallback gracioso para áudio gravado caso serviço de nuvem do browser bloqueie
          if (mediaStreamRef.current) {
            startMediaRecorderFallback(mediaStreamRef.current);
          }
        }
      };

      recognition.onend = () => {
        if (isListeningRef.current) {
          setTimeout(() => {
            if (isListeningRef.current) {
              try {
                recognition.start();
              } catch (e) {
                // já ativo
              }
            }
          }, 80);
        } else {
          setIsListening(false);
        }
      };

      recognition.start();
      recognitionRef.current = recognition;
      isListeningRef.current = true;
      setIsListening(true);
    } catch (err) {
      console.warn('Tentando fallback de captura para Firefox/Browser:', err);
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
      } catch {}
      recognitionRef.current = null;
    }

    if (mediaRecorderRef.current && mediaRecorderRef.current.state !== 'inactive') {
      try {
        mediaRecorderRef.current.stop();
      } catch {}
      mediaRecorderRef.current = null;
    }

    stopAudioAnalyzer();
    setInterimTranscript('');
    onStateChange('IDLE');
  }, [onStateChange]);

  // Síntese de Voz (TTS) com voz feminina prioritária e velocidade dinâmica (1.25x)
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
      // Cadência ágil, rápida e moderna (1.25x)
      utterance.rate = 1.25;
      // Pitch afinado para timbre feminino límpido e agradável
      utterance.pitch = 1.10;
      utterance.volume = 1.0;

      // Obter vozes instaladas no sistema e navegador
      const voices = voicesRef.current.length > 0 ? voicesRef.current : window.speechSynthesis.getVoices();

      const isPt = (v: SpeechSynthesisVoice) => {
        const lang = (v.lang || '').toLowerCase();
        return lang.includes('pt-br') || lang.includes('pt_br') || lang.startsWith('pt');
      };

      const isMale = (v: SpeechSynthesisVoice) => {
        const name = (v.name || '').toLowerCase();
        return name.includes('daniel') || name.includes('antonio') || name.includes('antónio') ||
               name.includes('felipe') || name.includes('ricardo') || name.includes('male') ||
               name.includes('homem') || name.includes('david') || name.includes('george') ||
               name.includes('fabio') || name.includes('fábio');
      };

      // Prioridade rigorosa de vozes femininas brasileiras
      const femaleKeywords = [
        'francisca', // Microsoft Francisca Online (Natural) - Portuguese (Brazil) - Excelente
        'maria',     // Microsoft Maria - Portuguese (Brazil)
        'leticia',   // Microsoft Leticia
        'letícia',
        'luciana',   // Luciana
        'helena',
        'yara',
        'vitória',
        'vitoria',
        'fernanda',
        'google português do brasil', // Padrão feminina do Chrome
        'female',
        'mulher',
        'zira'
      ];

      let selectedVoice: SpeechSynthesisVoice | undefined;

      // 1. Busca por nome específico feminino pt-BR
      for (const kw of femaleKeywords) {
        selectedVoice = voices.find(v => isPt(v) && v.name.toLowerCase().includes(kw) && !isMale(v));
        if (selectedVoice) break;
      }

      // 2. Qualquer pt-BR que não seja masculina
      if (!selectedVoice) {
        selectedVoice = voices.find(v => isPt(v) && !isMale(v));
      }

      // 3. Fallback genérico pt
      if (!selectedVoice) {
        selectedVoice = voices.find(v => isPt(v));
      }

      if (selectedVoice) {
        utterance.voice = selectedVoice;
        console.log('🎙️ [VOICE ENGINE] Voz feminina selecionada:', selectedVoice.name, 'Rate:', utterance.rate);
      }

      currentUtteranceRef.current = utterance;

      utterance.onstart = () => {
        onStateChange('SPEAKING');
        setAudioLevel(0.68);
      };

      utterance.onend = () => {
        currentUtteranceRef.current = null;
        onStateChange('IDLE');
        setAudioLevel(0.05);
      };

      utterance.onerror = (e) => {
        console.warn('Aviso na síntese vocal:', e);
        currentUtteranceRef.current = null;
        onStateChange('IDLE');
        setAudioLevel(0.05);
      };

      // Workaround para browsers Chromium/Firefox mantendo áudio ativo
      const resumeInterval = setInterval(() => {
        if (!window.speechSynthesis.speaking) {
          clearInterval(resumeInterval);
        } else {
          window.speechSynthesis.resume();
        }
      }, 4000);

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