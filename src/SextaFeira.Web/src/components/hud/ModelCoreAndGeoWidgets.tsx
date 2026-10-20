import React, { useEffect, useState } from 'react';
import { Cpu, Server, Activity, Key, Check } from 'lucide-react';

interface ModelCoreWidgetProps {
  activeModel?: string;
  mcpStatus?: string;
  cpuLoad?: string;
  memoryLoad?: string;
}

export const ModelCoreWidget: React.FC<ModelCoreWidgetProps> = () => {
  const [status, setStatus] = useState({
    activeModel: 'Google Gemini 2.0 Flash (Cloud Ultra-Fast)',
    userAccount: 'rafaelwierzba@gmail.com',
    mcpStatus: 'MCP Connected (8 Tools)',
    cpuLoad: '12%',
    memoryLoad: '32%',
    hasGeminiKey: false
  });
  const [showKeyInput, setShowKeyInput] = useState(false);
  const [keyInput, setKeyInput] = useState('');
  const [keySaved, setKeySaved] = useState(false);

  useEffect(() => {
    fetch('http://localhost:5000/api/core/status')
      .then((res) => res.json())
      .then((data) => {
        if (data) {
          setStatus({
            activeModel: data.activeModel || 'Google Gemini 2.0 Flash',
            userAccount: data.userAccount || 'rafaelwierzba@gmail.com',
            mcpStatus: data.mcpOrchestrator || 'MCP Connected (8 Tools)',
            cpuLoad: data.cpuUsage || '12%',
            memoryLoad: data.memoryUsage || '32%',
            hasGeminiKey: !!data.hasGeminiKey
          });
        }
      })
      .catch(() => {});
  }, [keySaved]);

  const handleSaveKey = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!keyInput.trim()) return;

    try {
      const res = await fetch('http://localhost:5000/api/core/config/gemini-key', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ apiKey: keyInput.trim() })
      });
      if (res.ok) {
        setKeySaved(true);
        setShowKeyInput(false);
        setKeyInput('');
      }
    } catch {
      //
    }
  };

  return (
    <div className="hud-panel" style={{ width: '330px' }}>
      <div className="hud-panel-header">
        <span style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
          <Cpu size={14} color="#00f0ff" /> GOOGLE GEMINI CLOUD ENGINE
        </span>
        <button
          onClick={() => setShowKeyInput(!showKeyInput)}
          style={{
            background: 'transparent',
            border: 'none',
            color: '#00f0ff',
            cursor: 'pointer',
            display: 'flex',
            alignItems: 'center',
            gap: '4px',
            fontSize: '0.65rem',
            fontFamily: 'var(--hud-font-orbitron)'
          }}
        >
          <Key size={11} /> API KEY
        </button>
      </div>

      <div style={{ display: 'flex', flexDirection: 'column', gap: '8px', fontSize: '0.8rem' }}>
        <div>
          <span style={{ color: 'rgba(0, 240, 255, 0.7)', fontSize: '0.7rem' }}>CONTA & NÚCLEO ATIVO:</span>
          <div style={{ color: '#fff', fontWeight: 600, fontFamily: 'var(--hud-font-orbitron)', fontSize: '0.75rem', marginTop: '2px' }}>
            {status.activeModel}
          </div>
          <div style={{ color: 'rgba(0, 255, 136, 0.8)', fontSize: '0.68rem', fontFamily: 'var(--hud-font-mono)' }}>
            User: {status.userAccount}
          </div>
        </div>

        {showKeyInput && (
          <form onSubmit={handleSaveKey} style={{ display: 'flex', gap: '4px', marginTop: '4px' }}>
            <input
              type="password"
              placeholder="Inserir Gemini API Key..."
              value={keyInput}
              onChange={(e) => setKeyInput(e.target.value)}
              style={{
                flex: 1,
                background: 'rgba(0, 0, 0, 0.6)',
                border: '1px solid #00f0ff',
                color: '#fff',
                padding: '4px 6px',
                fontSize: '0.7rem',
                borderRadius: '3px'
              }}
            />
            <button
              type="submit"
              style={{
                background: '#00f0ff',
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

        <div>
          <span style={{ color: 'rgba(0, 240, 255, 0.7)', fontSize: '0.7rem' }}>BARRAMENTO MCP:</span>
          <div style={{ color: '#00f0ff', display: 'flex', alignItems: 'center', gap: '6px', marginTop: '2px' }}>
            <Server size={13} /> {status.mcpStatus}
          </div>
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '8px', marginTop: '4px' }}>
          <div style={{ background: 'rgba(0, 240, 255, 0.05)', padding: '6px', borderRadius: '4px', border: '1px solid rgba(0, 240, 255, 0.15)' }}>
            <div style={{ fontSize: '0.65rem', color: 'rgba(0, 240, 255, 0.7)' }}>CPU LOAD</div>
            <div style={{ fontSize: '1rem', fontWeight: 700, color: '#00f0ff', fontFamily: 'var(--hud-font-mono)' }}>{status.cpuLoad}</div>
          </div>
          <div style={{ background: 'rgba(0, 240, 255, 0.05)', padding: '6px', borderRadius: '4px', border: '1px solid rgba(0, 240, 255, 0.15)' }}>
            <div style={{ fontSize: '0.65rem', color: 'rgba(0, 240, 255, 0.7)' }}>RAM ALLOCATED</div>
            <div style={{ fontSize: '1rem', fontWeight: 700, color: '#00ff88', fontFamily: 'var(--hud-font-mono)' }}>{status.memoryLoad}</div>
          </div>
        </div>
      </div>
    </div>
  );
};

interface GeoLocationData {
  city: string;
  region: string;
  country: string;
  latitude: number;
  longitude: number;
  formattedAddress: string;
  weather: {
    temp: number;
    condition: string;
    humidity: number;
  };
}

export const GeolocationWidget: React.FC = () => {
  const [geoData, setGeoData] = useState<GeoLocationData>({
    city: 'Detectando...',
    region: 'SP',
    country: 'Brasil',
    latitude: -23.0544,
    longitude: -46.3589,
    formattedAddress: 'Piracaia, SP',
    weather: {
      temp: 24,
      condition: 'Sunny',
      humidity: 58
    }
  });

  const [source, setSource] = useState<string>('GPS LOCKING');

  useEffect(() => {
    if (navigator.geolocation) {
      navigator.geolocation.getCurrentPosition(
        async (position) => {
          const lat = position.coords.latitude;
          const lon = position.coords.longitude;

          try {
            const res = await fetch(`https://nominatim.openstreetmap.org/reverse?lat=${lat}&lon=${lon}&format=json`);
            const json = await res.json();
            const city = json.address?.city || json.address?.town || json.address?.municipality || 'Localização';
            const state = json.address?.state || 'SP';

            const weatherRes = await fetch(`https://api.open-meteo.com/v1/forecast?latitude=${lat}&longitude=${lon}&current=temperature_2m,relative_humidity_2m`);
            const weatherJson = await weatherRes.json();
            const currentTemp = weatherJson.current?.temperature_2m ?? 24;
            const currentHumidity = weatherJson.current?.relative_humidity_2m ?? 58;

            setGeoData({
              city,
              region: state,
              country: json.address?.country || 'Brasil',
              latitude: lat,
              longitude: lon,
              formattedAddress: `${city}, ${state}`,
              weather: {
                temp: currentTemp,
                condition: currentTemp > 22 ? 'Ensolarado' : 'Ameno',
                humidity: currentHumidity
              }
            });
            setSource('GPS LIVE');
          } catch {
            setGeoData((prev) => ({ ...prev, latitude: lat, longitude: lon, formattedAddress: `${lat.toFixed(4)}, ${lon.toFixed(4)}` }));
            setSource('GPS NATIVO');
          }
        },
        async () => {
          try {
            const ipRes = await fetch('http://ip-api.com/json/');
            const ipJson = await ipRes.json();
            if (ipJson.status === 'success') {
              setGeoData({
                city: ipJson.city,
                region: ipJson.regionName,
                country: ipJson.country,
                latitude: ipJson.lat,
                longitude: ipJson.lon,
                formattedAddress: `${ipJson.city}, ${ipJson.regionName}`,
                weather: {
                  temp: 24,
                  condition: 'Ensolarado',
                  humidity: 58
                }
              });
              setSource('IP LIVE');
            }
          } catch {
            setSource('SPATIAL BASE');
          }
        },
        { timeout: 8000, enableHighAccuracy: true }
      );
    }
  }, []);

  return (
    <div className="hud-panel" style={{ width: '290px' }}>
      <div className="hud-panel-header">
        <span style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
          <Activity size={14} color="#00f0ff" /> SPATIAL RADAR & CLIMA
        </span>
        <span className="hud-badge hud-badge-cyan">{source}</span>
      </div>

      <div style={{ display: 'flex', flexDirection: 'column', gap: '6px', fontSize: '0.8rem' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <span style={{ color: 'rgba(0, 240, 255, 0.7)' }}>GEOLOCATION:</span>
          <span style={{ color: '#fff', fontWeight: 600 }}>{geoData.formattedAddress}</span>
        </div>

        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <span style={{ color: 'rgba(0, 240, 255, 0.7)' }}>COORDENADAS:</span>
          <span style={{ color: '#00f0ff', fontFamily: 'var(--hud-font-mono)', fontSize: '0.75rem' }}>
            {geoData.latitude.toFixed(4)}, {geoData.longitude.toFixed(4)}
          </span>
        </div>

        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <span style={{ color: 'rgba(0, 240, 255, 0.7)' }}>WEATHER:</span>
          <span style={{ color: '#ffaa00', fontWeight: 600 }}>
            {geoData.weather.condition}, {geoData.weather.temp}°C ({geoData.weather.humidity}%)
          </span>
        </div>

        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <span style={{ color: 'rgba(0, 240, 255, 0.7)' }}>RADAR ENGINE:</span>
          <span style={{ color: '#bce9ff', fontSize: '0.75rem' }}>Active Radar v1.2 (Live Stream)</span>
        </div>
      </div>
    </div>
  );
};
