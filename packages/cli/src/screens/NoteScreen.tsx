import React, { useState, useEffect, useRef, useCallback } from 'react';
import { Box, Text, useInput } from 'ink';
import TextInput from 'ink-text-input';
import Spinner from 'ink-spinner';
import { join } from 'node:path';
import { unlinkSync } from 'node:fs';
import {
  checkAudioPrerequisites,
  buildServicesFromConfig,
  getMicrophonePermissionHint,
  runAnalysisPipeline,
} from 'ten-second-tom-core';
import type { ServiceContainer } from 'ten-second-tom-core';
import { checkSetupComplete } from '../hooks/useSetupGuard.js';
import { useAutoExit } from '../hooks/useAutoExit.js';
import { ErrorDisplay } from '../components/ErrorDisplay.js';
import { toErrorMessage } from '../utils/format.js';
import type { AppContext } from '../commands/registry.js';
import type { ResultsSummaryProps } from '../components/ResultsSummary.js';
import { AUTO_EXIT_DELAY_MS } from '../constants.js';

// ---------------------------------------------------------------------------
// Props
// ---------------------------------------------------------------------------

export interface NoteScreenProps {
  context: AppContext;
  onComplete: (result: ResultsSummaryProps) => void;
  onCancel: () => void;
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

type Phase = 'input' | 'analyzing' | 'done' | 'error';
type InputMode = 'typing' | 'dictation';

// ---------------------------------------------------------------------------
// NoteScreen
// ---------------------------------------------------------------------------

export function NoteScreen({ context, onComplete, onCancel }: NoteScreenProps) {
  const [phase, setPhase] = useState<Phase>('input');
  const [inputMode, setInputMode] = useState<InputMode>('typing');
  const [noteText, setNoteText] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [dictationWarning, setDictationWarning] = useState<string | null>(null);

  // Refs for mutable state accessed in async callbacks
  const servicesRef = useRef<ServiceContainer | null>(null);
  const audioBaseDirRef = useRef<string>('');
  const phaseRef = useRef<Phase>('input');
  const inputModeRef = useRef<InputMode>('typing');
  const stdinSupportsInput = process.stdin.isTTY === true;

  // Keep refs in sync
  useEffect(() => {
    phaseRef.current = phase;
  }, [phase]);

  useEffect(() => {
    inputModeRef.current = inputMode;
  }, [inputMode]);

  // -------------------------------------------------------------------------
  // Discard recording: stop audio and delete the orphaned WAV file
  // -------------------------------------------------------------------------
  const discardRecording = useCallback(async () => {
    const svcs = servicesRef.current;
    if (svcs === null || !svcs.audio.isRecording()) return;

    try {
      const audioRelPath = await svcs.audio.stopRecording();
      if (audioRelPath && audioBaseDirRef.current) {
        try {
          unlinkSync(join(audioBaseDirRef.current, audioRelPath));
        } catch {
          // Best effort — file may not exist
        }
      }
    } catch {
      // Best effort — continue regardless
    }
  }, []);

  // -------------------------------------------------------------------------
  // Start dictation: load services, check STT availability, begin recording
  // -------------------------------------------------------------------------
  const startDictation = useCallback(async () => {
    setDictationWarning(null);

    try {
      // Resolve services — prefer context, fall back to fresh build
      let svcs = servicesRef.current;
      if (svcs === null) {
        if (context.services) {
          svcs = context.services;
        } else {
          const guard = checkSetupComplete();
          if (!guard.ok) {
            setDictationWarning(guard.error);
            return;
          }
          svcs = buildServicesFromConfig(guard.config, guard.configManager);
        }
        servicesRef.current = svcs;
      }

      if (context.configManager) {
        audioBaseDirRef.current = context.configManager.audioPath;
      }

      // Check SoX is installed (required by node-record-lpcm16)
      const soxCheck = checkAudioPrerequisites();
      if (!soxCheck.ok) {
        setDictationWarning(soxCheck.message);
        return;
      }

      // Check if live transcription is available
      if (!svcs.liveTranscription.isAvailable()) {
        setDictationWarning(
          'Live transcription model not found. Run `tom setup` to download the model.',
        );
        return;
      }

      // Start recording
      try {
        svcs.audio.startRecording();
      } catch (err) {
        const msg = toErrorMessage(err);
        setDictationWarning(`Microphone access denied. ${getMicrophonePermissionHint()} (${msg})`);
        return;
      }

      setInputMode('dictation');

      // Begin live transcription — chunks update the note text
      try {
        svcs.liveTranscription.start(
          svcs.audio.getAudioStream(),
          (text) => {
            setNoteText(text);
          },
          (err) => {
            setDictationWarning(`Live transcription stopped: ${toErrorMessage(err)}`);
          },
        );
      } catch (err) {
        const msg = toErrorMessage(err);
        setDictationWarning(`Live transcription failed to start: ${msg}`);
        // Still in dictation mode with recording active but no transcript preview
      }
    } catch (err) {
      const msg = toErrorMessage(err);
      setDictationWarning(`Dictation failed to start: ${msg}`);
    }
  }, [context.services, context.configManager]);

  // -------------------------------------------------------------------------
  // Stop dictation: stop audio + live transcription, discard file, keep text
  // -------------------------------------------------------------------------
  const stopDictation = useCallback(async () => {
    const svcs = servicesRef.current;
    if (svcs !== null) {
      svcs.liveTranscription.stop();
    }
    await discardRecording();
    setInputMode('typing');
  }, [discardRecording]);

  // -------------------------------------------------------------------------
  // Toggle between typing and dictation modes
  // -------------------------------------------------------------------------
  const toggleMode = useCallback(async () => {
    if (inputModeRef.current === 'typing') {
      await startDictation();
    } else {
      await stopDictation();
    }
  }, [startDictation, stopDictation]);

  // -------------------------------------------------------------------------
  // Submit handler
  // -------------------------------------------------------------------------
  const handleSubmit = useCallback(
    async (text: string) => {
      if (!text.trim()) return; // ignore empty

      // If still in dictation mode, stop the audio and delete the orphaned file
      if (inputModeRef.current === 'dictation') {
        const svcs = servicesRef.current;
        if (svcs !== null) {
          svcs.liveTranscription.stop();
        }
        await discardRecording();
      }

      setPhase('analyzing');

      try {
        // Resolve services — prefer existing, then context, then fresh build
        let svcs = servicesRef.current;
        if (svcs === null) {
          if (context.services) {
            svcs = context.services;
          } else {
            const guard = checkSetupComplete();
            if (!guard.ok) {
              throw new Error(guard.error);
            }
            svcs = buildServicesFromConfig(guard.config, guard.configManager);
          }
          servicesRef.current = svcs;
        }

        const inputMethod = inputModeRef.current === 'dictation' ? 'dictated' : 'typed';

        const result = await runAnalysisPipeline(text.trim(), undefined, svcs, {
          entryType: 'note',
          inputMethod,
        });

        setPhase('done');
        onComplete({
          transcript: text.trim(),
          analysis: result.analysis,
          warnings: result.warnings,
          entryType: 'note',
        });
      } catch (err) {
        const msg = toErrorMessage(err);
        setError(`Failed to save note: ${msg}`);
        setPhase('error');
      }
    },
    [context.services, discardRecording, onComplete],
  );

  // -------------------------------------------------------------------------
  // Keyboard handling: Tab toggles mode, Esc cancels
  // -------------------------------------------------------------------------
  useInput(
    (_input, key) => {
      if (phaseRef.current !== 'input') return;

      if (key.tab) {
        void toggleMode();
      }

      if (key.escape) {
        // Clean up any active dictation before cancelling
        if (inputModeRef.current === 'dictation') {
          void stopDictation().then(() => onCancel());
        } else {
          onCancel();
        }
      }
    },
    { isActive: stdinSupportsInput },
  );

  // -------------------------------------------------------------------------
  // Cleanup on unmount — stop any active recording
  // -------------------------------------------------------------------------
  useEffect(() => {
    return () => {
      const svcs = servicesRef.current;
      if (svcs !== null && svcs.audio.isRecording()) {
        svcs.liveTranscription.stop();
        void discardRecording();
      }
    };
  }, [discardRecording]);

  // -------------------------------------------------------------------------
  // One-shot: auto-exit after showing an error
  // -------------------------------------------------------------------------
  useAutoExit(phase === 'error', AUTO_EXIT_DELAY_MS, context.oneShot);

  // -------------------------------------------------------------------------
  // Render
  // -------------------------------------------------------------------------
  if (phase === 'error') {
    return <ErrorDisplay message={error ?? 'Unknown error'} />;
  }

  if (phase === 'analyzing') {
    return (
      <Box paddingY={1}>
        <Text color="cyan">
          <Spinner type="dots" />
        </Text>
        <Text> Analyzing note...</Text>
      </Box>
    );
  }

  if (phase === 'done') {
    // Parent will replace us with ResultsSummary in Static history
    return null;
  }

  // phase === 'input'
  const isDictating = inputMode === 'dictation';

  return (
    <Box flexDirection="column" gap={1}>
      {/* Header */}
      <Box>
        <Text bold>Note </Text>
        <Text color={isDictating ? 'magenta' : 'white'} bold>
          {isDictating ? '[dictation]' : '[typing]'}
        </Text>
        {isDictating && <Text> {'\uD83C\uDF99'}</Text>}
      </Box>

      {/* Prompt text */}
      <Text>
        {isDictating
          ? 'Speak your note... Press Enter when done (Tab to type):'
          : 'Type your note and press Enter (Tab for dictation):'}
      </Text>

      {/* Text input */}
      <Box>
        <Text bold>&gt; </Text>
        <TextInput
          value={noteText}
          onChange={setNoteText}
          onSubmit={(value) => void handleSubmit(value)}
        />
      </Box>

      {/* Dictation warning */}
      {dictationWarning !== null && <Text color="yellow">{dictationWarning}</Text>}

      {/* Controls hint */}
      <Text dimColor>
        {isDictating ? '[Tab] Switch to typing' : '[Tab] Switch to dictation'}
        {' \u00B7 [Esc] Cancel'}
      </Text>
    </Box>
  );
}
