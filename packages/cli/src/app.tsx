import React, { useState, useEffect, useCallback, useRef } from 'react';
import { Box, Static, Text, useApp, useInput } from 'ink';
import { buildServicesFromConfig } from 'ten-second-tom-core';
import type { AppConfig, ServiceContainer, ConfigManager } from 'ten-second-tom-core';
import { checkSetupComplete } from './hooks/useSetupGuard.js';
import { useAutoExit } from './hooks/useAutoExit.js';
import { HomeScreen } from './screens/HomeScreen.js';
import { RecordingScreen } from './screens/RecordingScreen.js';
import { ProcessingScreen } from './screens/ProcessingScreen.js';
import { SearchScreen } from './screens/SearchScreen.js';
import { ListScreen } from './screens/ListScreen.js';
import { NoteScreen } from './screens/NoteScreen.js';
import { SetupWizard } from './commands/setup.js';
import { ResultsSummary } from './components/ResultsSummary.js';
import type { ResultsSummaryProps } from './components/ResultsSummary.js';
import { findCommand } from './commands/registry.js';
import type { Screen, HistoryEntry, AppContext } from './commands/registry.js';
import { AUTO_EXIT_DELAY_MS } from './constants.js';

// ---------------------------------------------------------------------------
// Props
// ---------------------------------------------------------------------------

interface AppProps {
  mode: 'repl' | 'oneshot';
  initialCommand?: string;
  initialArgs?: string;
}

// ---------------------------------------------------------------------------
// Root App component
// ---------------------------------------------------------------------------

export function App({ mode, initialCommand, initialArgs }: AppProps) {
  const { exit } = useApp();

  // ---- state ----
  const [screen, setScreen] = useState<Screen>('home');
  const [completedOutputs, setCompletedOutputs] = useState<HistoryEntry[]>([]);
  const [screenData, setScreenData] = useState<Record<string, unknown>>({});
  const [services, setServices] = useState<ServiceContainer | null>(null);
  const [config, setConfig] = useState<AppConfig | null>(null);
  const [configManager, setConfigManager] = useState<ConfigManager | null>(null);
  const [entryCount, setEntryCount] = useState(0);

  // One-shot: tracks whether the command has completed (triggers auto-exit)
  const [commandDone, setCommandDone] = useState(false);

  // Data passed from RecordingScreen → ProcessingScreen
  const [recordingData, setRecordingData] = useState<{
    audioRelPath: string;
    liveTranscript: string;
    duration: number;
  } | null>(null);

  // Guard against double-executing the initial command in StrictMode
  const initialCommandExecuted = useRef(false);

  // ---- one-shot: auto-exit after delay when done ----
  useAutoExit(commandDone, AUTO_EXIT_DELAY_MS, mode === 'oneshot');

  // Allow pressing any key to exit immediately in one-shot mode when done
  useInput(
    (_input, _key) => {
      if (commandDone && mode === 'oneshot') {
        exit();
      }
    },
    { isActive: commandDone && mode === 'oneshot' },
  );

  // ---- helpers ----
  const pushHistory = useCallback((entry: HistoryEntry) => {
    setCompletedOutputs((prev) => [...prev, entry]);
  }, []);

  const handleSetScreenData = useCallback((data: Record<string, unknown>) => {
    setScreenData((prev) => ({ ...prev, ...data }));
  }, []);

  // ---- build context ----
  const context: AppContext = {
    services,
    configManager,
    setScreen,
    pushHistory,
    setScreenData: handleSetScreenData,
    exit,
    oneShot: mode === 'oneshot',
  };

  // ---- on mount: check setup, build services, count entries ----
  useEffect(() => {
    const guard = checkSetupComplete();
    if (!guard.ok) {
      // Not configured — stay on home screen, config will be null
      return;
    }

    const { config: loadedConfig, configManager: loadedCM } = guard;
    setConfig(loadedConfig);
    setConfigManager(loadedCM);

    try {
      const svcs = buildServicesFromConfig(loadedConfig, loadedCM);
      setServices(svcs);

      // Count entries
      void svcs.storage.countEntries().then((count) => {
        setEntryCount(count);
      });

      // Release native whisper context on exit to suppress "ggml_metal_free:
      // deallocating" and similar native teardown messages.
      const cleanup = () => {
        void svcs.transcription.release();
      };
      process.on('exit', cleanup);
      return () => {
        process.off('exit', cleanup);
      };
    } catch {
      // Service construction failed — degrade gracefully, home screen still works
    }
  }, []);

  // ---- refresh entry count whenever home screen is shown ----
  useEffect(() => {
    if (screen !== 'home' || !services) return;
    void services.storage.countEntries().then((count) => {
      setEntryCount(count);
    });
  }, [screen, services]);

  // ---- execute initial command in one-shot mode ----
  useEffect(() => {
    if (mode !== 'oneshot' || !initialCommand || initialCommandExecuted.current) return;
    initialCommandExecuted.current = true;

    const cmd = findCommand(initialCommand);
    if (cmd) {
      cmd.execute(initialArgs ?? '', context);
    } else {
      pushHistory({
        id: `unknown-${Date.now()}`,
        content: `Unknown command: ${initialCommand}`,
      });
    }
  }, [mode, initialCommand, initialArgs]);

  // screenData is consumed by child screens via context — suppress the
  // unused-variable lint warning here since it is referenced indirectly.
  void screenData;

  // ---- recording screen callbacks ----
  const handleRecordingComplete = useCallback(
    (audioRelPath: string, liveTranscript: string, recordingDuration: number) => {
      setRecordingData({ audioRelPath, liveTranscript, duration: recordingDuration });
      setScreen('processing');
    },
    [],
  );

  const handleRecordingCancel = useCallback(() => {
    pushHistory({
      id: `recording-cancel-${Date.now()}`,
      content: 'Recording cancelled.',
    });
    setScreen('home');
    if (mode === 'oneshot') {
      setCommandDone(true);
    }
  }, [pushHistory, mode]);

  // ---- note screen callback ----
  const handleNoteComplete = useCallback(
    (result: ResultsSummaryProps) => {
      pushHistory({
        id: `note-result-${Date.now()}`,
        content: (
          <ResultsSummary
            transcript={result.transcript}
            analysis={result.analysis}
            warnings={result.warnings}
            entryType={result.entryType}
          />
        ),
      });
      setScreenData({});
      setScreen('home');
      if (mode === 'oneshot') {
        setCommandDone(true);
      }
    },
    [pushHistory, mode],
  );

  // ---- processing screen callback ----
  const handleProcessingComplete = useCallback(
    (result: ResultsSummaryProps) => {
      pushHistory({
        id: `result-${Date.now()}`,
        content: (
          <ResultsSummary
            duration={result.duration}
            transcript={result.transcript}
            analysis={result.analysis}
            warnings={result.warnings}
            entryType={result.entryType}
          />
        ),
      });
      setRecordingData(null);
      setScreenData({});
      setScreen('home');
      if (mode === 'oneshot') {
        setCommandDone(true);
      }
    },
    [pushHistory, mode],
  );

  // ---- render ----
  return (
    <Box flexDirection="column">
      {/* Scroll history — already-completed outputs */}
      <Static items={completedOutputs}>
        {(entry) => (
          <Box key={entry.id} flexDirection="column">
            {typeof entry.content === 'string' ? <Text>{entry.content}</Text> : entry.content}
          </Box>
        )}
      </Static>

      {/* One-shot done: show exit hint instead of home screen */}
      {commandDone && mode === 'oneshot' && (
        <Box paddingTop={1}>
          <Text dimColor>Press any key to exit (auto-exits in 5s)</Text>
        </Box>
      )}

      {/* Active screen — hidden in one-shot done state */}
      {!commandDone && screen === 'home' && (
        <HomeScreen context={context} config={config} entryCount={entryCount} />
      )}

      {screen === 'recording' && (
        <RecordingScreen
          context={context}
          onComplete={handleRecordingComplete}
          onCancel={handleRecordingCancel}
        />
      )}

      {screen === 'processing' && (
        <ProcessingScreen
          context={context}
          audioRelPath={recordingData?.audioRelPath}
          liveTranscript={recordingData?.liveTranscript}
          duration={recordingData?.duration}
          entryId={screenData['entryId'] as string | undefined}
          onComplete={handleProcessingComplete}
        />
      )}

      {screen === 'search' && (
        <SearchScreen
          context={context}
          initialQuery={screenData['query'] as string | undefined}
          onClose={() => {
            setScreenData({});
            setScreen('home');
            if (mode === 'oneshot') {
              setCommandDone(true);
            }
          }}
        />
      )}

      {screen === 'list' && (
        <ListScreen
          context={context}
          filter={screenData['filter'] as 'notes' | 'recordings' | undefined}
          onClose={() => {
            setScreenData({});
            setScreen('home');
            if (mode === 'oneshot') {
              setCommandDone(true);
            }
          }}
        />
      )}

      {screen === 'note' && (
        <NoteScreen
          context={context}
          onComplete={handleNoteComplete}
          onCancel={() => {
            setScreen('home');
            if (mode === 'oneshot') {
              setCommandDone(true);
            }
          }}
        />
      )}

      {screen === 'setup' && (
        <SetupWizard
          onComplete={() => {
            setScreen('home');
          }}
        />
      )}
    </Box>
  );
}
