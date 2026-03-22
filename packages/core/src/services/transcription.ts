import { initWhisper } from '@fugood/whisper.node';
import type { WhisperContext, TranscribeOptions } from '@fugood/whisper.node';
import type { Readable } from 'node:stream';

export interface ITranscriptionService {
  transcribeStream(audioStream: Readable, onChunk: (text: string) => void): Promise<string>;
  transcribeFile(audioPath: string): Promise<string>;
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

const DEFAULT_TRANSCRIBE_OPTIONS: TranscribeOptions = {
  language: 'en',
};

/**
 * Whisper-based transcription service wrapping @fugood/whisper.node.
 *
 * Supports:
 * - Batch transcription of a WAV file via transcribeFile()
 * - Streaming (chunked) transcription via transcribeStream()
 *
 * The streaming implementation buffers all PCM audio chunks from the Readable
 * stream, then runs a single transcribeData call with onNewSegments to
 * deliver incremental results via the onChunk callback.
 */
export class WhisperTranscriptionService implements ITranscriptionService {
  private context: WhisperContext | null = null;

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
   * Transcribes audio from a Readable PCM stream.
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
  async transcribeStream(
    audioStream: Readable,
    onChunk: (text: string) => void,
  ): Promise<string> {
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
    const float32Audio = int16BufferToFloat32(combined);

    // transcribeData expects an ArrayBuffer
    const audioBuffer = float32Audio.buffer.slice(
      float32Audio.byteOffset,
      float32Audio.byteOffset + float32Audio.byteLength,
    ) as ArrayBuffer;

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
}
