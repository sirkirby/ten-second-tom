import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

// Mock Ink/React/components so setup.tsx can be imported without rendering
vi.mock('ink', () => ({
  render: vi.fn(),
  Box: vi.fn(),
  Text: vi.fn(),
  useApp: vi.fn(() => ({ exit: vi.fn() })),
  useInput: vi.fn(),
}));

vi.mock('react', () => ({
  default: { createElement: vi.fn() },
  useState: vi.fn(() => [null, vi.fn()]),
  useEffect: vi.fn(),
  useCallback: vi.fn((fn: unknown) => fn),
  useMemo: vi.fn((fn: () => unknown) => fn()),
}));

vi.mock('ink-select-input', () => ({ default: vi.fn() }));
vi.mock('ink-text-input', () => ({ default: vi.fn() }));
vi.mock('../../hooks/useAutoExit.js', () => ({ useAutoExit: vi.fn() }));
vi.mock('@ten-second-tom/core', () => ({
  ConfigManager: vi.fn().mockImplementation(() => ({
    homePath: '/tmp/test',
    modelsPath: '/tmp/test/models',
    audioPath: '/tmp/test/audio',
    save: vi.fn(),
  })),
  // Constants used at module scope in setup.tsx
  DEFAULT_OLLAMA_ENDPOINT: 'http://localhost:11434',
  DEFAULT_LOCAL_MODEL_ID: 'qwen2.5:7b',
  DEFAULT_OLLAMA_EMBEDDING_MODEL: 'nomic-embed-text',
  DEFAULT_OPENROUTER_EMBEDDING_MODEL: 'openai/text-embedding-3-small',
  ANTHROPIC_API_KEY_PREFIX: 'sk-ant-',
  // Model registries
  WHISPER_MODELS: [
    {
      id: 'distil-small.en',
      filename: 'ggml-distil-small.en.bin',
      url: 'https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-distil-small.en.bin',
      sizeBytes: 380_000_000,
      sizeLabel: '380 MB',
      description: 'English, fast, good accuracy',
      recommended: true,
    },
  ],
  getDefaultWhisperModel: () => ({
    id: 'distil-small.en',
    filename: 'ggml-distil-small.en.bin',
    url: 'https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-distil-small.en.bin',
    sizeBytes: 380_000_000,
    sizeLabel: '380 MB',
    description: 'English, fast, good accuracy',
    recommended: true,
  }),
  SHERPA_MODELS: [
    {
      id: 'zipformer-en-2023-06-26',
      dirName: 'sherpa-onnx-streaming-zipformer-en-2023-06-26',
      archiveFilename: 'sherpa-onnx-streaming-zipformer-en-2023-06-26.tar.bz2',
      url: 'https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/sherpa-onnx-streaming-zipformer-en-2023-06-26.tar.bz2',
      sizeBytes: 68_000_000,
      sizeLabel: '68 MB',
      description: 'English streaming, good balance',
      recommended: true,
      encoderFilename: 'encoder-epoch-99-avg-1-chunk-16-left-128.int8.onnx',
      decoderFilename: 'decoder-epoch-99-avg-1-chunk-16-left-128.onnx',
      joinerFilename: 'joiner-epoch-99-avg-1-chunk-16-left-128.int8.onnx',
      tokensFilename: 'tokens.txt',
    },
  ],
}));

import { fetchOllamaModels } from '../setup.js';

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

  it('returns error when Ollama is unreachable (fetch throws)', async () => {
    globalThis.fetch = vi.fn().mockRejectedValue(new Error('ECONNREFUSED'));

    const result = await fetchOllamaModels('http://localhost:11434');

    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.error).toContain('Could not connect to Ollama');
      expect(result.error).toContain('ECONNREFUSED');
    }
  });

  it('returns error on timeout (AbortError)', async () => {
    const abortError = new DOMException('The operation was aborted', 'AbortError');
    globalThis.fetch = vi.fn().mockRejectedValue(abortError);

    const result = await fetchOllamaModels('http://localhost:11434');

    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.error).toContain('timed out');
    }
  });

  it('handles response with missing models field', async () => {
    globalThis.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: () => Promise.resolve({}),
    });

    const result = await fetchOllamaModels('http://localhost:11434');

    expect(result.ok).toBe(true);
    if (result.ok) {
      expect(result.models).toHaveLength(0);
    }
  });
});
