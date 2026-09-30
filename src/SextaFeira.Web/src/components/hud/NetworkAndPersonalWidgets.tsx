import React from 'react';
import { Wifi, Calendar, Mail, Folder, HardDrive, Music } from 'lucide-react';

interface NetworkDevice {
  ip: string;
  mac: string;
  hostname: string;
  type: string;
  status: 'Active' | 'Idle' | 'Offline';
  latencyMs: number;
}

const mockDevices: NetworkDevice[] = [
  { ip: '192.168.1.1', mac: 'AC:84:C6:71:2B:01', hostname: 'Gateway Wi-Fi 6', type: 'Router', status: 'Active', latencyMs: 1 },
  { ip: '192.168.1.100', mac: 'E8:DB:84:44:12:34', hostname: 'Minha Smart TV', type: 'TV', status: 'Active', latencyMs: 8 },
  { ip: '192.168.1.101', mac: 'B8:27:EB:99:88:77', hostname: 'Watch-01', type: 'Wearable', status: 'Active', latencyMs: 14 },
  { ip: '192.168.1.102', mac: '50:C7:BF:33:44:55', hostname: 'Dispositivo IoT (Luz)', type: 'IoT', status: 'Active', latencyMs: 4 },
  { ip: '192.168.1.105', mac: '70:85:C2:91:FA:10', hostname: 'Windows Desktop', type: 'PC', status: 'Active', latencyMs: 0 }
];

export const NetworkMapWidget: React.FC = () => {
  return (
    <div className="hud-panel" style={{ width: '420px' }}>
      <div className="hud-panel-header">
        <span style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
          <Wifi size={14} color="#00f0ff" /> NETWORK MAP (WI-FI 6 / SCANNER)
        </span>
        <span className="hud-badge hud-badge-active">5 NODES</span>
      </div>

      <table className="hud-table">
        <thead>
          <tr>
            {/* Texto: Alinhamento à esquerda */}
            <th className="col-text">Dispositivo / Host</th>
            {/* Numérico: Alinhamento à direita */}
            <th className="col-numeric">Latência</th>
            {/* Status: Alinhamento centralizado */}
            <th className="col-status">Status</th>
          </tr>
        </thead>
        <tbody>
          {mockDevices.map((d, i) => (
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
  return (
    <div className="hud-panel" style={{ width: '380px' }}>
      <div className="hud-panel-header">
        <span style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
          <HardDrive size={14} color="#00f0ff" /> SERVIÇOS PESSOAIS & NUVEM
        </span>
        <span className="hud-badge hud-badge-cyan">SYNCED</span>
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
              <tr>
                <td className="col-text">Alinhamento Arquitetura IA</td>
                <td className="col-date" style={{ fontFamily: 'var(--hud-font-mono)' }}>18:00</td>
                <td className="col-status"><span className="hud-badge hud-badge-active">CONF</span></td>
              </tr>
              <tr>
                <td className="col-text">Review Sprint MCP</td>
                <td className="col-date" style={{ fontFamily: 'var(--hud-font-mono)' }}>19:30</td>
                <td className="col-status"><span className="hud-badge hud-badge-warning">PEND</span></td>
              </tr>
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
              <tr>
                <td className="col-text">Telemetria Core Sexta-Feira</td>
                <td className="col-date" style={{ fontFamily: 'var(--hud-font-mono)' }}>13:30 AM</td>
              </tr>
              <tr>
                <td className="col-text">Atualização Modelos ONNX</td>
                <td className="col-date" style={{ fontFamily: 'var(--hud-font-mono)' }}>11:15 AM</td>
              </tr>
            </tbody>
          </table>
        </div>

        {/* Spotify Integration */}
        <div>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', color: '#1db954', fontSize: '0.72rem', fontFamily: 'var(--hud-font-orbitron)', marginBottom: '4px' }}>
            <span style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
              <Music size={13} color="#1db954" /> SPOTIFY (PLAYBACK MCP)
            </span>
            <span className="hud-badge" style={{ background: 'rgba(29, 185, 84, 0.15)', color: '#1db954', borderColor: '#1db954' }}>LIVE</span>
          </div>
          <div style={{ background: 'rgba(29, 185, 84, 0.08)', border: '1px solid rgba(29, 185, 84, 0.3)', borderRadius: '4px', padding: '6px 10px', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
            <div>
              <div style={{ fontWeight: 700, color: '#fff', fontSize: '0.78rem' }}>Starboy</div>
              <div style={{ fontSize: '0.68rem', color: 'rgba(255, 255, 255, 0.7)' }}>The Weeknd ft. Daft Punk</div>
            </div>
            <span className="hud-badge hud-badge-active" style={{ background: '#1db954', color: '#000', fontWeight: 800 }}>TOCANDO</span>
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
