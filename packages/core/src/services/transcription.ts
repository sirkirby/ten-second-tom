import { initWhisper, toggleNativeLog } from '@fugood/whisper.node';
import type { WhisperContext, TranscribeOptions } from '@fugood/whisper.node';
import type { Readable } from 'node:stream';
import { LIVE_TRANSCRIPTION_CHUNK_BYTES } from '../constants.js';

export interface ITranscriptionService {
  transcribeStream(audioStream: Readable, onChunk: (text: string) => void): Promise<string>;
  transcribeFile(audioPath: string): Promise<string>;
  startLiveTranscription(audioStream: Readable, onChunk: (text: string) => void): void;
  stopLiveTranscription(): Promise<string>;
  isModelLoaded(): boolean;
  loadModel(modelPath: string): Promise<void>;
}

/**
 * Converts Int16 PCM Buffer samples to a Float32Array (range -1.0 to 1.0).
 * The whisper.node transcribeData expects Float32Array audio data.
 */
function int16BufferToFloat32(buffer: Buffer): Float32Array {
  const sampleCount = Math.floor(buffer.byteLength / 2);
  const float32 = new Float32Array(sampleCount);
  for (let i = 0; i < sampleCount; i++) {
    // Read little-endian Int16
    const sample = buffer.readInt16LE(i * 2);
    float32[i] = sample / 32768.0;
  }
  return float32;
}

/**
 * Extracts the transcript string from a result, preferring the top-level
 * `result` string and falling back to concatenating segment texts.
 */
function extractTranscript(result: { result: string; segments: Array<{ text: string }> }): string {
  if (result.result.length > 0) {
    return result.result;
  }
  return result.segments.map((s) => s.text).join('');
}

/**
 * Converts a Buffer of Int16 PCM to an ArrayBuffer suitable for transcribeData.
 */
function pcmBufferToArrayBuffer(buffer: Buffer): ArrayBuffer {
  const float32 = int16BufferToFloat32(buffer);
  return float32.buffer.slice(
    float32.byteOffset,
    float32.byteOffset + float32.byteLength,
  ) as ArrayBuffer;
}

const DEFAULT_TRANSCRIBE_OPTIONS: TranscribeOptions = {
  language: 'en',
};

/**
 * State for the live transcription session managed by startLiveTranscription /
 * stopLiveTranscription.
 */
interface LiveSession {
  /** Accumulated transcript across all completed chunks */
  accumulatedTranscript: string;
  /** PCM bytes buffered since the last chunk was transcribed */
  pendingBuffer: Buffer[];
  /** Total byte count of pendingBuffer */
  pendingBytes: number;
  /** Listener bound to the audio stream's 'data' event */
  onData: (chunk: Buffer) => void;
  /** Callback to deliver each partial transcript segment to the caller */
  onChunk: (text: string) => void;
  /** The audio stream being listened to */
  stream: Readable;
  /** Promise for the currently running transcribeData call (if any) */
  activeTranscription: Promise<void> | null;
}

/**
 * Whisper-based transcription service wrapping @fugood/whisper.node.
 *
 * Supports:
 * - Batch transcription of a WAV file via transcribeFile()
 * - Streaming (chunked) transcription via transcribeStream()
 * - Live chunked transcription during recording via startLiveTranscription() /
 *   stopLiveTranscription()
 *
 * Live transcription:
 *   The streaming implementation slices incoming PCM audio into ~5-second
 *   chunks (LIVE_TRANSCRIPTION_CHUNK_BYTES), transcribes each chunk in sequence
 *   via transcribeData(), and delivers partial segment text to the caller via
 *   the onChunk callback. Because whisper.cpp cannot run two transcriptions
 *   concurrently on the same context, chunks are queued: a new chunk is only
 *   started once the previous one completes.
 */
export class WhisperTranscriptionService implements ITranscriptionService {
  private context: WhisperContext | null = null;
  private liveSession: LiveSession | null = null;

  isModelLoaded(): boolean {
    return this.context !== null;
  }

  /**
   * Loads the GGML Whisper model at the given path.
   * If a model is already loaded, it is released before loading the new one.
   */
  async loadModel(modelPath: string): Promise<void> {
    if (this.context !== null) {
      await this.context.release();
      this.context = null;
    }

    // Suppress whisper.cpp / ggml native logging that the C++ library writes
    // directly to stderr.  The env-var approach (GGML_LOG_LEVEL) does not work
    // reliably at runtime.  The whisper.node package exposes toggleNativeLog()
    // which hooks into the native log callback — calling it with `false`
    // *before* initWhisper prevents all native output from reaching the
    // terminal during model loading and subsequent transcription calls.
    await toggleNativeLog(false);

    this.context = await initWhisper({ filePath: modelPath });
  }

  /**
   * Transcribes a complete WAV audio file (batch mode).
   *
   * @param audioPath - Absolute path to the WAV file (16kHz, mono, 16-bit PCM)
   * @returns The full transcript string
   * @throws If the model has not been loaded via loadModel()
   */
  async transcribeFile(audioPath: string): Promise<string> {
    if (this.context === null) {
      throw new Error('Model not loaded — call loadModel() first');
    }

    const { promise } = this.context.transcribeFile(audioPath, DEFAULT_TRANSCRIBE_OPTIONS);
    const result = await promise;
    return extractTranscript(result);
  }

  /**
   * Transcribes audio from a Readable PCM stream (batch mode — waits for stream end).
   *
   * The stream is expected to emit raw Int16 PCM data at 16kHz, 1 channel
   * (as produced by AudioService.getAudioStream()). All chunks are buffered
   * until the stream ends, then transcribed in a single pass. The onChunk
   * callback is invoked for each new segment as it completes.
   *
   * @param audioStream - Readable stream of raw Int16 PCM audio bytes
   * @param onChunk - Called with incremental transcript text as segments arrive
   * @returns The full accumulated transcript when the stream ends
   * @throws If the model has not been loaded via loadModel()
   */
  async transcribeStream(audioStream: Readable, onChunk: (text: string) => void): Promise<string> {
    if (this.context === null) {
      throw new Error('Model not loaded — call loadModel() first');
    }

    // Collect all PCM chunks from the stream
    const chunks: Buffer[] = [];
    await new Promise<void>((resolve, reject) => {
      audioStream.on('data', (chunk: Buffer) => chunks.push(chunk));
      audioStream.on('end', resolve);
      audioStream.on('error', reject);
    });

    if (chunks.length === 0) {
      return '';
    }

    const combined = Buffer.concat(chunks);
    const audioBuffer = pcmBufferToArrayBuffer(combined);

    const { promise } = this.context.transcribeData(audioBuffer, {
      ...DEFAULT_TRANSCRIBE_OPTIONS,
      onNewSegments: (segmentResult) => {
        if (segmentResult.result.length > 0) {
          onChunk(segmentResult.result);
        }
      },
    });

    const result = await promise;
    return extractTranscript(result);
  }

  /**
   * Starts live chunked transcription from the given audio stream.
   *
   * Audio is sliced into ~5-second chunks (LIVE_TRANSCRIPTION_CHUNK_BYTES).
   * Each full chunk is transcribed via transcribeData() immediately.
   * Partial results are delivered via onChunk() as they arrive.
   *
   * Call stopLiveTranscription() to flush the final partial chunk and stop.
   *
   * @param audioStream - Readable stream of raw Int16 PCM at 16kHz mono 16-bit
   * @param onChunk - Called with each transcript segment text as it arrives
   * @throws If the model has not been loaded via loadModel()
   * @throws If a live transcription session is already active
   */
  startLiveTranscription(audioStream: Readable, onChunk: (text: string) => void): void {
    if (this.context === null) {
      throw new Error('Model not loaded — call loadModel() first');
    }
    if (this.liveSession !== null) {
      throw new Error('Live transcription already active — call stopLiveTranscription() first');
    }

    const session: LiveSession = {
      accumulatedTranscript: '',
      pendingBuffer: [],
      pendingBytes: 0,
      onChunk,
      stream: audioStream,
      activeTranscription: null,
      onData: () => {},
    };

    // Transcribes a single PCM buffer chunk and appends its result to the
    // accumulated transcript. Chunks are serialised so whisper.cpp is never
    // called concurrently.
    const transcribeChunk = async (pcmData: Buffer): Promise<void> => {
      if (this.context === null) return;

      const audioBuffer = pcmBufferToArrayBuffer(pcmData);
      const { promise } = this.context.transcribeData(audioBuffer, {
        ...DEFAULT_TRANSCRIBE_OPTIONS,
        onNewSegments: (segmentResult) => {
          if (segmentResult.result.length > 0) {
            onChunk(segmentResult.result);
          }
        },
      });

      const result = await promise;
      const text = extractTranscript(result);
      if (text.length > 0) {
        session.accumulatedTranscript += text;
      }
    };

    session.onData = (chunk: Buffer) => {
      session.pendingBuffer.push(chunk);
      session.pendingBytes += chunk.length;

      // Once we have a full chunk's worth of audio, start transcribing it.
      if (session.pendingBytes >= LIVE_TRANSCRIPTION_CHUNK_BYTES) {
        const pcmData = Buffer.concat(session.pendingBuffer);
        session.pendingBuffer = [];
        session.pendingBytes = 0;

        // Serialise: wait for any active transcription to finish before
        // starting the next one. This prevents concurrent whisper.cpp calls.
        const prev = session.activeTranscription ?? Promise.resolve();
        session.activeTranscription = prev.then(() => transcribeChunk(pcmData));
      }
    };

    audioStream.on('data', session.onData);
    this.liveSession = session;
  }

  /**
   * Stops live transcription, flushes any remaining buffered audio, and
   * returns the full accumulated transcript.
   *
   * @returns The complete transcript from all transcribed chunks
   * @throws If no live transcription session is active
   */
  async stopLiveTranscription(): Promise<string> {
    if (this.liveSession === null) {
      throw new Error('No active live transcription session');
    }

    const session = this.liveSession;
    this.liveSession = null;

    // Remove the data listener so no more chunks are queued
    session.stream.removeListener('data', session.onData);

    // Wait for any in-progress chunk transcription to finish
    if (session.activeTranscription !== null) {
      await session.activeTranscription;
    }

    // Flush remaining buffered audio (the final partial chunk)
    if (session.pendingBytes > 0) {
      const pcmData = Buffer.concat(session.pendingBuffer);
      await (async () => {
        if (this.context === null) return;

        const audioBuffer = pcmBufferToArrayBuffer(pcmData);
        const { promise } = this.context.transcribeData(audioBuffer, {
          ...DEFAULT_TRANSCRIBE_OPTIONS,
          onNewSegments: (segmentResult) => {
            if (segmentResult.result.length > 0) {
              session.onChunk(segmentResult.result);
            }
          },
        });

        const result = await promise;
        const text = extractTranscript(result);
        if (text.length > 0) {
          session.accumulatedTranscript += text;
        }
      })();
    }

    return session.accumulatedTranscript;
  }
}
