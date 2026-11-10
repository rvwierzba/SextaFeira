/**
 * Fala da assistente neste dispositivo.
 * Toca as frases na ordem em que chegam (streaming): usa o áudio do servidor quando houver,
 * senão a voz neural do próprio sistema (Edge: Francisca Online Natural, Chrome: Google português, iOS: Luciana).
 */

interface QueueItem { seq: number; text: string; audioBase64: string | null; }

export interface SpeakerOptions {
  onSpeakingChange: (speaking: boolean) => void;
  onLevel?: (level: number) => void;
  /** Chamado quando toda a resposta do turno terminou de tocar. */
  onTurnPlaybackFinished?: () => void;
}

export class Speaker {
  private queue: QueueItem[] = [];
  private nextSeq = 0;
  private playing = false;
  private turnId: string | null = null;
  private turnDone = false;
  private expected = 0;
  private audio: HTMLAudioElement | null = null;
  private levelTimer: number | null = null;
  private generation = 0;
  preferredVoices: string[] = ['Francisca', 'Thalita', 'Google português do Brasil', 'Luciana', 'Maria'];
  rate = 1.1;
  muted = false;

  constructor(private readonly opts: SpeakerOptions) {
    if ('speechSynthesis' in window) {
      window.speechSynthesis.getVoices();
      window.speechSynthesis.onvoiceschanged = () => window.speechSynthesis.getVoices();
    }
  }

  get isSpeaking() { return this.playing; }

  static availableVoices(): SpeechSynthesisVoice[] {
    if (!('speechSynthesis' in window)) return [];
    return window.speechSynthesis.getVoices().filter(v => v.lang.toLowerCase().replace('_', '-').startsWith('pt'));
  }

  /** Destrava o áudio no celular (precisa ser chamado dentro de um toque/clique). */
  unlock() {
    try {
      const u = new SpeechSynthesisUtterance(' ');
      u.volume = 0;
      window.speechSynthesis?.speak(u);
    } catch { /* ignore */ }
  }

  enqueue(turnId: string, seq: number, text: string, audioBase64: string | null) {
    if (this.muted) return;
    if (this.turnId !== turnId) {
      this.stop();
      this.turnId = turnId;
      this.turnDone = false;
      this.expected = 0;
    }
    this.queue.push({ seq, text, audioBase64 });
    this.queue.sort((a, b) => a.seq - b.seq);
    this.pump();
  }

  /** O servidor avisou que o turno acabou e quantas frases foram enviadas. */
  finishTurn(turnId: string, count: number) {
    if (this.turnId !== turnId) {
      if (count === 0) this.opts.onTurnPlaybackFinished?.();
      return;
    }
    this.turnDone = true;
    this.expected = count;
    this.checkFinished();
  }

  stop() {
    this.generation++;
    this.queue = [];
    this.nextSeq = 0;
    this.turnId = null;
    if (this.audio) { this.audio.pause(); this.audio = null; }
    if ('speechSynthesis' in window) window.speechSynthesis.cancel();
    this.setPlaying(false);
  }

  private pump() {
    if (this.playing) return;
    const item = this.queue.find(q => q.seq === this.nextSeq);
    if (!item) return;
    this.queue = this.queue.filter(q => q !== item);
    this.nextSeq++;
    this.setPlaying(true);
    const gen = this.generation;
    const done = () => {
      if (gen !== this.generation) return;
      this.setPlaying(false);
      if (this.queue.some(q => q.seq === this.nextSeq)) this.pump();
      else this.checkFinished();
    };
    if (item.audioBase64) this.playMp3(item.audioBase64, done, () => this.speakNative(item.text, done));
    else this.speakNative(item.text, done);
  }

  private checkFinished() {
    if (this.turnDone && !this.playing && this.nextSeq >= this.expected) {
      this.turnDone = false;
      this.opts.onTurnPlaybackFinished?.();
    }
  }

  private playMp3(b64: string, onEnd: () => void, onError: () => void) {
    const audio = new Audio(`data:audio/mpeg;base64,${b64}`);
    this.audio = audio;
    audio.onended = onEnd;
    audio.onerror = onError;
    audio.play().catch(onError);
  }

  private speakNative(text: string, onEnd: () => void) {
    if (!('speechSynthesis' in window)) { onEnd(); return; }
    const u = new SpeechSynthesisUtterance(text);
    const voice = this.pickVoice();
    if (voice) u.voice = voice;
    u.lang = 'pt-BR';
    u.rate = this.rate;
    let ended = false;
    const finish = () => { if (!ended) { ended = true; onEnd(); } };
    u.onend = finish;
    u.onerror = finish;
    // Proteção contra o bug do Chrome em que onend nunca dispara.
    const guard = window.setTimeout(finish, 2000 + text.length * 120);
    u.onend = () => { clearTimeout(guard); finish(); };
    window.speechSynthesis.speak(u);
  }

  pickVoice(): SpeechSynthesisVoice | undefined {
    const voices = Speaker.availableVoices();
    const br = voices.filter(v => /pt[-_]br/i.test(v.lang));
    const pool = br.length ? br : voices;
    for (const pref of this.preferredVoices) {
      const v = pool.find(x => x.name.toLowerCase().includes(pref.toLowerCase()));
      if (v) return v;
    }
    return pool.find(v => /natural|online|neural/i.test(v.name)) ?? pool[0];
  }

  private setPlaying(p: boolean) {
    if (this.playing === p) return;
    this.playing = p;
    this.opts.onSpeakingChange(p);
    if (this.levelTimer) { clearInterval(this.levelTimer); this.levelTimer = null; }
    if (p && this.opts.onLevel) {
      // A Web Speech API não expõe o sinal; simula uma envoltória de fala para a esfera reagir.
      let t = 0;
      this.levelTimer = window.setInterval(() => {
        t += 0.25;
        this.opts.onLevel!(0.35 + 0.3 * Math.abs(Math.sin(t * 2.3)) * (0.6 + 0.4 * Math.random()));
      }, 60);
    } else this.opts.onLevel?.(0);
  }
}
