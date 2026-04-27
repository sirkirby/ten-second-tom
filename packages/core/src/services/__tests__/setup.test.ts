import { existsSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { buildSetupConfig, downloadModel, extractTarBz2, fetchOllamaModels } from '../setup.js';

const childProcessMocks = vi.hoisted(() => ({
  execFileSync: vi.fn(),
}));

vi.mock('node:child_process', () => ({
  execFileSync: childProcessMocks.execFileSync,
}));

describe('fetchOllamaModels', () => {
  const originalFetch = globalThis.fetch;

  beforeEach(() => {
    vi.restoreAllMocks();
  });

  afterEach(() => {
    globalThis.fetch = originalFetch;
  });

  it('returns models from a successful Ollama response', async () => {
    globalThis.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: () =>
        Promise.resolve({
          models: [
            { name: 'qwen2.5:7b', size: 4_700_000_000 },
            { name: 'mistral:7b', size: 4_100_000_000 },
          ],
        }),
    });

    const result = await fetchOllamaModels('http://localhost:11434');

    expect(result.ok).toBe(true);
    if (result.ok) {
      expect(result.models).toHaveLength(2);
      expect(result.models[0]?.name).toBe('qwen2.5:7b');
      expect(result.models[0]?.size).toBe(4_700_000_000);
      expect(result.models[1]?.name).toBe('mistral:7b');
    }
    expect(globalThis.fetch).toHaveBeenCalledWith(
      'http://localhost:11434/api/tags',
      expect.objectContaining({ signal: expect.any(AbortSignal) }),
    );
  });

  it('returns ok with empty models array when Ollama has no models', async () => {
    globalThis.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: () => Promise.resolve({ models: [] }),
    });

    const result = await fetchOllamaModels('http://localhost:11434');

    expect(result.ok).toBe(true);
    if (result.ok) {
      expect(result.models).toHaveLength(0);
    }
  });

  it('strips trailing slash from endpoint URL', async () => {
    globalThis.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: () => Promise.resolve({ models: [] }),
    });

    await fetchOllamaModels('http://localhost:11434/');

    expect(globalThis.fetch).toHaveBeenCalledWith(
      'http://localhost:11434/api/tags',
      expect.any(Object),
    );
  });

  it('returns error when Ollama returns a non-OK HTTP status', async () => {
    globalThis.fetch = vi.fn().mockResolvedValue({
      ok: false,
      status: 500,
    });

    const result = await fetchOllamaModels('http://localhost:11434');

    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.error).toContain('HTTP 500');
    }
  });

  it('returns error when Ollama is unreachable', async () => {
    globalThis.fetch = vi.fn().mockRejectedValue(new Error('ECONNREFUSED'));

    const result = await fetchOllamaModels('http://localhost:11434');

    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.error).toContain('Could not connect to Ollama');
      expect(result.error).toContain('ECONNREFUSED');
    }
  });

  it('returns error on timeout', async () => {
    const abortError = new DOMException('The operation was aborted', 'AbortError');
    globalThis.fetch = vi.fn().mockRejectedValue(abortError);

    const result = await fetchOllamaModels('http://localhost:11434');

    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.error).toContain('timed out');
    }
  });
});

describe('downloadModel', () => {
  const originalFetch = globalThis.fetch;
  let tempDir: string;

  beforeEach(() => {
    vi.restoreAllMocks();
    tempDir = mkdtempSync(join(tmpdir(), 'tom-model-download-'));
  });

  afterEach(() => {
    globalThis.fetch = originalFetch;
    rmSync(tempDir, { recursive: true, force: true });
  });

  it('streams a model download to a temp file before renaming it into place', async () => {
    const destPath = join(tempDir, 'models', 'model.bin');
    const onProgress = vi.fn();
    globalThis.fetch = vi.fn().mockResolvedValue({
      ok: true,
      headers: new Headers({ 'content-length': '10' }),
      body: new ReadableStream({
        start(controller) {
          controller.enqueue(new Uint8Array(Buffer.from('hello')));
          controller.enqueue(new Uint8Array(Buffer.from('world')));
          controller.close();
        },
      }),
    });

    await downloadModel('https://example.com/model.bin', destPath, onProgress);

    expect(readFileSync(destPath, 'utf8')).toBe('helloworld');
    expect(() => readFileSync(destPath + '.downloading')).toThrow();
    expect(onProgress).toHaveBeenLastCalledWith(10, 10);
    expect(globalThis.fetch).toHaveBeenCalledWith(
      'https://example.com/model.bin',
      expect.objectContaining({ signal: expect.any(AbortSignal) }),
    );
  });

  it('throws when the model download response is not successful', async () => {
    globalThis.fetch = vi.fn().mockResolvedValue({
      ok: false,
      status: 503,
      statusText: 'Service Unavailable',
    });

    await expect(
      downloadModel('https://example.com/model.bin', join(tempDir, 'model.bin'), vi.fn()),
    ).rejects.toThrow('HTTP 503 Service Unavailable');
  });

  it('throws when the response has no readable body', async () => {
    globalThis.fetch = vi.fn().mockResolvedValue({
      ok: true,
      headers: new Headers(),
      body: null,
    });

    await expect(
      downloadModel('https://example.com/model.bin', join(tempDir, 'model.bin'), vi.fn()),
    ).rejects.toThrow('no response body');
  });

  it('removes the partial temp file when streaming fails', async () => {
    const destPath = join(tempDir, 'models', 'model.bin');
    globalThis.fetch = vi.fn().mockResolvedValue({
      ok: true,
      headers: new Headers(),
      body: new ReadableStream({
        pull(controller) {
          controller.enqueue(new Uint8Array(Buffer.from('partial')));
          controller.error(new Error('stream failed'));
        },
      }),
    });

    await expect(downloadModel('https://example.com/model.bin', destPath, vi.fn())).rejects.toThrow(
      'stream failed',
    );
    expect(() => readFileSync(destPath)).toThrow();
    expect(() => readFileSync(destPath + '.downloading')).toThrow();
  });
});

describe('extractTarBz2', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('creates the target directory and extracts through tar', () => {
    const tempDir = mkdtempSync(join(tmpdir(), 'tom-extract-'));
    const targetDir = join(tempDir, 'model');
    const archivePath = join(tempDir, 'model.tar.bz2');
    writeFileSync(archivePath, '');

    try {
      extractTarBz2(archivePath, targetDir);

      expect(existsSync(targetDir)).toBe(true);
      expect(childProcessMocks.execFileSync).toHaveBeenCalledWith('tar', [
        'xjf',
        archivePath,
        '-C',
        targetDir,
      ]);
    } finally {
      rmSync(tempDir, { recursive: true, force: true });
    }
  });

  it('propagates tar extraction failures', () => {
    childProcessMocks.execFileSync.mockImplementationOnce(() => {
      throw new Error('tar failed');
    });
    const tempDir = mkdtempSync(join(tmpdir(), 'tom-extract-'));
    const targetDir = join(tempDir, 'model');

    try {
      expect(() => extractTarBz2(join(tempDir, 'model.tar.bz2'), targetDir)).toThrow('tar failed');
    } finally {
      rmSync(tempDir, { recursive: true, force: true });
    }
  });
});

describe('buildSetupConfig', () => {
  it('builds the persisted app config from setup choices', () => {
    const config = buildSetupConfig({
      llm: {
        provider: 'local',
        localEndpoint: 'http://localhost:11434',
        modelId: 'qwen2.5:7b',
      },
      embedding: { provider: 'none', model: '' },
      homePath: '/tmp/tom',
      modelsPath: '/tmp/tom/models',
      whisperModelFilename: 'ggml-distil-small.en.bin',
      liveTranscription: { provider: 'none' },
    });

    expect(config).toEqual({
      llm: {
        provider: 'local',
        localEndpoint: 'http://localhost:11434',
        modelId: 'qwen2.5:7b',
      },
      stt: {
        engine: 'whisper.node',
        modelPath: '/tmp/tom/models/ggml-distil-small.en.bin',
      },
      embedding: { provider: 'none', model: '' },
      storage: {
        dbPath: '/tmp/tom/tom.db',
      },
      liveTranscription: { provider: 'none' },
    });
  });
});
