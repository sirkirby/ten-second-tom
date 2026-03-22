import React, { useState, useEffect, useRef } from 'react';
import { Box, Text, useApp, useInput } from 'ink';
import TextInput from 'ink-text-input';
import { Command } from 'commander';
import { render } from 'ink';
import { ConfigManager } from '@ten-second-tom/core';
import type { EntryAnalysis } from '@ten-second-tom/core';
import { SentimentDisplay } from '../components/SentimentDisplay.js';
import { buildServicesFromConfig, runAnalysisPipeline } from './record.js';
import type { RecordingPipelineServices } from './record.js';

// ---------------------------------------------------------------------------
// Pipeline orchestration (extracted for testability)
// ---------------------------------------------------------------------------

export interface NotePipelineResult {
  entryId: string | null;
  analysis: EntryAnalysis | null;
  warnings: string[];
  error: string | null;
}

/**
 * Orchestrate the note pipeline: validate input, check setup, run analysis.
 * Exported for testing.
 *
 * @param text - The note text content to save.
 * @param inputMethod - How the text was created: 'typed' (default) or 'dictated'.
 */
export async function runNotePipeline(
  text: string,
  inputMethod: 'typed' | 'dictated' = 'typed',
): Promise<NotePipelineResult> {
  const empty: NotePipelineResult = {
    entryId: null,
    analysis: null,
    warnings: [],
    error: null,
  };

  if (!text.trim()) {
    return { ...empty, error: 'Note text is empty — nothing to save.' };
  }

  const configManager = new ConfigManager();

  if (!configManager.isSetupComplete()) {
    return {
      ...empty,
      error: 'Tom is not configured. Run `tom setup` first.',
    };
  }

  const config = configManager.load()!;
  const services = buildServicesFromConfig(config, configManager);

  try {
    const result = await runAnalysisPipeline(text.trim(), undefined, services, {
      entryType: 'note',
      inputMethod,
    });

    return {
      entryId: result.entryId,
      analysis: result.analysis,
      warnings: result.warnings,
      error: null,
    };
  } finally {
    services.storage.close();
  }
}

// ---------------------------------------------------------------------------
// React component
// ---------------------------------------------------------------------------

type Phase = 'input' | 'analyzing' | 'done' | 'error';
type InputMode = 'typed' | 'dictated';

function NoteCommand() {
  const { exit } = useApp();

  const [phase, setPhase] = useState<Phase>('input');
  const [inputMode, setInputMode] = useState<InputMode>('typed');
  const [noteText, setNoteText] = useState('');
  const [analysis, setAnalysis] = useState<EntryAnalysis | null>(null);
  const [warnings, setWarnings] = useState<string[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [dictationWarning, setDictationWarning] = useState<string | null>(null);
  const servicesRef = useRef<RecordingPipelineServices | null>(null);

  // -------------------------------------------------------------------------
  // On mount: check setup guard
  // -------------------------------------------------------------------------
  useEffect(() => {
    const configManager = new ConfigManager();
    if (!configManager.isSetupComplete()) {
      setError('Tom is not configured. Run `tom setup` first.');
      setPhase('error');
    }
  }, []);

  // -------------------------------------------------------------------------
  // Auto-exit after done / error
  // -------------------------------------------------------------------------
  useEffect(() => {
    if (phase === 'done' || phase === 'error') {
      const timer = setTimeout(() => exit(), 5000);
      return () => clearTimeout(timer);
    }
    return undefined;
  }, [phase, exit]);

  // -------------------------------------------------------------------------
  // Start dictation: load services, check STT model, begin recording
  // -------------------------------------------------------------------------
  async function startDictation() {
    setDictationWarning(null);

    try {
      const configManager = new ConfigManager();

      if (!configManager.isSetupComplete()) {
        setDictationWarning('Tom is not configured. Run `tom setup` first.');
        return;
      }

      const config = configManager.load()!;
      let svcs = servicesRef.current;
      if (svcs === null) {
        svcs = buildServicesFromConfig(config, configManager);
        servicesRef.current = svcs;
      }

      if (!svcs.transcription.isModelLoaded()) {
        try {
          await svcs.transcription.loadModel(config.stt.modelPath);
        } catch {
          // Model failed to load — stay in typed mode
        }
      }

      if (!svcs.transcription.isModelLoaded()) {
        setDictationWarning('STT model not loaded — run `tom setup` to download the model.');
        return;
      }

      // Start recording
      try {
        svcs.audio.startRecording();
      } catch (err) {
        const msg = err instanceof Error ? err.message : String(err);
        setDictationWarning(`Microphone unavailable: ${msg}`);
        return;
      }

      setInputMode('dictated');

      // Begin streaming transcription — chunks update the note text
      const stream = svcs.audio.getAudioStream();
      svcs.transcription
        .transcribeStream(stream, (chunk) => {
          setNoteText((prev) => prev + chunk);
        })
        .catch(() => {
          // Transcription errors are non-fatal — the user can still submit
        });
    } catch (err) {
      const msg = err instanceof Error ? err.message : String(err);
      setDictationWarning(`Dictation failed to start: ${msg}`);
    }
  }

  // -------------------------------------------------------------------------
  // Stop dictation: stop audio, discard the file, keep transcribed text
  // -------------------------------------------------------------------------
  async function stopDictation() {
    const svcs = servicesRef.current;
    if (svcs === null) return;

    try {
      if (svcs.audio.isRecording()) {
        // Discard the audio file path — we only want the transcribed text
        await svcs.audio.stopRecording();
      }
    } catch {
      // Best effort — continue regardless
    }

    setInputMode('typed');
  }

  // -------------------------------------------------------------------------
  // Toggle between typed and dictated input modes
  // -------------------------------------------------------------------------
  async function toggleMode() {
    if (inputMode === 'typed') {
      await startDictation();
    } else {
      await stopDictation();
    }
  }

  // -------------------------------------------------------------------------
  // Submit handler
  // -------------------------------------------------------------------------
  async function handleSubmit(text: string) {
    if (!text.trim()) return; // ignore empty

    // If still in dictation mode, stop the audio first (discard the file)
    if (inputMode === 'dictated') {
      const svcs = servicesRef.current;
      if (svcs !== null && svcs.audio.isRecording()) {
        try {
          await svcs.audio.stopRecording();
        } catch {
          // Best effort
        }
      }
    }

    setPhase('analyzing');

    try {
      const configManager = new ConfigManager();
      const config = configManager.load()!;
      const services = buildServicesFromConfig(config, configManager);

      const result = await runAnalysisPipeline(text.trim(), undefined, services, {
        entryType: 'note',
        inputMethod: inputMode,
      });

      services.storage.close();

      setAnalysis(result.analysis);
      setWarnings(result.warnings);
      setPhase('done');
    } catch (err) {
      const msg = err instanceof Error ? err.message : String(err);
      setError(`Failed to save note: ${msg}`);
      setPhase('error');
    }
  }

  // -------------------------------------------------------------------------
  // Keyboard handling: Tab toggles between typed and dictated modes
  // -------------------------------------------------------------------------
  useInput((input, key) => {
    if (phase !== 'input') return;

    if (key.tab) {
      void toggleMode();
    }

    // Allow 'q' or Enter to exit after done/error
    if ((input === 'q' || key.return) && (phase === 'done' || phase === 'error')) {
      exit();
    }
  });

  // -------------------------------------------------------------------------
  // Render
  // -------------------------------------------------------------------------
  if (phase === 'error') {
    return (
      <Box flexDirection="column" paddingY={1}>
        <Text color="red" bold>
          Error
        </Text>
        <Text color="red">{error}</Text>
      </Box>
    );
  }

  if (phase === 'input') {
    const isDictating = inputMode === 'dictated';

    return (
      <Box flexDirection="column" paddingY={1}>
        <Text bold>
          {'📝 New Note'}{' '}
          <Text color={isDictating ? 'magenta' : 'white'}>
            {isDictating ? '[dictation mode] 🎙️' : '[typing mode]'}
          </Text>
        </Text>

        <Text>
          {isDictating
            ? 'Speak your note... Press Enter when done'
            : 'Type your note and press Enter:'}
        </Text>

        <TextInput
          value={noteText}
          onChange={setNoteText}
          onSubmit={(value) => void handleSubmit(value)}
        />

        {dictationWarning !== null && (
          <Text color="yellow">{'⚠ '}{dictationWarning}</Text>
        )}

        <Text dimColor>
          {isDictating ? '[Tab] Switch to typing' : '[Tab] Switch to dictation'}
        </Text>
      </Box>
    );
  }

  if (phase === 'analyzing') {
    return (
      <Box paddingY={1}>
        <Text color="cyan">Analysing note...</Text>
      </Box>
    );
  }

  // phase === 'done'
  return (
    <Box flexDirection="column" paddingY={1}>
      <Text color="green" bold>
        {'✓ Note saved'}
      </Text>

      {analysis !== null && (
        <Box marginTop={1}>
          <SentimentDisplay analysis={analysis} />
        </Box>
      )}

      {warnings.length > 0 && (
        <Box marginTop={1} flexDirection="column">
          {warnings.map((w, i) => (
            <Text key={i} color="yellow">
              {'⚠ '}{w}
            </Text>
          ))}
        </Box>
      )}

      <Box marginTop={1}>
        <Text dimColor>Press Enter or q to exit.</Text>
      </Box>
    </Box>
  );
}

// ---------------------------------------------------------------------------
// Commander command registration
// ---------------------------------------------------------------------------

export const noteCommand = new Command('note')
  .description('Create a text note (type or dictate)')
  .action(() => {
    render(<NoteCommand />);
  });
