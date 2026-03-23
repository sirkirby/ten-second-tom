import recorder from 'node-record-lpcm16';
const { record } = recorder;
type Recording = ReturnType<typeof record>;
import type { Readable } from 'node:stream';
import { createWriteStream, mkdirSync, existsSync } from 'node:fs';
import { join } from 'node:path';
import { randomUUID } from 'node:crypto';
import { execFileSync } from 'node:child_process';

export type AudioPrerequisiteResult =
  | { ok: true }
  | { ok: false; message: string };

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
export function checkModelExists(
  modelPath: string,
): AudioPrerequisiteResult {
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
  /** Maximum buffer size (~55 minutes at 16kHz mono 16-bit). */
  private static readonly MAX_BUFFER_BYTES = 100 * 1024 * 1024; // 100MB

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
      sampleRate: 16000,
      channels: 1,
      audioType: 'wav',
    });

    this.audioStream = this.recording.stream();

    // Collect chunks in a buffer so we can write to disk on stop
    // without conflicting with the TranscriptionService reading the same stream
    this.audioStream.on('data', (chunk: Buffer) => {
      this.audioChunks.push(chunk);
      this.bufferSize += chunk.length;

      // Auto-stop recording to prevent OOM if buffer exceeds maximum
      if (this.bufferSize >= AudioService.MAX_BUFFER_BYTES) {
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

    // Write buffered audio to disk
    const writeStream = createWriteStream(filePath);
    await new Promise<void>((resolve, reject) => {
      writeStream.on('finish', resolve);
      writeStream.on('error', reject);
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
