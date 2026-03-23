import React, { useState, useEffect, useCallback, useRef } from 'react';
import { Box, Static, Text, useApp } from 'ink';
import { buildServicesFromConfig } from '@ten-second-tom/core';
import type { AppConfig, ServiceContainer, ConfigManager } from '@ten-second-tom/core';
import { checkSetupComplete } from './hooks/useSetupGuard.js';
import { HomeScreen } from './screens/HomeScreen.js';
import { RecordingScreen } from './screens/RecordingScreen.js';
import { ProcessingScreen } from './screens/ProcessingScreen.js';
import { ResultsSummary } from './components/ResultsSummary.js';
import type { ResultsSummaryProps } from './components/ResultsSummary.js';
import { findCommand } from './commands/registry.js';
import type { Screen, HistoryEntry, AppContext } from './commands/registry.js';

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

  // Data passed from RecordingScreen → ProcessingScreen
  const [recordingData, setRecordingData] = useState<{
    audioRelPath: string;
    liveTranscript: string;
    duration: number;
  } | null>(null);

  // Guard against double-executing the initial command in StrictMode
  const initialCommandExecuted = useRef(false);

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
      void svcs.storage.listEntries({ limit: 100_000 }).then((entries) => {
        setEntryCount(entries.length);
      });
    } catch {
      // Service construction failed — degrade gracefully, home screen still works
    }
  }, []);

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
  }, [pushHistory]);

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

      // In one-shot mode, exit after processing completes
      if (mode === 'oneshot') {
        exit();
      }
    },
    [pushHistory, mode, exit],
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

      {/* Active screen */}
      {screen === 'home' && (
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

      {/* Placeholder screens — will be implemented in subsequent tasks */}
      {screen === 'search' && <Text color="yellow">Search screen (coming in Task 4)</Text>}
      {screen === 'note' && <Text color="yellow">Note screen (coming in Task 5)</Text>}
      {screen === 'setup' && <Text color="yellow">Setup screen (coming later)</Text>}
    </Box>
  );
}
