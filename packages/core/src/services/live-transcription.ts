import type { Readable } from 'node:stream';
import { existsSync } from 'node:fs';
import { join } from 'node:path';
import {
  AUDIO_SAMPLE_RATE,
  SHERPA_ONNX_MODEL_DIR,
  SHERPA_ONNX_ENCODER_FILENAME,
  SHERPA_ONNX_DECODER_FILENAME,
  SHERPA_ONNX_JOINER_FILENAME,
  SHERPA_ONNX_TOKENS_FILENAME,
  SHERPA_ONNX_POLL_INTERVAL_MS,
} from '../constants.js';

// ---------------------------------------------------------------------------
// sherpa-onnx type declarations (CJS module, no shipped types)
// ---------------------------------------------------------------------------

export interface SherpaOnnxOnlineStream {
  acceptWaveform(sampleRate: number, samples: Float32Array): void;
  inputFinished(): void;
  free(): void;
}

export interface SherpaOnnxOnlineRecognizer {
  createStream(): SherpaOnnxOnlineStream;
  isReady(stream: SherpaOnnxOnlineStream): boolean;
  decode(stream: SherpaOnnxOnlineStream): void;
  isEndpoint(stream: SherpaOnnxOnlineStream): boolean;
  reset(stream: SherpaOnnxOnlineStream): void;
  getResult(stream: SherpaOnnxOnlineStream): { text: string };
  free(): void;
}

export interface SherpaOnnxRecognizerConfig {
  featConfig: { sampleRate: number; featureDim: number };
  modelConfig: {
    transducer: { encoder: string; decoder: string; joiner: string };
    tokens: string;
    numThreads: number;
    provider: string;
    debug: number;
    modelType: string;
  };
  decodingMethod: string;
  maxActivePaths: number;
  enableEndpoint: number;
  rule1MinTrailingSilence: number;
  rule2MinTrailingSilence: number;
  rule3MinUtteranceLength: number;
}

/** Factory function that creates a sherpa-onnx OnlineRecognizer from config. */
export type CreateRecognizerFn = (config: SherpaOnnxRecognizerConfig) => SherpaOnnxOnlineRecognizer;

// ---------------------------------------------------------------------------
// Interface
// ---------------------------------------------------------------------------

export interface ILiveTranscriptionService {
  /** Begin processing audio from the stream, calling onText with accumulated text. */
  start(audioStream: Readable, onText: (text: string) => void): void;
  /** Stop processing and clean up. */
  stop(): void;
  /** Whether the sherpa-onnx model files exist and the service can operate. */
  isAvailable(): boolean;
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/**
 * Converts a Buffer of Int16 PCM samples to a Float32Array (range -1.0 to 1.0).
 * sherpa-onnx's acceptWaveform expects Float32Array audio data.
 */
function int16BufferToFloat32(buffer: Buffer): Float32Array {
  const sampleCount = Math.floor(buffer.byteLength / 2);
  const float32 = new Float32Array(sampleCount);
  for (let i = 0; i < sampleCount; i++) {
    const sample = buffer.readInt16LE(i * 2);
    float32[i] = sample / 32768.0;
  }
  return float32;
}

/**
 * Default factory: loads the real sherpa-onnx CJS module and calls createOnlineRecognizer.
 */
function defaultCreateRecognizer(config: SherpaOnnxRecognizerConfig): SherpaOnnxOnlineRecognizer {
  // eslint-disable-next-line @typescript-eslint/no-require-imports
  const sherpaOnnx = require('sherpa-onnx') as {
    createOnlineRecognizer: CreateRecognizerFn;
  };
  return sherpaOnnx.createOnlineRecognizer(config);
}

// ---------------------------------------------------------------------------
// SherpaOnnxLiveTranscriptionService
// ---------------------------------------------------------------------------

export interface SherpaOnnxLiveTranscriptionConfig {
  /** Absolute path to the directory containing the sherpa-onnx model files. */
  modelsPath: string;
  /**
   * Optional factory to create the sherpa-onnx recognizer.
   * Defaults to loading the real sherpa-onnx module. Override in tests.
   */
  createRecognizer?: CreateRecognizerFn;
}

/**
 * Live streaming transcription using sherpa-onnx's online (streaming) recognizer.
 *
 * Produces low-latency draft transcripts during recording by feeding PCM audio
 * frame-by-frame to a Zipformer transducer model. The output is suitable for
 * live display but is NOT archival quality — Whisper batch transcription
 * produces the final stored transcript after recording stops.
 *
 * Data flow:
 *   mic PCM stream → acceptWaveform() → decode() → getResult() → onText callback
 *
 * The recognizer is polled at SHERPA_ONNX_POLL_INTERVAL_MS intervals for new
 * decoded text. Endpoint detection is enabled so the recognizer resets its
 * internal state when a natural pause is detected, allowing clean sentence
 * boundaries in the accumulated text.
 */
export class SherpaOnnxLiveTranscriptionService implements ILiveTranscriptionService {
  private readonly modelsPath: string;
  private readonly createRecognizerFn: CreateRecognizerFn;
  private recognizer: SherpaOnnxOnlineRecognizer | null = null;
  private stream: SherpaOnnxOnlineStream | null = null;
  private dataListener: ((chunk: Buffer) => void) | null = null;
  private audioStream: Readable | null = null;
  private pollTimer: ReturnType<typeof setInterval> | null = null;
  private accumulatedText = '';

  constructor(config: SherpaOnnxLiveTranscriptionConfig) {
    this.modelsPath = config.modelsPath;
    this.createRecognizerFn = config.createRecognizer ?? defaultCreateRecognizer;
  }

  isAvailable(): boolean {
    const modelDir = join(this.modelsPath, SHERPA_ONNX_MODEL_DIR);
    return (
      existsSync(join(modelDir, SHERPA_ONNX_ENCODER_FILENAME)) &&
      existsSync(join(modelDir, SHERPA_ONNX_DECODER_FILENAME)) &&
      existsSync(join(modelDir, SHERPA_ONNX_JOINER_FILENAME)) &&
      existsSync(join(modelDir, SHERPA_ONNX_TOKENS_FILENAME))
    );
  }

  start(audioStream: Readable, onText: (text: string) => void): void {
    if (this.recognizer !== null) {
      throw new Error('Live transcription already active — call stop() first');
    }

    if (!this.isAvailable()) {
      throw new Error(
        'sherpa-onnx model not found. Download the streaming model to ' +
          join(this.modelsPath, SHERPA_ONNX_MODEL_DIR),
      );
    }

    const modelDir = join(this.modelsPath, SHERPA_ONNX_MODEL_DIR);

    this.recognizer = this.createRecognizerFn({
      featConfig: { sampleRate: AUDIO_SAMPLE_RATE, featureDim: 80 },
      modelConfig: {
        transducer: {
          encoder: join(modelDir, SHERPA_ONNX_ENCODER_FILENAME),
          decoder: join(modelDir, SHERPA_ONNX_DECODER_FILENAME),
          joiner: join(modelDir, SHERPA_ONNX_JOINER_FILENAME),
        },
        tokens: join(modelDir, SHERPA_ONNX_TOKENS_FILENAME),
        numThreads: 2,
        provider: 'cpu',
        debug: 0,
        modelType: '',
      },
      decodingMethod: 'greedy_search',
      maxActivePaths: 4,
      enableEndpoint: 1,
      rule1MinTrailingSilence: 2.4,
      rule2MinTrailingSilence: 1.2,
      rule3MinUtteranceLength: 20,
    });

    this.stream = this.recognizer.createStream();
    this.audioStream = audioStream;
    this.accumulatedText = '';

    // Listen for PCM data and feed it to the recognizer
    this.dataListener = (chunk: Buffer) => {
      if (this.stream === null) return;
      const float32 = int16BufferToFloat32(chunk);
      this.stream.acceptWaveform(AUDIO_SAMPLE_RATE, float32);
    };

    audioStream.on('data', this.dataListener);

    // Poll the recognizer for decoded results
    this.pollTimer = setInterval(() => {
      if (this.recognizer === null || this.stream === null) return;

      while (this.recognizer.isReady(this.stream)) {
        this.recognizer.decode(this.stream);
      }

      const result = this.recognizer.getResult(this.stream);
      const currentText = result.text.trim();

      // Check for endpoint (natural pause / sentence boundary)
      if (this.recognizer.isEndpoint(this.stream)) {
        if (currentText.length > 0) {
          this.accumulatedText += (this.accumulatedText.length > 0 ? ' ' : '') + currentText;
          onText(this.accumulatedText);
        }
        this.recognizer.reset(this.stream);
      } else if (currentText.length > 0) {
        // Show in-progress text combined with accumulated finalized text
        const displayText =
          this.accumulatedText + (this.accumulatedText.length > 0 ? ' ' : '') + currentText;
        onText(displayText);
      }
    }, SHERPA_ONNX_POLL_INTERVAL_MS);
  }

  stop(): void {
    // Stop polling
    if (this.pollTimer !== null) {
      clearInterval(this.pollTimer);
      this.pollTimer = null;
    }

    // Remove audio stream listener
    if (this.audioStream !== null && this.dataListener !== null) {
      this.audioStream.removeListener('data', this.dataListener);
      this.dataListener = null;
      this.audioStream = null;
    }

    // Clean up sherpa-onnx resources
    if (this.stream !== null) {
      this.stream.free();
      this.stream = null;
    }

    if (this.recognizer !== null) {
      this.recognizer.free();
      this.recognizer = null;
    }

    this.accumulatedText = '';
  }
}

// ---------------------------------------------------------------------------
// NoopLiveTranscriptionService
// ---------------------------------------------------------------------------

/**
 * No-op implementation for when the sherpa-onnx model is not available.
 * Recording still works — the user just won't see live transcript preview.
 */
export class NoopLiveTranscriptionService implements ILiveTranscriptionService {
  start(_audioStream: Readable, _onText: (text: string) => void): void {
    // No-op: live transcription not available
  }

  stop(): void {
    // No-op
  }

  isAvailable(): boolean {
    return false;
  }
}
