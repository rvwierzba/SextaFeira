import React, { useState, useEffect } from 'react';
import { Wifi, Calendar, Mail, Folder, HardDrive, Music, Play, Pause, RefreshCw, Radio } from 'lucide-react';

interface NetworkDevice {
  ip: string;
  mac: string;
  hostname: string;
  type: string;
  status: 'Active' | 'Idle' | 'Offline';
  latencyMs: number;
}

const defaultDevices: NetworkDevice[] = [
  { ip: '192.168.1.1', mac: 'AC:84:C6:71:2B:01', hostname: 'Gateway Wi-Fi 6', type: 'Router', status: 'Active', latencyMs: 1 },
  { ip: '192.168.1.100', mac: 'E8:DB:84:44:12:34', hostname: 'Minha Smart TV', type: 'TV', status: 'Active', latencyMs: 8 },
  { ip: '192.168.1.101', mac: 'B8:27:EB:99:88:77', hostname: 'Watch-01', type: 'Wearable', status: 'Active', latencyMs: 14 },
  { ip: '192.168.1.102', mac: '50:C7:BF:33:44:55', hostname: 'Dispositivo IoT (Luz)', type: 'IoT', status: 'Active', latencyMs: 4 },
  { ip: '192.168.1.105', mac: '70:85:C2:91:FA:10', hostname: 'Windows Desktop', type: 'PC', status: 'Active', latencyMs: 0 }
];

export const NetworkMapWidget: React.FC = () => {
  const [devices, setDevices] = useState<NetworkDevice[]>(defaultDevices);
  const [isScanning, setIsScanning] = useState(false);

  const scanNetwork = async () => {
    setIsScanning(true);
    try {
      const res = await fetch('http://localhost:5000/api/network/devices').catch(() => null);
      if (res && res.ok) {
        const data = await res.json();
        if (Array.isArray(data) && data.length > 0) {
          setDevices(data);
        }
      }
    } catch {
      // Keep existing devices
    } finally {
      setTimeout(() => setIsScanning(false), 800);
    }
  };

  return (
    <div className="hud-panel" style={{ width: '420px' }}>
      <div className="hud-panel-header">
        <span style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
          <Wifi size={14} color="#00f0ff" /> NETWORK MAP (WI-FI 6 / SCANNER)
        </span>
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
          <button
            onClick={scanNetwork}
            disabled={isScanning}
            title="Escanear rede local"
            style={{
              background: 'transparent',
              border: 'none',
              color: '#00f0ff',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              padding: '2px 4px'
            }}
          >
            <RefreshCw size={12} className={isScanning ? 'hud-pulse-glow' : ''} />
          </button>
          <span className="hud-badge hud-badge-active">{devices.length} NODES</span>
        </div>
      </div>

      <table className="hud-table">
        <thead>
          <tr>
            <th className="col-text">Dispositivo / Host</th>
            <th className="col-numeric">Latência</th>
            <th className="col-status">Status</th>
          </tr>
        </thead>
        <tbody>
          {devices.map((d, i) => (
            <tr key={i}>
              <td className="col-text">
                <div style={{ fontWeight: 600, color: '#fff' }}>{d.hostname}</div>
                <div style={{ fontSize: '0.68rem', color: 'rgba(0, 240, 255, 0.6)', fontFamily: 'var(--hud-font-mono)' }}>{d.ip}</div>
              </td>
              <td className="col-numeric">
                <span style={{ color: d.latencyMs < 10 ? '#00ff88' : '#ffaa00' }}>
                  {d.latencyMs} ms
                </span>
              </td>
              <td className="col-status">
                <span className="hud-badge hud-badge-connected">CONNECT</span>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
};

export const PersonalServicesWidget: React.FC = () => {
  const [isPlaying, setIsPlaying] = useState(true);
  const [calendarEvents, setCalendarEvents] = useState([
    { title: 'Alinhamento Arquitetura IA', time: '18:00', status: 'CONF' },
    { title: 'Review Sprint MCP', time: '19:30', status: 'PEND' }
  ]);
  const [emails, setEmails] = useState([
    { subject: 'Relatório Telemetria Core', received: '13:30 AM' },
    { subject: 'Atualização Modelos ONNX', received: '11:15 AM' }
  ]);

  const loadPersonalData = async () => {
    try {
      const calRes = await fetch('http://localhost:5000/api/personal/calendar').catch(() => null);
      if (calRes && calRes.ok) {
        const calData = await calRes.json();
        if (Array.isArray(calData) && calData.length > 0) {
          setCalendarEvents(
            calData.map((e: any) => ({
              title: e.title,
              time: new Date(e.startTime).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
              status: e.status?.substring(0, 4).toUpperCase() || 'CONF'
            }))
          );
        }
      }

      const emailRes = await fetch('http://localhost:5000/api/personal/emails').catch(() => null);
      if (emailRes && emailRes.ok) {
        const mailData = await emailRes.json();
        if (Array.isArray(mailData) && mailData.length > 0) {
          setEmails(
            mailData.map((m: any) => ({
              subject: m.subject,
              received: new Date(m.receivedAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
            }))
          );
        }
      }
    } catch {
      // Keep state
    }
  };

  useEffect(() => {
    loadPersonalData();
  }, []);

  const togglePlayback = () => {
    setIsPlaying(!isPlaying);
  };

  return (
    <div className="hud-panel" style={{ width: '380px' }}>
      <div className="hud-panel-header">
        <span style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
          <HardDrive size={14} color="#00f0ff" /> SERVIÇOS PESSOAIS & NUVEM
        </span>
        <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
          <button
            onClick={loadPersonalData}
            title="Sincronizar serviços pessoais"
            style={{
              background: 'transparent',
              border: 'none',
              color: '#00f0ff',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              padding: '2px 4px'
            }}
          >
            <RefreshCw size={12} />
          </button>
          <span className="hud-badge hud-badge-cyan">SYNCED</span>
        </div>
      </div>

      <div style={{ display: 'flex', flexDirection: 'column', gap: '10px' }}>
        {/* Google Agenda */}
        <div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '6px', color: '#ffaa00', fontSize: '0.72rem', fontFamily: 'var(--hud-font-orbitron)', marginBottom: '4px' }}>
            <Calendar size={13} /> GOOGLE AGENDA
          </div>
          <table className="hud-table">
            <thead>
              <tr>
                <th className="col-text">Compromisso</th>
                <th className="col-date">Horário</th>
                <th className="col-status">Status</th>
              </tr>
            </thead>
            <tbody>
              {calendarEvents.map((evt, idx) => (
                <tr key={idx}>
                  <td className="col-text">{evt.title}</td>
                  <td className="col-date" style={{ fontFamily: 'var(--hud-font-mono)' }}>{evt.time}</td>
                  <td className="col-status">
                    <span className={`hud-badge ${evt.status === 'CONF' ? 'hud-badge-active' : 'hud-badge-warning'}`}>
                      {evt.status}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {/* Gmail Recentes */}
        <div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '6px', color: '#00f0ff', fontSize: '0.72rem', fontFamily: 'var(--hud-font-orbitron)', marginBottom: '4px' }}>
            <Mail size={13} /> G-MAIL (INBOX)
          </div>
          <table className="hud-table">
            <thead>
              <tr>
                <th className="col-text">Assunto</th>
                <th className="col-date">Recebido</th>
              </tr>
            </thead>
            <tbody>
              {emails.map((mail, idx) => (
                <tr key={idx}>
                  <td className="col-text">{mail.subject}</td>
                  <td className="col-date" style={{ fontFamily: 'var(--hud-font-mono)' }}>{mail.received}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {/* Spotify Integration */}
        <div>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', color: '#1db954', fontSize: '0.72rem', fontFamily: 'var(--hud-font-orbitron)', marginBottom: '4px' }}>
            <span style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
              <Music size={13} color="#1db954" /> SPOTIFY (PLAYBACK MCP)
            </span>
            <span className="hud-badge" style={{ background: 'rgba(29, 185, 84, 0.15)', color: '#1db954', borderColor: '#1db954' }}>
              {isPlaying ? 'LIVE' : 'PAUSADO'}
            </span>
          </div>
          <div style={{ background: 'rgba(29, 185, 84, 0.08)', border: '1px solid rgba(29, 185, 84, 0.3)', borderRadius: '4px', padding: '6px 10px', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
            <div>
              <div style={{ fontWeight: 700, color: '#fff', fontSize: '0.78rem' }}>Starboy</div>
              <div style={{ fontSize: '0.68rem', color: 'rgba(255, 255, 255, 0.7)' }}>The Weeknd ft. Daft Punk</div>
            </div>
            <button
              onClick={togglePlayback}
              style={{
                background: isPlaying ? '#1db954' : 'transparent',
                border: '1px solid #1db954',
                color: isPlaying ? '#000' : '#1db954',
                borderRadius: '3px',
                padding: '4px 8px',
                cursor: 'pointer',
                fontSize: '0.65rem',
                fontFamily: 'var(--hud-font-orbitron)',
                fontWeight: 700,
                display: 'flex',
                alignItems: 'center',
                gap: '4px'
              }}
            >
              {isPlaying ? <Pause size={10} /> : <Play size={10} />}
              {isPlaying ? 'PAUSAR' : 'TOCAR'}
            </button>
          </div>
        </div>

        {/* OneDrive Files */}
        <div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '6px', color: '#00ff88', fontSize: '0.72rem', fontFamily: 'var(--hud-font-orbitron)', marginBottom: '4px' }}>
            <Folder size={13} /> ONEDRIVE (FILES)
          </div>
          <table className="hud-table">
            <thead>
              <tr>
                <th className="col-text">Arquivo / Diretório</th>
                <th className="col-numeric">Tamanho</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td className="col-text">📁 /Projetos/SextaFeira.sln</td>
                <td className="col-numeric">4.1 KB</td>
              </tr>
              <tr>
                <td className="col-text">📄 Arquitetura-Holografica.pdf</td>
                <td className="col-numeric">10.4 MB</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
};
