import React, { useState, useEffect, useRef, useCallback } from 'react';
import { Box, Text, useInput } from 'ink';
import Spinner from 'ink-spinner';
import {
  checkAudioPrerequisites,
  checkModelExists,
  buildServicesFromConfig,
  getMicrophonePermissionHint,
} from 'ten-second-tom-core';
import type { ServiceContainer } from 'ten-second-tom-core';
import { checkSetupComplete } from '../hooks/useSetupGuard.js';
import { useAutoExit } from '../hooks/useAutoExit.js';
import { ErrorDisplay } from '../components/ErrorDisplay.js';
import { TranscriptBox } from '../components/TranscriptBox.js';
import { formatDuration, toErrorMessage } from '../utils/format.js';
import type { AppContext } from '../commands/registry.js';
import { AUTO_EXIT_DELAY_MS } from '../constants.js';

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

const TIMER_INTERVAL_MS = 1_000;
const RECORDING_INDICATOR = '\u25CF'; // ● (filled circle)

// ---------------------------------------------------------------------------
// Props
// ---------------------------------------------------------------------------

export interface RecordingScreenProps {
  context: AppContext;
  onComplete: (audioRelPath: string, liveTranscript: string, duration: number) => void;
  onCancel: () => void;
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

type Phase = 'init' | 'recording' | 'error';

// ---------------------------------------------------------------------------
// RecordingScreen
// ---------------------------------------------------------------------------

export function RecordingScreen({ context, onComplete, onCancel }: RecordingScreenProps) {
  const [phase, setPhase] = useState<Phase>('init');
  const [initStatus, setInitStatus] = useState('Checking prerequisites...');
  const [transcript, setTranscript] = useState('');
  const [duration, setDuration] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const [liveTranscriptionWarning, setLiveTranscriptionWarning] = useState<string | null>(null);

  // Refs for mutable state accessed in callbacks
  const servicesRef = useRef<ServiceContainer | null>(null);
  const phaseRef = useRef<Phase>('init');
  const transcriptRef = useRef('');
  const durationRef = useRef(0);

  // Keep phaseRef in sync
  useEffect(() => {
    phaseRef.current = phase;
  }, [phase]);

  // Keep transcriptRef in sync
  useEffect(() => {
    transcriptRef.current = transcript;
  }, [transcript]);

  // Keep durationRef in sync
  useEffect(() => {
    durationRef.current = duration;
  }, [duration]);

  // -------------------------------------------------------------------------
  // Initialise: check setup, load model, start recording
  // -------------------------------------------------------------------------
  useEffect(() => {
    let cancelled = false;

    async function init() {
      try {
        // Use pre-built services from the App context if available
        const guard = checkSetupComplete();
        if (!guard.ok) {
          if (!cancelled) {
            setError(guard.error);
            setPhase('error');
          }
          return;
        }

        const { config, configManager } = guard;

        // Check Whisper model exists before loading
        const modelCheck = checkModelExists(config.stt.modelPath);
        if (!modelCheck.ok) {
          if (!cancelled) {
            setError(modelCheck.message);
            setPhase('error');
          }
          return;
        }

        // Check SoX is installed (required by node-record-lpcm16)
        const soxCheck = checkAudioPrerequisites();
        if (!soxCheck.ok) {
          if (!cancelled) {
            setError(soxCheck.message);
            setPhase('error');
          }
          return;
        }

        // Build services — we need our own ServiceContainer for the recording
        // lifecycle since the App-level services may not have a loaded
        // transcription model.
        const svcs = context.services ?? buildServicesFromConfig(config, configManager);
        servicesRef.current = svcs;

        // Load STT model if not already loaded
        if (!svcs.transcription.isModelLoaded()) {
          if (!cancelled) setInitStatus('Loading Whisper model...');
          // Yield so Ink can render the status update
          await new Promise((resolve) => setTimeout(resolve, 0));
          try {
            await svcs.transcription.loadModel(config.stt.modelPath);
          } catch {
            if (!cancelled) {
              setError('STT model not found. Run `tom setup` to download the model.');
              setPhase('error');
            }
            return;
          }
        }

        if (!svcs.transcription.isModelLoaded()) {
          if (!cancelled) {
            setError('STT model not found. Run `tom setup` to download the model.');
            setPhase('error');
          }
          return;
        }

        if (!cancelled) setInitStatus('Starting microphone...');
        // Yield so the status update renders before starting recording
        await new Promise((resolve) => setTimeout(resolve, 0));

        // Start recording
        try {
          svcs.audio.startRecording();
        } catch (err) {
          const msg = toErrorMessage(err);
          if (!cancelled) {
            setError(`Microphone access denied. ${getMicrophonePermissionHint()}\n(${msg})`);
            setPhase('error');
          }
          return;
        }

        // Start live transcription (sherpa-onnx) if available
        if (svcs.liveTranscription.isAvailable()) {
          try {
            svcs.liveTranscription.start(svcs.audio.getAudioStream(), (text) => {
              if (!cancelled) {
                setTranscript(text);
              }
            });
          } catch (err) {
            const msg = toErrorMessage(err);
            if (!cancelled) {
              setLiveTranscriptionWarning(`Live preview unavailable: ${msg}`);
            }
          }
        }

        if (!cancelled) {
          setPhase('recording');
        }
      } catch (err) {
        const msg = toErrorMessage(err);
        if (!cancelled) {
          setError(`Initialisation failed: ${msg}`);
          setPhase('error');
        }
      }
    }

    void init();

    return () => {
      cancelled = true;
    };
  }, []);

  // -------------------------------------------------------------------------
  // Duration timer
  // -------------------------------------------------------------------------
  useEffect(() => {
    if (phase !== 'recording') return;

    const interval = setInterval(() => {
      setDuration((d) => d + 1);
    }, TIMER_INTERVAL_MS);

    return () => clearInterval(interval);
  }, [phase]);

  // -------------------------------------------------------------------------
  // Stop recording and hand off to ProcessingScreen
  // -------------------------------------------------------------------------
  const handleStop = useCallback(async () => {
    const svcs = servicesRef.current;
    if (!svcs || phaseRef.current !== 'recording') return;

    try {
      // Stop live transcription
      svcs.liveTranscription.stop();

      // Stop the audio recorder — writes the WAV file to disk
      const audioRelPath = await svcs.audio.stopRecording();

      // Hand off the audio path, live transcript, and duration to the parent
      onComplete(audioRelPath, transcriptRef.current, durationRef.current);
    } catch (err) {
      const msg = toErrorMessage(err);
      setError(`Recording failed: ${msg}`);
      setPhase('error');
    }
  }, [onComplete]);

  // -------------------------------------------------------------------------
  // Cancel recording
  // -------------------------------------------------------------------------
  const handleCancel = useCallback(async () => {
    const svcs = servicesRef.current;
    if (!svcs || phaseRef.current !== 'recording') return;

    try {
      svcs.liveTranscription.stop();
      await svcs.audio.stopRecording();
    } catch {
      // Best effort cleanup
    }
    onCancel();
  }, [onCancel]);

  // -------------------------------------------------------------------------
  // Keyboard handling
  // -------------------------------------------------------------------------
  useInput((_input, key) => {
    if (key.return && phaseRef.current === 'recording') {
      void handleStop();
    }
    if (key.escape && phaseRef.current === 'recording') {
      void handleCancel();
    }
  });

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

  if (phase === 'init') {
    return (
      <Box paddingY={1}>
        <Text color="cyan">
          <Spinner type="dots" />
        </Text>
        <Text> {initStatus}</Text>
      </Box>
    );
  }

  // phase === 'recording'
  return (
    <Box flexDirection="column" gap={1}>
      {/* Header: red recording indicator with timer */}
      <Box>
        <Text color="red" bold>
          {RECORDING_INDICATOR} RECORDING
        </Text>
        <Text bold>{` \u2014 ${formatDuration(duration)}`}</Text>
      </Box>

      {/* Live transcript preview */}
      {transcript.length > 0 && (
        <Box flexDirection="column" paddingLeft={2}>
          <Text dimColor italic>
            Live preview
          </Text>
          <TranscriptBox text={transcript} />
        </Box>
      )}

      {/* Live transcription warning */}
      {liveTranscriptionWarning !== null && (
        <Box paddingLeft={2}>
          <Text dimColor color="yellow">
            {liveTranscriptionWarning}
          </Text>
        </Box>
      )}

      {/* Controls hint */}
      <Box paddingLeft={2}>
        <Text dimColor>{'Enter to finish \u00B7 Esc to cancel'}</Text>
      </Box>
    </Box>
  );
}
