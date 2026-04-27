import recorder from 'node-record-lpcm16';
const { record } = recorder;
type Recording = ReturnType<typeof record>;
import type { Readable } from 'node:stream';
import { chmodSync, createWriteStream, existsSync, mkdirSync } from 'node:fs';
import { join } from 'node:path';
import { randomUUID } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import {
  AUDIO_SAMPLE_RATE,
  AUDIO_CHANNELS,
  AUDIO_BITS_PER_SAMPLE,
  MAX_AUDIO_BUFFER_BYTES,
  PRIVATE_DIR_MODE,
  PRIVATE_FILE_MODE,
} from '../constants.js';

function chmodBestEffort(path: string, mode: number): void {
  try {
    chmodSync(path, mode);
  } catch {
    // Some platforms/filesystems do not support POSIX modes.
  }
}

const WAV_HEADER_BYTES = 44;
const WAV_RIFF_CHUNK_SIZE_OFFSET = 4;
const WAV_FORMAT_OFFSET = 8;
const WAV_FMT_CHUNK_OFFSET = 12;
const WAV_FMT_CHUNK_SIZE_OFFSET = 16;
const WAV_AUDIO_FORMAT_OFFSET = 20;
const WAV_CHANNELS_OFFSET = 22;
const WAV_SAMPLE_RATE_OFFSET = 24;
const WAV_BYTE_RATE_OFFSET = 28;
const WAV_BLOCK_ALIGN_OFFSET = 32;
const WAV_BITS_PER_SAMPLE_OFFSET = 34;
const WAV_DATA_CHUNK_OFFSET = 36;
const WAV_DATA_LENGTH_OFFSET = 40;
const WAV_PCM_FORMAT = 1;
const WAV_FMT_CHUNK_SIZE_BYTES = 16;
const WAV_RIFF_CHUNK_BASE_BYTES = 36;

/**
 * Creates a standard 44-byte WAV header for 16kHz, 16-bit, mono PCM data.
 * Used to wrap raw PCM buffers so they can be saved as valid .wav files.
 */
export function createWavHeader(dataLength: number): Buffer {
  const header = Buffer.alloc(WAV_HEADER_BYTES);
  const byteRate = AUDIO_SAMPLE_RATE * AUDIO_CHANNELS * (AUDIO_BITS_PER_SAMPLE / 8);
  const blockAlign = AUDIO_CHANNELS * (AUDIO_BITS_PER_SAMPLE / 8);

  header.write('RIFF', 0);
  header.writeUInt32LE(WAV_RIFF_CHUNK_BASE_BYTES + dataLength, WAV_RIFF_CHUNK_SIZE_OFFSET);
  header.write('WAVE', WAV_FORMAT_OFFSET);
  header.write('fmt ', WAV_FMT_CHUNK_OFFSET);
  header.writeUInt32LE(WAV_FMT_CHUNK_SIZE_BYTES, WAV_FMT_CHUNK_SIZE_OFFSET);
  header.writeUInt16LE(WAV_PCM_FORMAT, WAV_AUDIO_FORMAT_OFFSET);
  header.writeUInt16LE(AUDIO_CHANNELS, WAV_CHANNELS_OFFSET);
  header.writeUInt32LE(AUDIO_SAMPLE_RATE, WAV_SAMPLE_RATE_OFFSET);
  header.writeUInt32LE(byteRate, WAV_BYTE_RATE_OFFSET);
  header.writeUInt16LE(blockAlign, WAV_BLOCK_ALIGN_OFFSET);
  header.writeUInt16LE(AUDIO_BITS_PER_SAMPLE, WAV_BITS_PER_SAMPLE_OFFSET);
  header.write('data', WAV_DATA_CHUNK_OFFSET);
  header.writeUInt32LE(dataLength, WAV_DATA_LENGTH_OFFSET);
  return header;
}

/**
 * Returns a platform-specific hint for granting microphone permission.
 */
export function getMicrophonePermissionHint(): string {
  if (process.platform === 'darwin')
    return 'Grant permission in System Settings > Privacy & Security > Microphone';
  if (process.platform === 'win32') return 'Check Settings > Privacy > Microphone';
  return 'Check your audio device settings.';
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
  private recordingError: Error | null = null;
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
    this.recordingError = null;

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

    this.audioStream.on('error', (err: Error) => {
      this.recordingError = err;
    });
  }

  async stopRecording(): Promise<string> {
    if (this.recording === null || this.audioStream === null) {
      throw new Error('Not recording');
    }

    const recording = this.recording;
    const audioStream = this.audioStream;

    try {
      // Stop the recorder — this signals end-of-stream
      recording.stop();

      // Wait for the stream to fully drain before writing
      await new Promise<void>((resolve, reject) => {
        if (audioStream.destroyed) {
          resolve();
          return;
        }
        audioStream.once('end', resolve);
        audioStream.once('close', resolve);
        audioStream.once('error', reject);
      });

      if (this.recordingError !== null) {
        throw this.recordingError;
      }

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

      mkdirSync(dirPath, { recursive: true, mode: PRIVATE_DIR_MODE });
      chmodBestEffort(dirPath, PRIVATE_DIR_MODE);

      // Write buffered raw PCM audio to disk as a valid WAV file.
      // The recorder outputs raw PCM (audioType: 'raw') so that the
      // transcription stream receives clean PCM without WAV headers.
      // We prepend a standard WAV header here for file-based playback
      // and for whisper.node's transcribeFile which handles WAV natively.
      const totalPcmBytes = this.bufferSize;
      const wavHeader = createWavHeader(totalPcmBytes);

      const writeStream = createWriteStream(filePath, { mode: PRIVATE_FILE_MODE });
      await new Promise<void>((resolve, reject) => {
        writeStream.on('finish', resolve);
        writeStream.on('error', reject);
        writeStream.write(wavHeader);
        for (const chunk of this.audioChunks) {
          writeStream.write(chunk);
        }
        writeStream.end();
      });
      chmodBestEffort(filePath, PRIVATE_FILE_MODE);

      // Return relative path from audioDir parent perspective (monthDir/fileName)
      return join(monthDir, fileName);
    } finally {
      this.recording = null;
      this.audioStream = null;
      this.audioChunks = [];
      this.bufferSize = 0;
      this.recordingError = null;
    }
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
