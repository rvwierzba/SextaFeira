import { useCallback, useEffect, useRef, useState } from 'react';
import * as signalR from '@microsoft/signalr';
import { AgentVisualState } from '../components/3d/HologramCoreCanvas';
import { MicCapture, pcmToBase64 } from '../lib/micCapture';
import { Speaker } from '../lib/speaker';
import { getDevice } from '../lib/device';

export interface LogEntry {
  id: string;
  kind: 'user' | 'assistant' | 'tool' | 'ignored' | 'notice';
  text: string;
  detail?: string;
  deviceName?: string;
  ok?: boolean;
}

export interface DeviceSummary { deviceId: string; name: string; kind: string; micActive: boolean; }

type ConnState = 'connecting' | 'online' | 'offline';

const uid = () => Math.random().toString(36).slice(2, 10);

export function useAssistant() {
  const device = useRef(getDevice()).current;
  const [connection, setConnection] = useState<ConnState>('connecting');
  const [agentState, setAgentState] = useState<AgentVisualState>('IDLE');
  const [statusText, setStatusText] = useState('Conectando ao núcleo...');
  const [micOn, setMicOn] = useState(false);
  const [micError, setMicError] = useState<string | null>(null);
  const [level, setLevel] = useState(0);
  const [lastUser, setLastUser] = useState('');
  const [assistantText, setAssistantText] = useState('');
  const [log, setLog] = useState<LogEntry[]>([]);
  const [devices, setDevices] = useState<DeviceSummary[]>([]);
  const [lastLatency, setLastLatency] = useState<{ engine: string; model: string; ttftMs: number; totalMs: number } | null>(null);

  const hubRef = useRef<signalR.HubConnection | null>(null);
  const micRef = useRef<MicCapture | null>(null);
  const speakerRef = useRef<Speaker | null>(null);
  const busyRef = useRef(false); // há um turno em andamento
  const currentTurnRef = useRef<string | null>(null);
  const micLevelRef = useRef(0);
  const speakLevelRef = useRef(0);

  const pushLog = useCallback((e: Omit<LogEntry, 'id'>) => {
    setLog(prev => [...prev.slice(-80), { ...e, id: uid() }]);
  }, []);

  // ── Fala ────────────────────────────────────────────────────────────────
  if (!speakerRef.current) {
    speakerRef.current = new Speaker({
      onSpeakingChange: speaking => {
        if (speaking) setAgentState('SPEAKING');
        else if (!busyRef.current) setAgentState(micRef.current?.active ? 'LISTENING' : 'IDLE');
      },
      onLevel: l => { speakLevelRef.current = l; },
      onTurnPlaybackFinished: () => {
        hubRef.current?.invoke('PlaybackFinished').catch(() => {});
        if (!busyRef.current) {
          setAgentState(micRef.current?.active ? 'LISTENING' : 'IDLE');
          setStatusText(micRef.current?.active ? 'Pode continuar falando...' : 'Pronta.');
        }
      },
    });
  }

  // Nível combinado (microfone ou fala) para animar a esfera, sem re-render a cada frame de áudio.
  useEffect(() => {
    const t = window.setInterval(() => setLevel(Math.max(micLevelRef.current, speakLevelRef.current)), 60);
    return () => clearInterval(t);
  }, []);

  // ── Conexão ─────────────────────────────────────────────────────────────
  useEffect(() => {
    const hub = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/assistant')
      .withAutomaticReconnect([0, 1000, 2000, 5000, 10000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();
    hubRef.current = hub;

    const register = () => hub.invoke('Register', device.id, device.name, device.kind)
      .then(() => hub.invoke('SetMicState', micRef.current?.active ?? false));

    hub.on('state', (s: { state: string; detail: string; deviceId: string }) => {
      if (s.state === 'IDLE') { if (!speakerRef.current?.isSpeaking && !busyRef.current) setAgentState(micRef.current?.active ? 'LISTENING' : 'IDLE'); return; }
      if (s.state === 'TRANSCRIBING') { if (!busyRef.current) { setAgentState('THINKING'); setStatusText('Entendendo...'); } return; }
      busyRef.current = true;
      if (!speakerRef.current?.isSpeaking) setAgentState(s.state === 'EXECUTING' ? 'EXECUTING' : 'THINKING');
      setStatusText(s.detail);
    });

    hub.on('transcript', (t: { deviceId: string; deviceName: string; text: string; accepted: boolean; reason?: string }) => {
      if (t.accepted) {
        setLastUser(t.text);
        setAssistantText('');
        pushLog({ kind: 'user', text: t.text, deviceName: t.deviceName });
      } else if (t.text) {
        pushLog({ kind: 'ignored', text: t.text, detail: t.reason, deviceName: t.deviceName });
      }
    });

    hub.on('assistantDelta', (d: { turnId: string; text: string }) => {
      if (currentTurnRef.current !== d.turnId) { currentTurnRef.current = d.turnId; setAssistantText(''); }
      setAssistantText(prev => prev + d.text);
    });

    hub.on('assistantAudio', (a: { turnId: string; seq: number; text: string; audio: string | null; targetDeviceId: string }) => {
      // Só o dispositivo que fez a pergunta fala; os outros mostram a legenda.
      if (a.targetDeviceId === device.id) speakerRef.current?.enqueue(a.turnId, a.seq, a.text, a.audio);
    });

    hub.on('tool', (t: { name: string; phase: string; ok?: boolean; preview?: string; ms?: number; args?: string }) => {
      if (t.phase === 'end') pushLog({ kind: 'tool', text: t.name, detail: `${t.ms} ms · ${t.preview ?? ''}`, ok: t.ok });
    });

    hub.on('assistantDone', (d: { turnId: string; text: string; engine?: string; model?: string; ttftMs?: number; totalMs?: number; targetDeviceId: string; audioCount: number; cancelled?: boolean }) => {
      busyRef.current = false;
      if (d.text) pushLog({ kind: 'assistant', text: d.text });
      if (d.engine && d.totalMs) setLastLatency({ engine: d.engine, model: d.model ?? '', ttftMs: d.ttftMs ?? 0, totalMs: d.totalMs });
      if (d.targetDeviceId === device.id) speakerRef.current?.finishTurn(d.turnId, d.audioCount);
      if (!speakerRef.current?.isSpeaking) {
        setAgentState(micRef.current?.active ? 'LISTENING' : 'IDLE');
        setStatusText(micRef.current?.active ? 'Ouvindo...' : 'Pronta.');
      }
    });

    hub.on('stopAudio', () => speakerRef.current?.stop());
    hub.on('notice', (msg: string) => { pushLog({ kind: 'notice', text: msg }); setStatusText(msg); });
    hub.on('devices', (list: DeviceSummary[]) => setDevices(list));
    hub.on('conversationCleared', () => { setLog([]); setLastUser(''); setAssistantText(''); });

    hub.onreconnecting(() => { setConnection('connecting'); setStatusText('Reconectando...'); });
    hub.onreconnected(() => { setConnection('online'); setStatusText('Reconectada.'); register(); });
    hub.onclose(() => { setConnection('offline'); setStatusText('Sem conexão com o núcleo.'); });

    let cancelled = false;
    const start = async () => {
      while (!cancelled) {
        try {
          await hub.start();
          await register();
          setConnection('online');
          setStatusText('Pronta. Ative o microfone ou digite.');
          return;
        } catch {
          setConnection('offline');
          setStatusText('Núcleo offline — tentando de novo...');
          await new Promise(r => setTimeout(r, 3000));
        }
      }
    };
    start();
    return () => { cancelled = true; hub.stop(); micRef.current?.stop(); speakerRef.current?.stop(); };
  }, [device, pushLog]);

  // ── Microfone ───────────────────────────────────────────────────────────
  const startMic = useCallback(async () => {
    setMicError(null);
    speakerRef.current?.unlock();
    if (!micRef.current) {
      micRef.current = new MicCapture({
        isAssistantSpeaking: () => speakerRef.current?.isSpeaking ?? false,
        onLevel: l => { micLevelRef.current = l; },
        onSpeechStart: strength => {
          // Barge-in: falar por cima interrompe a resposta.
          if (speakerRef.current?.isSpeaking && strength > 1.2) {
            speakerRef.current.stop();
            hubRef.current?.invoke('Interrupt').catch(() => {});
          }
          if (!busyRef.current) setAgentState('LISTENING');
        },
        onUtterance: pcm => {
          const hub = hubRef.current;
          if (hub?.state !== signalR.HubConnectionState.Connected) return;
          hub.invoke('SendUtterance', pcmToBase64(pcm)).catch(err => pushLog({ kind: 'notice', text: `Falha ao enviar áudio: ${err}` }));
        },
      });
    }
    try {
      await micRef.current.start();
      setMicOn(true);
      setAgentState('LISTENING');
      setStatusText('Ouvindo. Diga "Sexta-Feira" para chamar.');
      hubRef.current?.invoke('SetMicState', true).catch(() => {});
    } catch (e: any) {
      const msg = e?.name === 'NotAllowedError' ? 'Permissão do microfone negada.' : (e?.message ?? String(e));
      setMicError(msg);
      setStatusText(msg);
    }
  }, [pushLog]);

  const stopMic = useCallback(() => {
    micRef.current?.stop();
    setMicOn(false);
    if (!busyRef.current && !speakerRef.current?.isSpeaking) setAgentState('IDLE');
    setStatusText('Microfone desligado.');
    hubRef.current?.invoke('SetMicState', false).catch(() => {});
  }, []);

  const toggleMic = useCallback(() => (micRef.current?.active ? stopMic() : startMic()), [startMic, stopMic]);

  const sendText = useCallback((text: string) => {
    speakerRef.current?.unlock();
    speakerRef.current?.stop();
    hubRef.current?.invoke('SendText', text).catch(err => pushLog({ kind: 'notice', text: `Falha ao enviar: ${err}` }));
  }, [pushLog]);

  const interrupt = useCallback(() => {
    speakerRef.current?.stop();
    hubRef.current?.invoke('Interrupt').catch(() => {});
  }, []);

  const clearConversation = useCallback(() => hubRef.current?.invoke('ClearConversation').catch(() => {}), []);

  return {
    device, connection, agentState, statusText, micOn, micError, level, lastUser, assistantText, log, devices, lastLatency,
    speaker: speakerRef.current!, toggleMic, sendText, interrupt, clearConversation,
  };
}
