import React, { useEffect, useState } from 'react';
import { Cpu, Server, Activity } from 'lucide-react';

interface ModelCoreWidgetProps {
  activeModel?: string;
  mcpStatus?: string;
  cpuLoad?: string;
  memoryLoad?: string;
}

export const ModelCoreWidget: React.FC<ModelCoreWidgetProps> = ({
  activeModel = 'Phi-3 Mini (Local) - Active (Med. Load)',
  mcpStatus = 'MCP Connected (8 Tools)',
  cpuLoad = '18%',
  memoryLoad = '42%'
}) => {
  return (
    <div className="hud-panel" style={{ width: '310px' }}>
      <div className="hud-panel-header">
        <span style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
          <Cpu size={14} color="#00f0ff" /> MODEL CORE & ORCHESTRATOR
        </span>
        <span className="hud-badge hud-badge-active">ONLINE</span>
      </div>

      <div style={{ display: 'flex', flexDirection: 'column', gap: '8px', fontSize: '0.8rem' }}>
        <div>
          <span style={{ color: 'rgba(0, 240, 255, 0.7)', fontSize: '0.7rem' }}>ACTIVE LLM ENGINE:</span>
          <div style={{ color: '#fff', fontWeight: 600, fontFamily: 'var(--hud-font-orbitron)', fontSize: '0.75rem', marginTop: '2px' }}>
            {activeModel}
          </div>
        </div>

        <div>
          <span style={{ color: 'rgba(0, 240, 255, 0.7)', fontSize: '0.7rem' }}>ORCHESTRATOR BUS:</span>
          <div style={{ color: '#00f0ff', display: 'flex', alignItems: 'center', gap: '6px', marginTop: '2px' }}>
            <Server size={13} /> {mcpStatus}
          </div>
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '8px', marginTop: '4px' }}>
          <div style={{ background: 'rgba(0, 240, 255, 0.05)', padding: '6px', borderRadius: '4px', border: '1px solid rgba(0, 240, 255, 0.15)' }}>
            <div style={{ fontSize: '0.65rem', color: 'rgba(0, 240, 255, 0.7)' }}>CPU LOAD</div>
            <div style={{ fontSize: '1rem', fontWeight: 700, color: '#00f0ff', fontFamily: 'var(--hud-font-mono)' }}>{cpuLoad}</div>
          </div>
          <div style={{ background: 'rgba(0, 240, 255, 0.05)', padding: '6px', borderRadius: '4px', border: '1px solid rgba(0, 240, 255, 0.15)' }}>
            <div style={{ fontSize: '0.65rem', color: 'rgba(0, 240, 255, 0.7)' }}>RAM ALLOCATED</div>
            <div style={{ fontSize: '1rem', fontWeight: 700, color: '#00ff88', fontFamily: 'var(--hud-font-mono)' }}>{memoryLoad}</div>
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
    // 1. Tentar obter coordenadas do GPS do navegador em tempo real
    if (navigator.geolocation) {
      navigator.geolocation.getCurrentPosition(
        async (position) => {
          const lat = position.coords.latitude;
          const lon = position.coords.longitude;

          try {
            // Chamar endpoint do backend ou OpenStreetMap direto
            const res = await fetch(`https://nominatim.openstreetmap.org/reverse?lat=${lat}&lon=${lon}&format=json`);
            const json = await res.json();
            const city = json.address?.city || json.address?.town || json.address?.municipality || 'Localização';
            const state = json.address?.state || 'SP';

            // Consultar Open-Meteo em tempo real
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
                condition: currentTemp > 22 ? 'Sunny' : 'Mild',
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
          // Fallback para IP Geolocation em tempo real
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
          <span style={{ color: 'rgba(0, 240, 255, 0.7)' }}>MAP ENGINE:</span>
          <span style={{ color: '#bce9ff', fontSize: '0.75rem' }}>Active Radar v1.2 (Live Stream)</span>
        </div>
      </div>
    </div>
  );
};
