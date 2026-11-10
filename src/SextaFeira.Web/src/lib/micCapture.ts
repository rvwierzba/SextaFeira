/**
 * Captura do microfone com detecção de voz (VAD) no próprio navegador.
 *
 * - AudioWorklet converte o áudio para PCM mono 16 kHz (o que o Whisper espera).
 * - O VAD usa um piso de ruído adaptativo: funciona em sala silenciosa e em ambiente barulhento.
 * - Guarda ~300 ms antes do início da fala (pré-roll) para não cortar a primeira sílaba.
 * - Considera a frase terminada depois de uma pausa natural (padrão 800 ms).
 */

const TARGET_RATE = 16000;
const FRAME_MS = 20;
const FRAME_SAMPLES = (TARGET_RATE * FRAME_MS) / 1000; // 320

export interface MicCaptureOptions {
  onUtterance: (pcm16: Int16Array, durationMs: number) => void;
  onSpeechStart?: (strength: number) => void;
  onLevel?: (level: number) => void;
  /** Retorna true enquanto a assistente está falando (o VAD fica mais exigente para não ouvir o alto-falante). */
  isAssistantSpeaking?: () => boolean;
  endSilenceMs?: number;
  maxUtteranceMs?: number;
}

const WORKLET_SOURCE = `
class PcmDownsampler extends AudioWorkletProcessor {
  constructor(options) {
    super();
    this.ratio = sampleRate / ${TARGET_RATE};
    this.pos = 0;
    this.buffer = new Float32Array(${FRAME_SAMPLES});
    this.filled = 0;
  }
  process(inputs) {
    const input = inputs[0];
    if (!input || input.length === 0) return true;
    const ch = input[0];
    // Reamostragem por média de janela (passa-baixa simples + decimação).
    while (this.pos < ch.length) {
      const start = Math.floor(this.pos);
      const end = Math.min(ch.length, Math.floor(this.pos + this.ratio));
      let sum = 0, n = 0;
      for (let i = start; i < end; i++) { sum += ch[i]; n++; }
      this.buffer[this.filled++] = n ? sum / n : ch[start] || 0;
      if (this.filled === this.buffer.length) {
        this.port.postMessage(this.buffer.slice(0));
        this.filled = 0;
      }
      this.pos += this.ratio;
    }
    this.pos -= ch.length;
    return true;
  }
}
registerProcessor('pcm-downsampler', PcmDownsampler);
`;

export class MicCapture {
  private ctx: AudioContext | null = null;
  private stream: MediaStream | null = null;
  private node: AudioWorkletNode | null = null;
  private source: MediaStreamAudioSourceNode | null = null;

  private noiseFloor = 0.004;
  private speaking = false;
  private speechFrames = 0;
  private silenceFrames = 0;
  private preRoll: Float32Array[] = [];
  private current: Float32Array[] = [];
  private readonly preRollFrames = Math.round(300 / FRAME_MS);
  private readonly opts: Required<Omit<MicCaptureOptions, 'onSpeechStart' | 'onLevel' | 'isAssistantSpeaking'>> & MicCaptureOptions;

  constructor(options: MicCaptureOptions) {
    this.opts = { endSilenceMs: 800, maxUtteranceMs: 20000, ...options };
  }

  get active() { return this.ctx !== null; }

  async start(): Promise<void> {
    if (this.ctx) return;
    if (!navigator.mediaDevices?.getUserMedia) {
      throw new Error(window.isSecureContext
        ? 'Este navegador não permite acesso ao microfone.'
        : 'O microfone só funciona em HTTPS. Abra pelo endereço https://<ip-do-pc>:5001.');
    }
    this.stream = await navigator.mediaDevices.getUserMedia({
      audio: { echoCancellation: true, noiseSuppression: true, autoGainControl: true, channelCount: 1 },
    });
    const AudioCtx = window.AudioContext || (window as any).webkitAudioContext;
    this.ctx = new AudioCtx();
    if (this.ctx.state === 'suspended') await this.ctx.resume();

    const url = URL.createObjectURL(new Blob([WORKLET_SOURCE], { type: 'application/javascript' }));
    await this.ctx.audioWorklet.addModule(url);
    URL.revokeObjectURL(url);

    this.source = this.ctx.createMediaStreamSource(this.stream);
    this.node = new AudioWorkletNode(this.ctx, 'pcm-downsampler');
    this.node.port.onmessage = (e: MessageEvent<Float32Array>) => this.onFrame(e.data);
    this.source.connect(this.node);
    // O worklet precisa estar ligado ao grafo para processar; volume zero evita retorno no alto-falante.
    const mute = this.ctx.createGain();
    mute.gain.value = 0;
    this.node.connect(mute).connect(this.ctx.destination);
  }

  stop() {
    this.node?.disconnect();
    this.source?.disconnect();
    this.stream?.getTracks().forEach(t => t.stop());
    this.ctx?.close().catch(() => {});
    this.ctx = null; this.stream = null; this.node = null; this.source = null;
    this.reset();
    this.opts.onLevel?.(0);
  }

  private reset() {
    this.speaking = false;
    this.speechFrames = 0;
    this.silenceFrames = 0;
    this.current = [];
  }

  private onFrame(frame: Float32Array) {
    let sum = 0;
    for (let i = 0; i < frame.length; i++) sum += frame[i] * frame[i];
    const rms = Math.sqrt(sum / frame.length);
    this.opts.onLevel?.(Math.min(1, rms * 12));

    const assistantTalking = this.opts.isAssistantSpeaking?.() ?? false;
    // Limiar: bem acima do ruído de fundo; mais alto ainda enquanto a assistente fala (eco do alto-falante).
    const factor = assistantTalking ? 6 : 3;
    const minAbs = assistantTalking ? 0.03 : 0.012;
    const threshold = Math.max(this.noiseFloor * factor, minAbs);
    const isVoice = rms > threshold;

    if (!this.speaking) {
      // Atualiza o piso de ruído só quando não há fala.
      if (!isVoice) this.noiseFloor = this.noiseFloor * 0.95 + rms * 0.05;
      this.preRoll.push(frame);
      if (this.preRoll.length > this.preRollFrames) this.preRoll.shift();

      this.speechFrames = isVoice ? this.speechFrames + 1 : 0;
      if (this.speechFrames >= 3) { // 60 ms de voz contínua
        this.speaking = true;
        this.silenceFrames = 0;
        this.current = [...this.preRoll];
        this.preRoll = [];
        this.opts.onSpeechStart?.(rms / Math.max(threshold, 1e-6));
      }
      return;
    }

    this.current.push(frame);
    this.silenceFrames = isVoice ? 0 : this.silenceFrames + 1;
    const durationMs = this.current.length * FRAME_MS;
    const endFrames = Math.round(this.opts.endSilenceMs / FRAME_MS);

    if (this.silenceFrames >= endFrames || durationMs >= this.opts.maxUtteranceMs) {
      // Remove a maior parte do silêncio final.
      const keep = Math.max(0, this.current.length - Math.max(0, this.silenceFrames - 8));
      const frames = this.current.slice(0, keep);
      this.reset();
      const voicedMs = (frames.length - 8) * FRAME_MS;
      if (voicedMs < 250) return; // estalo / tosse
      this.opts.onUtterance(toPcm16(frames), frames.length * FRAME_MS);
    }
  }
}

function toPcm16(frames: Float32Array[]): Int16Array {
  const total = frames.reduce((n, f) => n + f.length, 0);
  const out = new Int16Array(total);
  let o = 0;
  for (const f of frames) {
    for (let i = 0; i < f.length; i++) {
      const s = Math.max(-1, Math.min(1, f[i]));
      out[o++] = s < 0 ? s * 0x8000 : s * 0x7fff;
    }
  }
  return out;
}

export function pcmToBase64(pcm: Int16Array): string {
  const bytes = new Uint8Array(pcm.buffer, pcm.byteOffset, pcm.byteLength);
  let binary = '';
  const chunk = 0x8000;
  for (let i = 0; i < bytes.length; i += chunk) {
    binary += String.fromCharCode.apply(null, Array.from(bytes.subarray(i, i + chunk)));
  }
  return btoa(binary);
}
