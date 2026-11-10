import React, { useState } from 'react';
import { Settings, MessagesSquare } from 'lucide-react';
import { HologramCoreCanvas } from './components/3d/HologramCoreCanvas';
import { ConversationLog, HudStatusBar } from './components/hud/ConsoleAndStatusWidgets';
import { HolographicSubtitles } from './components/hud/HolographicSubtitles';
import { VoiceCapsulePill } from './components/hud/VoiceCapsulePill';
import { SettingsPanel } from './components/hud/SettingsPanel';
import { useAssistant } from './hooks/useAssistant';

export const App: React.FC = () => {
  const a = useAssistant();
  const [settingsOpen, setSettingsOpen] = useState(false);
  const [logOpen, setLogOpen] = useState(false);

  const isSpeaking = a.agentState === 'SPEAKING';
  const isThinking = a.agentState === 'THINKING' || a.agentState === 'EXECUTING';
  const engineLabel = a.lastLatency ? `${a.lastLatency.engine} · ${a.lastLatency.model}` : 'núcleo local';

  return (
    <div className="hud-viewport">
      <HologramCoreCanvas state={a.agentState} audioLevel={a.level} />

      <VoiceCapsulePill
        isListening={a.micOn}
        audioLevel={a.level}
        agentState={a.agentState}
        onToggleVoice={a.toggleMic}
        interimTranscript={a.micError ?? undefined}
      />

      <div className="hud-top-actions">
        <button className="hud-dock-toggle-btn" onClick={() => setLogOpen(o => !o)} style={{ borderRadius: 20, padding: '7px 14px' }}>
          <MessagesSquare size={13} /> <span className="hud-hide-mobile">CONVERSA</span>
        </button>
        <button className="hud-dock-toggle-btn" onClick={() => setSettingsOpen(true)} style={{ borderRadius: 20, padding: '7px 14px' }}>
          <Settings size={13} /> <span className="hud-hide-mobile">CONFIGURAÇÕES</span>
        </button>
      </div>

      <div className={`hud-dock-wing hud-dock-right ${logOpen ? '' : 'collapsed'}`}>
        <ConversationLog entries={a.log} onClear={a.clearConversation} />
      </div>

      <div className="hud-subtitles-anchor">
        <HolographicSubtitles
          lastUserSpeech={a.lastUser}
          lastAiResponse={a.assistantText}
          isListening={a.micOn}
          isSpeaking={isSpeaking}
          isThinking={isThinking}
          activeProvider={engineLabel}
        />
      </div>

      <HudStatusBar
        statusText={a.statusText}
        agentState={a.agentState}
        micOn={a.micOn}
        connection={a.connection}
        latency={a.lastLatency}
        onToggleVoice={a.toggleMic}
        onSendTextMessage={a.sendText}
        onInterrupt={a.interrupt}
      />

      <SettingsPanel open={settingsOpen} onClose={() => setSettingsOpen(false)} speaker={a.speaker} devices={a.devices} deviceName={a.device.name} />
    </div>
  );
};
