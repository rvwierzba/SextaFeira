/** Identidade estável deste dispositivo (salva no navegador) para o núcleo saber quem está falando. */
export interface DeviceIdentity { id: string; name: string; kind: 'phone' | 'tablet' | 'desktop'; }

const KEY = 'sextafeira.device';

function guessKind(): DeviceIdentity['kind'] {
  const ua = navigator.userAgent;
  if (/iPad|Tablet/i.test(ua) || (/Android/i.test(ua) && !/Mobile/i.test(ua))) return 'tablet';
  if (/Mobi|iPhone|Android/i.test(ua)) return 'phone';
  return 'desktop';
}

function guessName(kind: DeviceIdentity['kind']): string {
  const ua = navigator.userAgent;
  const os = /iPhone/.test(ua) ? 'iPhone' : /iPad/.test(ua) ? 'iPad' : /Android/.test(ua) ? 'Android'
    : /Windows/.test(ua) ? 'Windows' : /Mac OS/.test(ua) ? 'Mac' : /Linux/.test(ua) ? 'Linux' : 'Navegador';
  const host = location.hostname === 'localhost' || location.hostname === '127.0.0.1' ? ' (este PC)' : '';
  const label = kind === 'phone' ? 'Celular' : kind === 'tablet' ? 'Tablet' : 'Computador';
  return `${label} ${os}${host}`;
}

export function getDevice(): DeviceIdentity {
  try {
    const saved = localStorage.getItem(KEY);
    if (saved) return JSON.parse(saved);
  } catch { /* armazenamento indisponível */ }
  const kind = guessKind();
  const identity: DeviceIdentity = { id: crypto.randomUUID?.() ?? Math.random().toString(36).slice(2), name: guessName(kind), kind };
  try { localStorage.setItem(KEY, JSON.stringify(identity)); } catch { /* ignore */ }
  return identity;
}

export function renameDevice(name: string): DeviceIdentity {
  const d = { ...getDevice(), name };
  try { localStorage.setItem(KEY, JSON.stringify(d)); } catch { /* ignore */ }
  return d;
}
