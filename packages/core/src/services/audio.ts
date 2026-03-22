import { record } from 'node-record-lpcm16';
import type { Recording } from 'node-record-lpcm16';
import type { Readable } from 'node:stream';
import { createWriteStream, mkdirSync } from 'node:fs';
import { join } from 'node:path';
import { randomUUID } from 'node:crypto';

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
  private readonly audioDir: string;

  constructor(config: AudioServiceConfig) {
    this.audioDir = config.audioDir;
  }

  startRecording(): void {
    if (this.recording !== null) {
      throw new Error('Already recording');
    }

    this.audioChunks = [];

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
