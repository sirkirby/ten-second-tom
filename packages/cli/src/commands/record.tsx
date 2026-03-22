import React, { useState, useEffect } from 'react';
import { Box, Text, useApp, useInput } from 'ink';
import { Command } from 'commander';
import { render } from 'ink';
import {
  ConfigManager,
  AudioService,
  WhisperTranscriptionService,
  TomAgent,
  OllamaEmbeddingService,
  NoopEmbeddingService,
  SqliteStorageService,
  type IStorageService,
  type IAudioService,
  type ITranscriptionService,
  type IEmbeddingService,
} from '@ten-second-tom/core';
import type { AppConfig, EntryAnalysis } from '@ten-second-tom/core';
import { RecordingUI } from '../components/RecordingUI.js';
import { SentimentDisplay } from '../components/SentimentDisplay.js';

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
  audioPath: string;
  analysis: EntryAnalysis | null;
  embeddingStored: boolean;
  warnings: string[];
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

  const agentConfig =
    config.llm.provider === 'cloud'
      ? { provider: 'cloud' as const, apiKey: config.llm.apiKey }
      : {
          provider: 'local' as const,
          localEndpoint: config.llm.localEndpoint,
          modelId: config.llm.modelId,
        };
  const agent = new TomAgent(agentConfig);

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
 * Run the post-recording analysis pipeline.
 * Returns the analysis result (or null) and any warnings.
 * Exported for testing.
 */
export async function runAnalysisPipeline(
  transcript: string,
  audioPath: string,
  services: RecordingPipelineServices,
): Promise<PipelineResult> {
  const warnings: string[] = [];

  // Save the entry first — capture always succeeds if the mic worked.
  const entry = await services.storage.saveEntry({
    type: 'recording',
    content: transcript,
    audioPath,
    inputMethod: 'recorded',
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

type Phase = 'init' | 'recording' | 'analyzing' | 'done' | 'error';

function RecordCommand() {
  const { exit } = useApp();

  const [phase, setPhase] = useState<Phase>('init');
  const [transcript, setTranscript] = useState('');
  const [duration, setDuration] = useState(0);
  const [analysis, setAnalysis] = useState<EntryAnalysis | null>(null);
  const [warnings, setWarnings] = useState<string[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [services, setServices] = useState<RecordingPipelineServices | null>(null);

  // -------------------------------------------------------------------------
  // Initialise: check setup, load model, start recording
  // -------------------------------------------------------------------------
  useEffect(() => {
    async function init() {
      try {
        const configManager = new ConfigManager();

        if (!configManager.isSetupComplete()) {
          setError('Setup not complete. Run `tom setup` first.');
          setPhase('error');
          return;
        }

        const config = configManager.load()!;
        const svcs = buildServicesFromConfig(config, configManager);

        // Load STT model
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
            process.platform === 'win32'
              ? 'Ensure a microphone is connected and check Windows sound settings.'
              : process.platform === 'darwin'
                ? 'Ensure microphone access is granted in System Settings > Privacy.'
                : 'Ensure a microphone is connected and accessible.';
          const msg = err instanceof Error ? err.message : String(err);
          setError(`Microphone unavailable: ${msg}\n${hint}`);
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
    setPhase('analyzing');

    try {
      const audioPath = await services.audio.stopRecording();
      const audioStream = services.audio.isRecording() ? services.audio.getAudioStream() : null;

      // Transcribe from the stream we already collected
      // Since stopRecording has finished, we transcribe the saved file
      let finalTranscript = transcript;
      if (finalTranscript.trim().length === 0 && audioStream) {
        try {
          finalTranscript = await services.transcription.transcribeStream(audioStream, (chunk) => {
            setTranscript((t) => t + chunk);
          });
        } catch {
          // Transcript stays empty — still save the entry
        }
      }

      const result = await runAnalysisPipeline(finalTranscript, audioPath, services);

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
  useEffect(() => {
    if (phase === 'done' || phase === 'error') {
      const timer = setTimeout(() => exit(), 5000);
      return () => clearTimeout(timer);
    }
    return undefined;
  }, [phase, exit]);

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

  if (phase === 'init') {
    return (
      <Box paddingY={1}>
        <Text dimColor>Initialising...</Text>
      </Box>
    );
  }

  if (phase === 'recording') {
    return <RecordingUI transcript={transcript} duration={duration} isRecording={true} />;
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
