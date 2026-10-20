import React, { useState, useEffect } from 'react';
import { Wifi, Calendar, Mail, Folder, HardDrive, Music, Play, Pause, RefreshCw, Plus, Check } from 'lucide-react';

interface NetworkDevice {
  ipAddress: string;
  macAddress: string;
  hostname: string;
  deviceType: string;
  status: 'Active' | 'Idle' | 'Offline';
  latencyMs: number;
}

export const NetworkMapWidget: React.FC = () => {
  const [devices, setDevices] = useState<NetworkDevice[]>([]);
  const [isScanning, setIsScanning] = useState(false);

  const scanNetwork = async () => {
    setIsScanning(true);
    try {
      const res = await fetch('http://localhost:5000/api/network/devices').catch(() => null);
      if (res && res.ok) {
        const data = await res.json();
        if (Array.isArray(data)) {
          setDevices(data);
        }
      }
    } catch {
      // Keep state
    } finally {
      setIsScanning(false);
    }
  };

  useEffect(() => {
    scanNetwork();
  }, []);

  return (
    <div className="hud-panel" style={{ width: '420px' }}>
      <div className="hud-panel-header">
        <span style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
          <Wifi size={14} color="#00f0ff" /> NETWORK MAP (WI-FI 6 / LOCAL SCANNER)
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
          <span className="hud-badge hud-badge-active">{devices.length} NÓS ATIVOS</span>
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
                <div style={{ fontWeight: 600, color: '#fff' }}>{d.hostname || 'Adaptador Local'}</div>
                <div style={{ fontSize: '0.68rem', color: 'rgba(0, 240, 255, 0.6)', fontFamily: 'var(--hud-font-mono)' }}>
                  {d.ipAddress || '127.0.0.1'} ({d.deviceType || 'PC'})
                </div>
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
          {devices.length === 0 && (
            <tr>
              <td colSpan={3} style={{ textAlign: 'center', color: 'rgba(0, 240, 255, 0.5)', padding: '12px' }}>
                {isScanning ? 'Varrendo adaptadores e nós locais...' : 'Nenhum dispositivo encontrado. Clique para escanear.'}
              </td>
            </tr>
          )}
        </tbody>
      </table>
    </div>
  );
};

interface CalendarEvent {
  id?: string;
  title: string;
  time: string;
  status: string;
}

interface EmailItem {
  subject: string;
  fromAddress?: string;
  received: string;
}

interface SpotifyTrack {
  id: string;
  title: string;
  artist: string;
  album: string;
  durationMs: number;
  isPlaying: boolean;
  contextUri: string;
}

export const PersonalServicesWidget: React.FC = () => {
  const [calendarEvents, setCalendarEvents] = useState<CalendarEvent[]>([]);
  const [emails, setEmails] = useState<EmailItem[]>([]);
  const [oneDriveFiles, setOneDriveFiles] = useState<{ name: string; size: string }[]>([]);
  const [spotifyTrack, setSpotifyTrack] = useState<SpotifyTrack>({
    id: 'spot-001',
    title: 'Get Lucky',
    artist: 'Daft Punk ft. Pharrell Williams',
    album: 'Random Access Memories',
    durationMs: 248000,
    isPlaying: true,
    contextUri: 'https://open.spotify.com/embed/track/2Foc5Q5nqNiosCNqttRgAV'
  });
  const [showAddEvent, setShowAddEvent] = useState(false);
  const [newEventTitle, setNewEventTitle] = useState('');
  const [newEventTime, setNewEventTime] = useState('15:00');

  const loadPersonalData = async () => {
    try {
      const calRes = await fetch('http://localhost:5000/api/personal/calendar').catch(() => null);
      if (calRes && calRes.ok) {
        const calData = await calRes.json();
        if (Array.isArray(calData)) {
          setCalendarEvents(
            calData.map((e: any) => ({
              id: e.id,
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
        if (Array.isArray(mailData)) {
          setEmails(
            mailData.map((m: any) => ({
              subject: m.subject,
              fromAddress: m.fromAddress,
              received: new Date(m.receivedAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
            }))
          );
        }
      }

      const driveRes = await fetch('http://localhost:5000/api/personal/onedrive').catch(() => null);
      if (driveRes && driveRes.ok) {
        const driveData = await driveRes.json();
        if (Array.isArray(driveData)) {
          setOneDriveFiles(
            driveData.map((f: any) => ({
              name: f.name,
              size: f.sizeInBytes > 0 ? `${(f.sizeInBytes / 1024).toFixed(1)} KB` : 'Diretório'
            }))
          );
        }
      }

      const spotRes = await fetch('http://localhost:5000/api/personal/spotify').catch(() => null);
      if (spotRes && spotRes.ok) {
        const spotData = await spotRes.json();
        if (spotData && spotData.title) {
          setSpotifyTrack(spotData);
        }
      }
    } catch {
      // Keep state
    }
  };

  useEffect(() => {
    loadPersonalData();
  }, []);

  const handleAddCalendarEvent = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newEventTitle.trim()) return;

    const [hours, minutes] = newEventTime.split(':').map(Number);
    const start = new Date();
    start.setHours(hours || 14, minutes || 0, 0, 0);
    const end = new Date(start.getTime() + 60 * 60 * 1000);

    try {
      const res = await fetch('http://localhost:5000/api/personal/calendar', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          title: newEventTitle.trim(),
          description: 'Criado via Sexta-Feira HUD',
          startTime: start.toISOString(),
          endTime: end.toISOString(),
          location: 'Google Meet',
          status: 'Confirmado'
        })
      });
      if (res.ok) {
        setNewEventTitle('');
        setShowAddEvent(false);
        loadPersonalData();
      }
    } catch {
      // Fallback local
    }
  };

  const playSong = async (query: string) => {
    try {
      const res = await fetch('http://localhost:5000/api/personal/spotify/play', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ query })
      });
      if (res.ok) {
        const data = await res.json();
        if (data.currentTrack) {
          setSpotifyTrack(data.currentTrack);
        }
      }
    } catch {
      // Fallback
    }
  };

  return (
    <div className="hud-panel" style={{ width: '400px' }}>
      <div className="hud-panel-header">
        <span style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
          <HardDrive size={14} color="#00f0ff" /> GOOGLE & NUVEM (rafaelwierzba@gmail.com)
        </span>
        <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
          <button
            onClick={loadPersonalData}
            title="Sincronizar serviços"
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
          <span className="hud-badge hud-badge-cyan">LIVE CLOUD</span>
        </div>
      </div>

      <div style={{ display: 'flex', flexDirection: 'column', gap: '10px' }}>
        {/* Google Agenda */}
        <div>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '4px' }}>
            <span style={{ display: 'flex', alignItems: 'center', gap: '6px', color: '#ffaa00', fontSize: '0.72rem', fontFamily: 'var(--hud-font-orbitron)' }}>
              <Calendar size={13} /> GOOGLE AGENDA
            </span>
            <button
              onClick={() => setShowAddEvent(!showAddEvent)}
              style={{
                background: 'rgba(255, 170, 0, 0.15)',
                border: '1px solid #ffaa00',
                color: '#ffaa00',
                borderRadius: '3px',
                padding: '2px 6px',
                fontSize: '0.62rem',
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                gap: '2px'
              }}
            >
              <Plus size={10} /> NOVO
            </button>
          </div>

          {showAddEvent && (
            <form onSubmit={handleAddCalendarEvent} style={{ display: 'flex', gap: '4px', marginBottom: '6px' }}>
              <input
                type="text"
                placeholder="Título do compromisso..."
                value={newEventTitle}
                onChange={(e) => setNewEventTitle(e.target.value)}
                style={{
                  flex: 1,
                  background: 'rgba(0, 0, 0, 0.6)',
                  border: '1px solid rgba(255, 170, 0, 0.4)',
                  color: '#fff',
                  padding: '4px 6px',
                  fontSize: '0.72rem',
                  borderRadius: '3px'
                }}
              />
              <input
                type="time"
                value={newEventTime}
                onChange={(e) => setNewEventTime(e.target.value)}
                style={{
                  width: '65px',
                  background: 'rgba(0, 0, 0, 0.6)',
                  border: '1px solid rgba(255, 170, 0, 0.4)',
                  color: '#fff',
                  padding: '4px',
                  fontSize: '0.72rem',
                  borderRadius: '3px'
                }}
              />
              <button
                type="submit"
                style={{
                  background: '#ffaa00',
                  border: 'none',
                  color: '#000',
                  fontWeight: 700,
                  padding: '4px 8px',
                  borderRadius: '3px',
                  cursor: 'pointer'
                }}
              >
                <Check size={12} />
              </button>
            </form>
          )}

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
              {calendarEvents.length === 0 && (
                <tr>
                  <td colSpan={3} style={{ textAlign: 'center', color: 'rgba(255, 170, 0, 0.5)', padding: '6px' }}>
                    Nenhum compromisso agendado.
                  </td>
                </tr>
              )}
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
              {emails.length === 0 && (
                <tr>
                  <td colSpan={2} style={{ textAlign: 'center', color: 'rgba(0, 240, 255, 0.5)', padding: '6px' }}>
                    Caixa de entrada limpa.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>

        {/* Spotify Integration (Player com Embed Iframe Real) */}
        <div>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', color: '#1db954', fontSize: '0.72rem', fontFamily: 'var(--hud-font-orbitron)', marginBottom: '4px' }}>
            <span style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
              <Music size={13} color="#1db954" /> SPOTIFY REAL EMBED (0 API KEYS)
            </span>
            <div style={{ display: 'flex', gap: '4px' }}>
              <button
                onClick={() => playSong('Daft Punk')}
                style={{ background: 'rgba(29, 185, 84, 0.1)', border: '1px solid #1db954', color: '#1db954', fontSize: '0.58rem', padding: '1px 4px', borderRadius: '2px', cursor: 'pointer' }}
              >
                Daft Punk
              </button>
              <button
                onClick={() => playSong('The Weeknd')}
                style={{ background: 'rgba(29, 185, 84, 0.1)', border: '1px solid #1db954', color: '#1db954', fontSize: '0.58rem', padding: '1px 4px', borderRadius: '2px', cursor: 'pointer' }}
              >
                Weeknd
              </button>
              <button
                onClick={() => playSong('Synthwave')}
                style={{ background: 'rgba(29, 185, 84, 0.1)', border: '1px solid #1db954', color: '#1db954', fontSize: '0.58rem', padding: '1px 4px', borderRadius: '2px', cursor: 'pointer' }}
              >
                Synthwave
              </button>
            </div>
          </div>

          <div style={{ borderRadius: '6px', overflow: 'hidden', border: '1px solid rgba(29, 185, 84, 0.4)', background: '#121212' }}>
            <iframe
              src={spotifyTrack.contextUri || 'https://open.spotify.com/embed/track/2Foc5Q5nqNiosCNqttRgAV'}
              width="100%"
              height="80"
              frameBorder="0"
              allow="autoplay; clipboard-write; encrypted-media; fullscreen; picture-in-picture"
              loading="lazy"
              title="Spotify Web Player"
            />
          </div>
        </div>

        {/* OneDrive Files */}
        <div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '6px', color: '#00ff88', fontSize: '0.72rem', fontFamily: 'var(--hud-font-orbitron)', marginBottom: '4px' }}>
            <Folder size={13} /> ONEDRIVE / PROJETOS LOCAIS
          </div>
          <table className="hud-table">
            <thead>
              <tr>
                <th className="col-text">Arquivo / Diretório</th>
                <th className="col-numeric">Tamanho</th>
              </tr>
            </thead>
            <tbody>
              {oneDriveFiles.map((file, idx) => (
                <tr key={idx}>
                  <td className="col-text">{file.name}</td>
                  <td className="col-numeric">{file.size}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
};
