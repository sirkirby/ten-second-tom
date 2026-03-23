import recorder from 'node-record-lpcm16';
const { record } = recorder;
type Recording = ReturnType<typeof record>;
import type { Readable } from 'node:stream';
import { createWriteStream, mkdirSync, existsSync } from 'node:fs';
import { join } from 'node:path';
import { randomUUID } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import {
  AUDIO_SAMPLE_RATE,
  AUDIO_CHANNELS,
  AUDIO_BITS_PER_SAMPLE,
  MAX_AUDIO_BUFFER_BYTES,
} from '../constants.js';

/**
 * Creates a standard 44-byte WAV header for 16kHz, 16-bit, mono PCM data.
 * Used to wrap raw PCM buffers so they can be saved as valid .wav files.
 */
export function createWavHeader(dataLength: number): Buffer {
  const header = Buffer.alloc(44);
  const byteRate = AUDIO_SAMPLE_RATE * AUDIO_CHANNELS * (AUDIO_BITS_PER_SAMPLE / 8);
  const blockAlign = AUDIO_CHANNELS * (AUDIO_BITS_PER_SAMPLE / 8);

  header.write('RIFF', 0);
  header.writeUInt32LE(36 + dataLength, 4);
  header.write('WAVE', 8);
  header.write('fmt ', 12);
  header.writeUInt32LE(16, 16); // PCM chunk size
  header.writeUInt16LE(1, 20); // PCM format
  header.writeUInt16LE(AUDIO_CHANNELS, 22);
  header.writeUInt32LE(AUDIO_SAMPLE_RATE, 24);
  header.writeUInt32LE(byteRate, 28);
  header.writeUInt16LE(blockAlign, 32);
  header.writeUInt16LE(AUDIO_BITS_PER_SAMPLE, 34);
  header.write('data', 36);
  header.writeUInt32LE(dataLength, 40);
  return header;
}

export type AudioPrerequisiteResult = { ok: true } | { ok: false; message: string };

/**
 * Check whether SoX (required by node-record-lpcm16) is available on the system.
 * Returns `{ ok: true }` if SoX is found, or `{ ok: false, message }` with
 * platform-specific install instructions otherwise.
 */
export function checkAudioPrerequisites(): AudioPrerequisiteResult {
  try {
    execFileSync('sox', ['--version'], { stdio: 'ignore' });
    return { ok: true };
  } catch {
    const platform = process.platform;
    if (platform === 'darwin') {
      return {
        ok: false,
        message: 'SoX is required for audio recording. Install with: brew install sox',
      };
    } else if (platform === 'win32') {
      return {
        ok: false,
        message:
          'SoX is required for audio recording. Install from: https://sourceforge.net/projects/sox/',
      };
    }
    return {
      ok: false,
      message: 'SoX is required for audio recording. Install it for your platform.',
    };
  }
}

/**
 * Check whether the Whisper model file exists at the given path.
 * Returns `{ ok: true }` if found, or `{ ok: false, message }` otherwise.
 */
export function checkModelExists(modelPath: string): AudioPrerequisiteResult {
  if (existsSync(modelPath)) {
    return { ok: true };
  }
  return {
    ok: false,
    message: `Whisper model not found at ${modelPath}. Run \`tom setup\` to download it.`,
  };
}

export interface IAudioService {
  startRecording(): void;
  stopRecording(): Promise<string>;
  getAudioStream(): Readable;
  isRecording(): boolean;
}

export interface AudioServiceConfig {
  audioDir: string;
}

export class AudioService implements IAudioService {
  private recording: Recording | null = null;
  private audioStream: Readable | null = null;
  private audioChunks: Buffer[] = [];
  private bufferSize = 0;
  private readonly audioDir: string;

  constructor(config: AudioServiceConfig) {
    this.audioDir = config.audioDir;
  }

  startRecording(): void {
    if (this.recording !== null) {
      throw new Error('Already recording');
    }

    this.audioChunks = [];
    this.bufferSize = 0;

    this.recording = record({
      sampleRate: AUDIO_SAMPLE_RATE,
      channels: AUDIO_CHANNELS,
      audioType: 'raw',
    });

    this.audioStream = this.recording.stream();

    // Collect chunks in a buffer so we can write to disk on stop
    // without conflicting with the TranscriptionService reading the same stream
    this.audioStream.on('data', (chunk: Buffer) => {
      this.audioChunks.push(chunk);
      this.bufferSize += chunk.length;

      // Auto-stop recording to prevent OOM if buffer exceeds maximum
      if (this.bufferSize >= MAX_AUDIO_BUFFER_BYTES) {
        void this.stopRecording();
      }
    });
  }

  async stopRecording(): Promise<string> {
    if (this.recording === null || this.audioStream === null) {
      throw new Error('Not recording');
    }

    // Stop the recorder — this signals end-of-stream
    this.recording.stop();

    // Wait for the stream to fully drain before writing
    await new Promise<void>((resolve) => {
      if (this.audioStream === null || this.audioStream.destroyed) {
        resolve();
        return;
      }
      this.audioStream.once('end', resolve);
      this.audioStream.once('close', resolve);
    });

    // Build output path: <audioDir>/<YYYY-MM>/<YYYY-MM-DD-abcd1234>.wav
    const now = new Date();
    const year = now.getFullYear();
    const month = String(now.getMonth() + 1).padStart(2, '0');
    const monthDir = `${year}-${month}`;
    const dateStr = now.toISOString().slice(0, 10);
    const id = randomUUID().slice(0, 8);
    const fileName = `${dateStr}-${id}.wav`;
    const dirPath = join(this.audioDir, monthDir);
    const filePath = join(dirPath, fileName);

    mkdirSync(dirPath, { recursive: true });

    // Write buffered raw PCM audio to disk as a valid WAV file.
    // The recorder outputs raw PCM (audioType: 'raw') so that the
    // transcription stream receives clean PCM without WAV headers.
    // We prepend a standard WAV header here for file-based playback
    // and for whisper.node's transcribeFile which handles WAV natively.
    const totalPcmBytes = this.audioChunks.reduce((sum, c) => sum + c.length, 0);
    const wavHeader = createWavHeader(totalPcmBytes);

    const writeStream = createWriteStream(filePath);
    await new Promise<void>((resolve, reject) => {
      writeStream.on('finish', resolve);
      writeStream.on('error', reject);
      writeStream.write(wavHeader);
      for (const chunk of this.audioChunks) {
        writeStream.write(chunk);
      }
      writeStream.end();
    });

    // Reset state
    this.recording = null;
    this.audioStream = null;
    this.audioChunks = [];
    this.bufferSize = 0;

    // Return relative path from audioDir parent perspective (monthDir/fileName)
    return join(monthDir, fileName);
  }

  getAudioStream(): Readable {
    if (this.audioStream === null) {
      throw new Error('Not recording — call startRecording() first');
    }
    return this.audioStream;
  }

  isRecording(): boolean {
    return this.recording !== null;
  }
}
