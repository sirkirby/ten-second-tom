import { spawn } from 'node:child_process';
import { existsSync, mkdtempSync, readFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import type { TranscribeOptions } from '@fugood/whisper.node';

export interface ITranscriptionService {
  transcribeFile(audioPath: string): Promise<string>;
  isModelLoaded(): boolean;
  loadModel(modelPath: string): Promise<void>;
  /** Release the whisper context and free native resources. */
  release(): Promise<void>;
}

interface WhisperTranscribeResult {
  result: string;
  segments: Array<{ text: string }>;
}

type WorkerPayload = { ok: true; result: WhisperTranscribeResult } | { ok: false; error: string };

const DEFAULT_TRANSCRIBE_OPTIONS: TranscribeOptions = {
  language: 'en',
};

const CHILD_OUTPUT_LIMIT_BYTES = 8_192;

const WHISPER_WORKER_CODE = `
import { writeFileSync } from 'node:fs';

const [moduleSpecifier, modelPath, audioPath, outputPath, optionsJson] = process.argv.slice(1);

try {
  const { initWhisper, toggleNativeLog } = await import(moduleSpecifier);
  await toggleNativeLog(false);
  const context = await initWhisper({ filePath: modelPath });
  try {
    const { promise } = context.transcribeFile(audioPath, JSON.parse(optionsJson));
    const result = await promise;
    writeFileSync(outputPath, JSON.stringify({ ok: true, result }), 'utf8');
  } finally {
    await context.release();
  }
} catch (error) {
  const message = error instanceof Error ? error.message : String(error);
  writeFileSync(outputPath, JSON.stringify({ ok: false, error: message }), 'utf8');
  process.exitCode = 1;
}
`;

/**
 * Extracts the transcript string from a result, preferring the top-level
 * `result` string and falling back to concatenating segment texts.
 */
function extractTranscript(result: WhisperTranscribeResult): string {
  if (result.result.length > 0) {
    return result.result;
  }
  return result.segments.map((s) => s.text).join('');
}

function truncateOutput(output: string): string {
  if (output.length <= CHILD_OUTPUT_LIMIT_BYTES) return output;
  return output.slice(-CHILD_OUTPUT_LIMIT_BYTES);
}

async function runWhisperWorker(
  modelPath: string,
  audioPath: string,
  options: TranscribeOptions,
): Promise<WhisperTranscribeResult> {
  const tempDir = mkdtempSync(join(tmpdir(), 'tom-whisper-'));
  const outputPath = join(tempDir, 'result.json');
  const moduleSpecifier = import.meta.resolve('@fugood/whisper.node');

  try {
    const child = spawn(
      process.execPath,
      [
        '--input-type=module',
        '--eval',
        WHISPER_WORKER_CODE,
        moduleSpecifier,
        modelPath,
        audioPath,
        outputPath,
        JSON.stringify(options),
      ],
      {
        stdio: ['ignore', 'pipe', 'pipe'],
      },
    );

    let stdout = '';
    let stderr = '';
    child.stdout.on('data', (chunk: Buffer) => {
      stdout = truncateOutput(stdout + chunk.toString('utf8'));
    });
    child.stderr.on('data', (chunk: Buffer) => {
      stderr = truncateOutput(stderr + chunk.toString('utf8'));
    });

    await new Promise<void>((resolve, reject) => {
      child.on('error', reject);
      child.on('exit', () => resolve());
    });

    const payload = readWorkerPayload(outputPath);
    if (payload.ok) {
      return payload.result;
    }

    const suffix =
      stdout.length > 0 || stderr.length > 0 ? ' Native output was captured and suppressed.' : '';
    throw new Error(`Whisper transcription failed: ${payload.error}.${suffix}`);
  } catch (error) {
    if (error instanceof Error) {
      throw error;
    }
    throw new Error(`Whisper transcription failed: ${String(error)}`, { cause: error });
  } finally {
    rmSync(tempDir, { recursive: true, force: true });
  }
}

function readWorkerPayload(outputPath: string): WorkerPayload {
  if (!existsSync(outputPath)) {
    return {
      ok: false,
      error: 'worker exited before returning a transcription result',
    };
  }

  return JSON.parse(readFileSync(outputPath, 'utf8')) as WorkerPayload;
}

/**
 * Whisper-based transcription service wrapping @fugood/whisper.node.
 *
 * Batch transcribes a WAV file after recording stops. Native Whisper writes
 * loader diagnostics directly to stdout/stderr, so transcription runs in a
 * child process with captured stdio to keep the Ink UI clean.
 */
export class WhisperTranscriptionService implements ITranscriptionService {
  private modelPath: string | null = null;

  isModelLoaded(): boolean {
    return this.modelPath !== null;
  }

  /**
   * Records the GGML Whisper model path to use for future transcriptions.
   * The native model is loaded in a quiet child process during transcription.
   */
  async loadModel(modelPath: string): Promise<void> {
    if (!existsSync(modelPath)) {
      throw new Error('STT model not found. Run `tom setup` to download the model.');
    }
    this.modelPath = modelPath;
  }

  /**
   * Releases the loaded model marker.
   * The native context itself lives only inside the transcription worker.
   */
  async release(): Promise<void> {
    this.modelPath = null;
  }

  /**
   * Transcribes a complete WAV audio file (batch mode).
   *
   * @param audioPath - Absolute path to the WAV file (16kHz, mono, 16-bit PCM)
   * @returns The full transcript string
   * @throws If the model has not been loaded via loadModel()
   */
  async transcribeFile(audioPath: string): Promise<string> {
    if (this.modelPath === null) {
      throw new Error('Model not loaded — call loadModel() first');
    }

    const result = await runWhisperWorker(this.modelPath, audioPath, DEFAULT_TRANSCRIBE_OPTIONS);
    return extractTranscript(result);
  }
}
