import React, { useCallback, useEffect, useState } from 'react';
import { X, RefreshCw, Save, Cpu, Cloud, Mic, Plug, Smartphone, Brain } from 'lucide-react';
import { Speaker } from '../../lib/speaker';
import { renameDevice } from '../../lib/device';
import type { DeviceSummary } from '../../hooks/useAssistant';

interface EngineStatus { id: string; displayName: string; isLocal: boolean; model: string; available: boolean; detail: string; models?: string[] | null; }
interface McpStatus { name: string; transport: string; state: string; detail: string; toolCount: number; pendingLoginUrl?: string | null; }
interface SystemStatus {
  activeEngine: string;
  engines: EngineStatus[];
  stt: { ready: boolean; status: string };
  tts: { status: string };
  mcp: McpStatus[];
  tools: { name: string; source: string }[];
  lanUrls: string[];
}

interface Props {
  open: boolean;
  onClose: () => void;
  speaker: Speaker;
  devices: DeviceSummary[];
  deviceName: string;
}

const card: React.CSSProperties = { padding: 14, display: 'flex', flexDirection: 'column', gap: 10 };
const label: React.CSSProperties = { fontSize: '0.72rem', color: 'var(--hud-text-dim, #7fa8c0)', letterSpacing: 1, textTransform: 'uppercase' };
const input: React.CSSProperties = {
  background: 'rgba(4, 14, 30, 0.85)', border: '1px solid rgba(0, 240, 255, 0.3)', borderRadius: 4,
  padding: '7px 10px', color: '#d8f4ff', fontFamily: 'var(--hud-font-mono)', fontSize: '0.8rem', width: '100%', boxSizing: 'border-box',
};
const button: React.CSSProperties = {
  background: 'rgba(0, 240, 255, 0.15)', border: '1px solid var(--hud-cyan, #00f0ff)', color: '#00f0ff', borderRadius: 4,
  padding: '7px 12px', cursor: 'pointer', fontFamily: 'var(--hud-font-orbitron)', fontSize: '0.68rem', display: 'inline-flex', alignItems: 'center', gap: 6,
};

const ENGINE_KEYS: Record<string, 'OpenAI' | 'Anthropic' | 'Gemini' | 'Ollama'> = {
  ollama: 'Ollama', openai: 'OpenAI', anthropic: 'Anthropic', gemini: 'Gemini',
};

export const SettingsPanel: React.FC<Props> = ({ open, onClose, speaker, devices, deviceName }) => {
  const [status, setStatus] = useState<SystemStatus | null>(null);
  const [settings, setSettings] = useState<any>(null);
  const [mcpText, setMcpText] = useState('');
  const [message, setMessage] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [myName, setMyName] = useState(deviceName);
  const [voices, setVoices] = useState<SpeechSynthesisVoice[]>([]);

  const refresh = useCallback(async () => {
    try {
      const [st, se, mc] = await Promise.all([
        fetch('/api/status').then(r => r.json()),
        fetch('/api/settings').then(r => r.json()),
        fetch('/api/mcp/config').then(r => r.text()),
      ]);
      setStatus(st); setSettings(se); setMcpText(mc);
    } catch (e) {
      setMessage(`Não consegui falar com o núcleo: ${e}`);
    }
  }, []);

  useEffect(() => {
    if (!open) return;
    refresh();
    setVoices(Speaker.availableVoices());
    const t = setInterval(() => fetch('/api/status').then(r => r.json()).then(setStatus).catch(() => {}), 4000);
    return () => clearInterval(t);
  }, [open, refresh]);

  const update = (path: string, value: any) => {
    setSettings((prev: any) => {
      const next = structuredClone(prev);
      const keys = path.split('.');
      let o = next;
      for (const k of keys.slice(0, -1)) o = o[k];
      o[keys[keys.length - 1]] = value;
      return next;
    });
  };

  const save = async () => {
    setSaving(true); setMessage(null);
    try {
      const res = await fetch('/api/settings', { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(settings) });
      if (!res.ok) throw new Error((await res.json()).error ?? res.statusText);
      setSettings(await res.json());
      speaker.preferredVoices = settings.voice.preferredVoices;
      speaker.rate = settings.voice.speechRate;
      if (myName.trim() && myName !== deviceName) { renameDevice(myName.trim()); setMessage('Salvo. Recarregue a página para aplicar o novo nome do dispositivo.'); }
      else setMessage('Configurações salvas.');
      refresh();
    } catch (e: any) {
      setMessage(`Erro ao salvar: ${e.message ?? e}`);
    } finally { setSaving(false); }
  };

  const saveMcp = async () => {
    setMessage(null);
    const res = await fetch('/api/mcp/config', { method: 'PUT', body: mcpText });
    if (!res.ok) { setMessage((await res.json()).error); return; }
    setMessage('Servidores MCP recarregados.');
    refresh();
  };

  const testVoice = () => {
    speaker.preferredVoices = settings?.voice?.preferredVoices ?? speaker.preferredVoices;
    speaker.rate = settings?.voice?.speechRate ?? 1.1;
    speaker.unlock();
    speaker.stop();
    speaker.enqueue('teste-voz', 0, 'Oi! Essa é a minha voz neste dispositivo.', null);
    speaker.finishTurn('teste-voz', 1);
  };

  if (!open) return null;

  return (
    <div style={{ position: 'fixed', inset: 0, background: 'rgba(0, 4, 12, 0.72)', backdropFilter: 'blur(6px)', zIndex: 100, display: 'flex', justifyContent: 'center', alignItems: 'flex-start', overflowY: 'auto', padding: '24px 12px' }}
      onClick={onClose}>
      <div className="hud-panel" style={{ width: 'min(920px, 100%)', padding: 0 }} onClick={e => e.stopPropagation()}>
        <div className="hud-panel-header" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '12px 16px' }}>
          <span>CONFIGURAÇÕES DO NÚCLEO</span>
          <div style={{ display: 'flex', gap: 8 }}>
            <button style={button} onClick={refresh}><RefreshCw size={12} /> ATUALIZAR</button>
            <button style={button} onClick={onClose} aria-label="Fechar"><X size={14} /></button>
          </div>
        </div>

        {message && <div style={{ padding: '8px 16px', color: '#ffd27f', fontSize: '0.82rem' }}>{message}</div>}
        {!settings || !status ? <div style={{ padding: 24, color: '#7fa8c0' }}>Carregando...</div> : (
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))', gap: 12, padding: 12 }}>

            {/* Cérebro */}
            <section className="hud-panel" style={card}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}><Brain size={14} color="#00f0ff" /> <strong>Cérebro (LLM)</strong></div>
              <span style={label}>Engine ativa</span>
              {status.engines.map(e => (
                <label key={e.id} style={{ display: 'flex', gap: 8, alignItems: 'flex-start', cursor: 'pointer', fontSize: '0.85rem' }}>
                  <input type="radio" name="engine" checked={settings.activeEngine === e.id} onChange={() => update('activeEngine', e.id)} />
                  <span>
                    {e.isLocal ? <Cpu size={12} /> : <Cloud size={12} />} {e.displayName}
                    <span className={`hud-badge ${e.available ? 'hud-badge-success' : 'hud-badge-offline'}`} style={{ marginLeft: 6 }}>{e.available ? 'pronto' : 'indisponível'}</span>
                    <div style={{ fontSize: '0.72rem', color: '#7fa8c0' }}>{e.detail}</div>
                  </span>
                </label>
              ))}
              <label style={{ display: 'flex', gap: 8, fontSize: '0.8rem' }}>
                <input type="checkbox" checked={settings.fallbackToLocal} onChange={e => update('fallbackToLocal', e.target.checked)} />
                Se a nuvem falhar, responder com o modelo local
              </label>
            </section>

            {/* Modelos e chaves */}
            <section className="hud-panel" style={card}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}><Cpu size={14} color="#00f0ff" /> <strong>Modelos e contas</strong></div>
              <span style={label}>Modelo local (Ollama)</span>
              <select style={input} value={settings.ollama.model} onChange={e => update('ollama.model', e.target.value)}>
                {(status.engines.find(e => e.id === 'ollama')?.models ?? [settings.ollama.model])
                  .filter(m => !m.includes('embed'))
                  .map(m => <option key={m} value={m}>{m}</option>)}
              </select>
              {(['openai', 'anthropic', 'gemini'] as const).map(id => {
                const key = ENGINE_KEYS[id].charAt(0).toLowerCase() + ENGINE_KEYS[id].slice(1);
                const cfg = settings[key];
                return (
                  <div key={id} style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 6 }}>
                    <span style={{ ...label, gridColumn: '1 / -1' }}>{status.engines.find(e => e.id === id)?.displayName}</span>
                    <input style={input} placeholder="modelo" value={cfg.model} onChange={e => update(`${key}.model`, e.target.value)} />
                    <input style={input} placeholder="chave da API" type="password" value={cfg.apiKey} onChange={e => update(`${key}.apiKey`, e.target.value)} />
                  </div>
                );
              })}
            </section>

            {/* Voz */}
            <section className="hud-panel" style={card}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}><Mic size={14} color="#00f0ff" /> <strong>Voz e conversa</strong></div>
              <div style={{ fontSize: '0.75rem', color: '#7fa8c0' }}>Reconhecimento: {status.stt.status}</div>
              <span style={label}>Seu nome</span>
              <input style={input} value={settings.userName} onChange={e => update('userName', e.target.value)} />
              <label style={{ display: 'flex', gap: 8, fontSize: '0.8rem' }}>
                <input type="checkbox" checked={settings.voice.requireWakeWord} onChange={e => update('voice.requireWakeWord', e.target.checked)} />
                Exigir "Sexta-Feira" para começar a conversa
              </label>
              <span style={label}>Janela para continuar sem chamar de novo: {settings.voice.followUpWindowSeconds}s</span>
              <input type="range" min={5} max={90} value={settings.voice.followUpWindowSeconds} onChange={e => update('voice.followUpWindowSeconds', Number(e.target.value))} />
              <span style={label}>Modelo Whisper (precisão × velocidade)</span>
              <select style={input} value={settings.voice.whisperModel} onChange={e => update('voice.whisperModel', e.target.value)}>
                {['tiny', 'base', 'small', 'medium', 'large-v3-turbo'].map(m => <option key={m}>{m}</option>)}
              </select>
              <span style={label}>Voz neste dispositivo</span>
              <select style={input} value={speaker.pickVoice()?.name ?? ''} onChange={e => update('voice.preferredVoices', [e.target.value, ...settings.voice.preferredVoices.filter((v: string) => v !== e.target.value)])}>
                {voices.length === 0 && <option>Nenhuma voz em português encontrada</option>}
                {voices.map(v => <option key={v.name} value={v.name}>{v.name}</option>)}
              </select>
              <span style={label}>Velocidade da fala: {Number(settings.voice.speechRate).toFixed(2)}×</span>
              <input type="range" min={0.8} max={1.5} step={0.05} value={settings.voice.speechRate} onChange={e => update('voice.speechRate', Number(e.target.value))} />
              <button style={button} onClick={testVoice}>TESTAR VOZ</button>
            </section>

            {/* Dispositivos */}
            <section className="hud-panel" style={card}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}><Smartphone size={14} color="#00f0ff" /> <strong>Dispositivos</strong></div>
              <span style={label}>Nome deste dispositivo</span>
              <input style={input} value={myName} onChange={e => setMyName(e.target.value)} />
              <span style={label}>Conectados agora</span>
              {devices.map(d => (
                <div key={d.deviceId} style={{ fontSize: '0.82rem', display: 'flex', justifyContent: 'space-between' }}>
                  <span>{d.name}</span>
                  <span className={`hud-badge ${d.micActive ? 'hud-badge-listening' : ''}`}>{d.micActive ? 'mic ligado' : 'mic desligado'}</span>
                </div>
              ))}
              <span style={label}>Abra em outro aparelho da mesma rede</span>
              {status.lanUrls.length === 0 && <div style={{ fontSize: '0.78rem', color: '#7fa8c0' }}>O servidor está só neste computador (ListenOnLan=false).</div>}
              {status.lanUrls.map(u => <code key={u} style={{ fontSize: '0.8rem', color: '#00f0ff' }}>{u}</code>)}
              <div style={{ fontSize: '0.72rem', color: '#7fa8c0' }}>O navegador vai avisar que o certificado é autoassinado: toque em "Avançado → Continuar". É ele que libera o microfone na rede local.</div>
            </section>

            {/* MCP */}
            <section className="hud-panel" style={{ ...card, gridColumn: '1 / -1' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}><Plug size={14} color="#00f0ff" /> <strong>Serviços conectados (MCP)</strong>
                <span style={{ fontSize: '0.72rem', color: '#7fa8c0' }}>{status.tools.length} ferramentas disponíveis no total</span></div>
              {status.mcp.length === 0 && <div style={{ fontSize: '0.8rem', color: '#7fa8c0' }}>Nenhum servidor ativo. Edite o JSON abaixo (mesmo formato do Claude Desktop/Cursor) e tire o "_" do nome para ativar.</div>}
              {status.mcp.map(m => (
                <div key={m.name} style={{ fontSize: '0.82rem', display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
                  <strong>{m.name}</strong> <span className="hud-badge">{m.transport}</span>
                  <span className={`hud-badge ${m.state === 'conectado' ? 'hud-badge-success' : m.state === 'erro' ? 'hud-badge-danger' : 'hud-badge-warning'}`}>{m.state}</span>
                  <span style={{ color: '#7fa8c0' }}>{m.detail}</span>
                  {m.pendingLoginUrl && <a href={m.pendingLoginUrl} target="_blank" rel="noreferrer" style={{ color: '#ffd27f' }}>fazer login</a>}
                </div>
              ))}
              <textarea style={{ ...input, minHeight: 180, resize: 'vertical' }} value={mcpText} onChange={e => setMcpText(e.target.value)} spellCheck={false} />
              <div><button style={button} onClick={saveMcp}><Plug size={12} /> SALVAR E RECONECTAR MCP</button></div>
            </section>
          </div>
        )}

        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8, padding: '0 16px 16px' }}>
          <button style={{ ...button, opacity: saving ? 0.6 : 1 }} onClick={save} disabled={saving || !settings}><Save size={12} /> SALVAR CONFIGURAÇÕES</button>
        </div>
      </div>
    </div>
  );
};
