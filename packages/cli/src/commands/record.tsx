import React, { useState, useEffect, useRef } from 'react';
import { Box, Text, useApp, useInput } from 'ink';
import { Command } from 'commander';
import { render } from 'ink';
import { join } from 'node:path';
import {
  AudioService,
  WhisperTranscriptionService,
  TomAgent,
  OllamaEmbeddingService,
  NoopEmbeddingService,
  SqliteStorageService,
  checkAudioPrerequisites,
  checkModelExists,
  type IStorageService,
  type IAudioService,
  type ITranscriptionService,
  type IEmbeddingService,
} from '@ten-second-tom/core';
import type { ConfigManager, AppConfig, EntryAnalysis } from '@ten-second-tom/core';
import { RecordingUI } from '../components/RecordingUI.js';
import { SentimentDisplay } from '../components/SentimentDisplay.js';
import { ErrorDisplay } from '../components/ErrorDisplay.js';
import { useAutoExit } from '../hooks/useAutoExit.js';
import { checkSetupComplete } from '../hooks/useSetupGuard.js';

// ---------------------------------------------------------------------------
// Pipeline types & orchestration (extracted for testability)
// ---------------------------------------------------------------------------

export interface RecordingPipelineServices {
  audio: IAudioService;
  transcription: ITranscriptionService;
  agent: TomAgent;
  embedding: IEmbeddingService;
  storage: IStorageService;
}

export interface PipelineResult {
  entryId: string;
  transcript: string;
  audioPath: string | undefined;
  analysis: EntryAnalysis | null;
  embeddingStored: boolean;
  warnings: string[];
}

export interface PipelineOptions {
  entryType?: 'recording' | 'note';
  inputMethod?: 'recorded' | 'typed' | 'dictated';
  audioPath?: string;
}

/**
 * Build services from a loaded AppConfig.
 * Exported for testing.
 */
export function buildServicesFromConfig(
  config: AppConfig,
  configManager: ConfigManager,
): RecordingPipelineServices {
  const audio = new AudioService({ audioDir: configManager.audioPath });

  const transcription = new WhisperTranscriptionService();

  const agent = new TomAgent(config.llm);

  const embedding =
    config.embedding.provider === 'ollama'
      ? new OllamaEmbeddingService({
          model: config.embedding.model,
          endpoint: config.embedding.endpoint,
        })
      : config.embedding.provider === 'cloud'
        ? // Cloud embedding not yet implemented — fall back to noop
          new NoopEmbeddingService()
        : new NoopEmbeddingService();

  const storage = new SqliteStorageService(config.storage.dbPath);

  return { audio, transcription, agent, embedding, storage };
}

/**
 * Run the post-recording/note analysis pipeline.
 * Returns the analysis result (or null) and any warnings.
 * Exported for testing.
 *
 * @param transcript - The text content to analyse.
 * @param audioPathOrOptions - For recordings: the audio file path (string).
 *   For notes: pass undefined or a PipelineOptions object.
 * @param services - The pipeline services.
 * @param options - Optional pipeline options (used when audioPathOrOptions is undefined).
 */
export async function runAnalysisPipeline(
  transcript: string,
  audioPathOrOptions: string | undefined,
  services: RecordingPipelineServices,
  options?: PipelineOptions,
): Promise<PipelineResult> {
  const warnings: string[] = [];

  // Resolve audioPath and entry metadata from overloaded argument.
  const audioPath =
    typeof audioPathOrOptions === 'string' ? audioPathOrOptions : options?.audioPath;
  const entryType = options?.entryType ?? 'recording';
  const inputMethod = options?.inputMethod ?? 'recorded';

  // Save the entry first — capture always succeeds if the mic worked.
  const entry = await services.storage.saveEntry({
    type: entryType,
    content: transcript,
    audioPath,
    inputMethod,
  });

  // Run analysis + embedding in parallel, degrading gracefully on failure.
  const [analysisResult, embeddingResult] = await Promise.allSettled([
    services.agent.analyze(transcript),
    services.embedding.embed(transcript),
  ]);

  let analysis: EntryAnalysis | null = null;
  let embeddingStored = false;

  if (analysisResult.status === 'fulfilled') {
    analysis = analysisResult.value;
    await services.storage.updateEntryAnalysis(entry.id, analysis);
  } else {
    warnings.push(
      'AI analysis unavailable — entry saved without analysis. Check your LLM configuration.',
    );
  }

  if (embeddingResult.status === 'fulfilled') {
    await services.storage.updateEntryEmbedding(entry.id, embeddingResult.value);
    embeddingStored = true;
  } else {
    warnings.push('Embedding unavailable — entry saved without vector index.');
  }

  return {
    entryId: entry.id,
    transcript,
    audioPath,
    analysis,
    embeddingStored,
    warnings,
  };
}

// ---------------------------------------------------------------------------
// React component
// ---------------------------------------------------------------------------

type Phase = 'init' | 'recording' | 'transcribing' | 'analyzing' | 'done' | 'error';

function RecordCommand() {
  const { exit } = useApp();

  const [phase, setPhase] = useState<Phase>('init');
  const [transcript, setTranscript] = useState('');
  const [duration, setDuration] = useState(0);
  const [analysis, setAnalysis] = useState<EntryAnalysis | null>(null);
  const [warnings, setWarnings] = useState<string[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [services, setServices] = useState<RecordingPipelineServices | null>(null);

  // Ref to hold the audioPath base directory for file-based transcription
  const audioBaseDirRef = useRef<string>('');

  // -------------------------------------------------------------------------
  // Initialise: check setup, load model, start recording
  // -------------------------------------------------------------------------
  useEffect(() => {
    async function init() {
      try {
        const guard = checkSetupComplete();
        if (!guard.ok) {
          setError(guard.error);
          setPhase('error');
          return;
        }

        const { config, configManager } = guard;

        // Check Whisper model exists before loading
        const modelCheck = checkModelExists(config.stt.modelPath);
        if (!modelCheck.ok) {
          setError(modelCheck.message);
          setPhase('error');
          return;
        }

        // Check SoX is installed (required by node-record-lpcm16)
        const soxCheck = checkAudioPrerequisites();
        if (!soxCheck.ok) {
          setError(soxCheck.message);
          setPhase('error');
          return;
        }

        const svcs = buildServicesFromConfig(config, configManager);
        audioBaseDirRef.current = configManager.audioPath;

        // Load STT model — stderr suppression is handled inside the
        // transcription service via GGML_LOG_LEVEL env var.
        if (!svcs.transcription.isModelLoaded()) {
          try {
            await svcs.transcription.loadModel(config.stt.modelPath);
          } catch {
            setError('STT model not found. Run `tom setup` to download the model.');
            setPhase('error');
            return;
          }
        }

        if (!svcs.transcription.isModelLoaded()) {
          setError('STT model not found. Run `tom setup` to download the model.');
          setPhase('error');
          return;
        }

        setServices(svcs);

        // Start recording
        try {
          svcs.audio.startRecording();
        } catch (err) {
          const hint =
            process.platform === 'darwin'
              ? 'Grant permission in System Settings > Privacy & Security > Microphone'
              : process.platform === 'win32'
                ? 'Check Settings > Privacy > Microphone'
                : 'Check your audio device settings.';
          const msg = err instanceof Error ? err.message : String(err);
          setError(`Microphone access denied. ${hint}\n(${msg})`);
          setPhase('error');
          return;
        }

        setPhase('recording');
      } catch (err) {
        const msg = err instanceof Error ? err.message : String(err);
        setError(`Initialisation failed: ${msg}`);
        setPhase('error');
      }
    }

    void init();
  }, []);

  // -------------------------------------------------------------------------
  // Duration timer
  // -------------------------------------------------------------------------
  useEffect(() => {
    if (phase !== 'recording') return;

    const interval = setInterval(() => {
      setDuration((d) => d + 1);
    }, 1000);

    return () => clearInterval(interval);
  }, [phase]);

  // -------------------------------------------------------------------------
  // Stop and run analysis pipeline
  // -------------------------------------------------------------------------
  async function stopAndAnalyze() {
    if (!services || phase !== 'recording') return;

    try {
      // Stop recording — this signals end-of-stream to the audio recorder
      // and saves the WAV file. The relative path is returned.
      const audioRelPath = await services.audio.stopRecording();

      // Transition to transcribing phase — whisper processes the saved file
      // and we show segments as they arrive via onNewSegments.
      setPhase('transcribing');

      let finalTranscript = '';
      if (audioRelPath) {
        try {
          const fullAudioPath = join(audioBaseDirRef.current, audioRelPath);

          // Use the audio stream for transcription. The stream has already
          // been fully collected by AudioService, so we use transcribeFile
          // on the saved WAV. The onNewSegments callback in the transcription
          // service fires during processing, but transcribeFile doesn't
          // expose it — so we use transcribeStream with the buffered PCM.
          // Actually, transcribeFile is simpler and works with WAV files.
          finalTranscript = await services.transcription.transcribeFile(fullAudioPath);
          setTranscript(finalTranscript);
        } catch {
          // Transcript stays empty — still save the entry
        }
      }

      // Transition to analyzing phase
      setPhase('analyzing');

      const result = await runAnalysisPipeline(finalTranscript, audioRelPath, services);

      setAnalysis(result.analysis);
      setWarnings(result.warnings);
      setPhase('done');
    } catch (err) {
      const msg = err instanceof Error ? err.message : String(err);
      setError(`Recording failed: ${msg}`);
      setPhase('error');
    } finally {
      services.storage.close();
    }
  }

  async function cancel() {
    if (!services || phase !== 'recording') return;
    try {
      await services.audio.stopRecording();
      services.storage.close();
    } catch {
      // Best effort
    }
    exit();
  }

  // -------------------------------------------------------------------------
  // Keyboard handling
  // -------------------------------------------------------------------------
  useInput((input, key) => {
    if (key.return && phase === 'recording') {
      void stopAndAnalyze();
    }
    if (key.escape && phase === 'recording') {
      void cancel();
    }
    // Allow 'q' to quit after done/error
    if ((input === 'q' || key.return) && (phase === 'done' || phase === 'error')) {
      exit();
    }
  });

  // -------------------------------------------------------------------------
  // Exit after done/error with a short delay
  // -------------------------------------------------------------------------
  useAutoExit(phase === 'done' || phase === 'error');

  // -------------------------------------------------------------------------
  // Render
  // -------------------------------------------------------------------------
  if (phase === 'error') {
    return <ErrorDisplay message={error ?? 'Unknown error'} />;
  }

  if (phase === 'init') {
    return (
      <Box paddingY={1}>
        <Text dimColor>Loading Whisper model...</Text>
      </Box>
    );
  }

  if (phase === 'recording') {
    return <RecordingUI phase="recording" transcript="" duration={duration} />;
  }

  if (phase === 'transcribing') {
    return <RecordingUI phase="transcribing" transcript={transcript} duration={duration} />;
  }

  if (phase === 'analyzing') {
    return (
      <Box paddingY={1}>
        <Text color="cyan">Analysing recording...</Text>
      </Box>
    );
  }

  // phase === 'done'
  return (
    <Box flexDirection="column" paddingY={1}>
      <Text color="green" bold>
        Recording saved
      </Text>

      {transcript.length > 0 && (
        <Box paddingLeft={2} marginTop={1} flexDirection="column">
          <Text dimColor>Transcript:</Text>
          <Text>{transcript}</Text>
        </Box>
      )}

      {analysis !== null && (
        <Box marginTop={1}>
          <SentimentDisplay analysis={analysis} />
        </Box>
      )}

      {warnings.length > 0 && (
        <Box marginTop={1} flexDirection="column">
          {warnings.map((w, i) => (
            <Text key={i} color="yellow">
              Warning: {w}
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

export const recordCommand = new Command('record')
  .description('Record audio with live transcription and AI analysis')
  .action(() => {
    render(<RecordCommand />);
  });
