import { createWriteStream, mkdirSync, renameSync, unlinkSync } from 'node:fs';
import { execFileSync } from 'node:child_process';
import { once } from 'node:events';
import { join } from 'node:path';
import type {
  AppConfig,
  EmbeddingConfig,
  LiveTranscriptionConfig,
  LlmConfig,
} from '../types/config.js';
import { MODEL_DOWNLOAD_TIMEOUT_MS, OLLAMA_FETCH_TIMEOUT_MS } from '../constants.js';

export interface OllamaModel {
  name: string;
  size: number;
}

export interface BuildSetupConfigOptions {
  llm: LlmConfig;
  embedding: EmbeddingConfig;
  homePath: string;
  modelsPath: string;
  whisperModelFilename: string;
  liveTranscription: LiveTranscriptionConfig;
}

export async function fetchOllamaModels(
  endpoint: string,
): Promise<{ ok: true; models: OllamaModel[] } | { ok: false; error: string }> {
  const base = endpoint.replace(/\/+$/, '');
  const url = `${base}/api/tags`;

  try {
    const response = await fetch(url, { signal: AbortSignal.timeout(OLLAMA_FETCH_TIMEOUT_MS) });

    if (!response.ok) {
      return { ok: false, error: `Ollama returned HTTP ${response.status}` };
    }

    const data = (await response.json()) as { models?: Array<{ name: string; size: number }> };
    const models: OllamaModel[] = (data.models ?? []).map((m) => ({
      name: m.name,
      size: m.size,
    }));

    return { ok: true, models };
  } catch (err) {
    if (err instanceof DOMException && err.name === 'AbortError') {
      return {
        ok: false,
        error: `Could not connect to Ollama at ${endpoint}. Connection timed out. Make sure Ollama is running.`,
      };
    }
    const msg = err instanceof Error ? err.message : String(err);
    return {
      ok: false,
      error: `Could not connect to Ollama at ${endpoint}. Make sure Ollama is running. (${msg})`,
    };
  }
}

export async function downloadModel(
  url: string,
  destPath: string,
  onProgress: (bytesDownloaded: number, totalBytes: number) => void,
): Promise<void> {
  const response = await fetch(url, {
    signal: AbortSignal.timeout(MODEL_DOWNLOAD_TIMEOUT_MS),
  });

  if (!response.ok) {
    throw new Error(`Download failed: HTTP ${response.status} ${response.statusText}`);
  }

  if (!response.body) {
    throw new Error('Download failed: no response body');
  }

  const contentLength = Number(response.headers.get('content-length') ?? 0);
  let bytesDownloaded = 0;

  const dir = join(destPath, '..');
  mkdirSync(dir, { recursive: true });

  const tmpPath = destPath + '.downloading';
  const fileStream = createWriteStream(tmpPath);
  let fileStreamClosed = false;
  fileStream.on('error', () => {
    // Errors are surfaced through the write/drain/finish awaits below.
  });
  fileStream.on('close', () => {
    fileStreamClosed = true;
  });

  try {
    const reader = response.body.getReader();

    while (true) {
      const { done, value } = await reader.read();
      if (done) break;

      const chunk = Buffer.from(value);
      if (!fileStream.write(chunk)) {
        await Promise.race([
          once(fileStream, 'drain'),
          once(fileStream, 'error').then(([err]) => {
            throw err;
          }),
        ]);
      }
      bytesDownloaded += value.byteLength;
      onProgress(bytesDownloaded, contentLength);
    }

    await new Promise<void>((resolve, reject) => {
      fileStream.on('finish', resolve);
      fileStream.on('error', reject);
      fileStream.end();
    });

    renameSync(tmpPath, destPath);
  } catch (err) {
    fileStream.destroy();
    if (!fileStreamClosed) {
      await once(fileStream, 'close').catch(() => undefined);
    }
    try {
      unlinkSync(tmpPath);
    } catch {
      // Best effort cleanup.
    }
    throw err;
  }
}

export function extractTarBz2(archivePath: string, targetDir: string): void {
  mkdirSync(targetDir, { recursive: true });
  execFileSync('tar', ['xjf', archivePath, '-C', targetDir]);
}

export function buildSetupConfig(options: BuildSetupConfigOptions): AppConfig {
  return {
    llm: options.llm,
    stt: {
      engine: 'whisper.node',
      modelPath: join(options.modelsPath, options.whisperModelFilename),
    },
    embedding: options.embedding,
    storage: {
      dbPath: join(options.homePath, 'tom.db'),
    },
    liveTranscription: options.liveTranscription,
  };
}
