import { describe, it, expect, vi, afterEach } from 'vitest';
import {
  OllamaEmbeddingService,
  NoopEmbeddingService,
  OpenAICompatibleEmbeddingService,
} from '../embedding.js';

afterEach(() => {
  vi.restoreAllMocks();
});

describe('OllamaEmbeddingService', () => {
  it('generates embeddings via API', async () => {
    const embeddingValues = Array.from({ length: 768 }, (_, i) => (i + 1) / 1000);
    const mockFetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({ embedding: embeddingValues }),
    });
    vi.stubGlobal('fetch', mockFetch);

    const service = new OllamaEmbeddingService({
      model: 'nomic-embed-text',
      endpoint: 'http://localhost:11434',
    });

    const result = await service.embed('Hello, world!');

    expect(result).toBeInstanceOf(Float32Array);
    expect(result.length).toBe(768);
    expect(result[0]).toBeCloseTo(0.001);
    expect(mockFetch).toHaveBeenCalledWith(
      'http://localhost:11434/api/embeddings',
      expect.objectContaining({
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ model: 'nomic-embed-text', prompt: 'Hello, world!' }),
        signal: expect.any(AbortSignal),
      }),
    );
  });

  it('throws when the API call fails', async () => {
    const mockFetch = vi.fn().mockRejectedValue(new Error('Network error'));
    vi.stubGlobal('fetch', mockFetch);

    const service = new OllamaEmbeddingService({
      model: 'nomic-embed-text',
      endpoint: 'http://localhost:11434',
    });

    await expect(service.embed('Hello')).rejects.toThrow('Network error');
  });

  it('throws when the API returns a non-ok response', async () => {
    const mockFetch = vi.fn().mockResolvedValue({
      ok: false,
      status: 500,
      statusText: 'Internal Server Error',
    });
    vi.stubGlobal('fetch', mockFetch);

    const service = new OllamaEmbeddingService({
      model: 'nomic-embed-text',
      endpoint: 'http://localhost:11434',
    });

    await expect(service.embed('Hello')).rejects.toThrow(
      'Embedding request failed: 500 Internal Server Error',
    );
  });

  it('reports availability via health check', async () => {
    const mockFetch = vi.fn().mockResolvedValue({ ok: true });
    vi.stubGlobal('fetch', mockFetch);

    const service = new OllamaEmbeddingService({
      model: 'nomic-embed-text',
      endpoint: 'http://localhost:11434',
    });

    const available = await service.isAvailable();

    expect(available).toBe(true);
    expect(mockFetch).toHaveBeenCalledWith('http://localhost:11434', {
      signal: expect.any(AbortSignal),
    });
  });

  it('reports unavailable when health check fails', async () => {
    const mockFetch = vi.fn().mockRejectedValue(new Error('Connection refused'));
    vi.stubGlobal('fetch', mockFetch);

    const service = new OllamaEmbeddingService({
      model: 'nomic-embed-text',
      endpoint: 'http://localhost:11434',
    });

    const available = await service.isAvailable();

    expect(available).toBe(false);
  });

  it('reports unavailable when health check returns non-ok response', async () => {
    const mockFetch = vi.fn().mockResolvedValue({ ok: false });
    vi.stubGlobal('fetch', mockFetch);

    const service = new OllamaEmbeddingService({
      model: 'nomic-embed-text',
      endpoint: 'http://localhost:11434',
    });

    const available = await service.isAvailable();

    expect(available).toBe(false);
  });

  it('caches availability result for subsequent calls', async () => {
    const mockFetch = vi.fn().mockResolvedValue({ ok: true });
    vi.stubGlobal('fetch', mockFetch);

    const service = new OllamaEmbeddingService({
      model: 'nomic-embed-text',
      endpoint: 'http://localhost:11434',
    });

    // First call hits the network
    const first = await service.isAvailable();
    expect(first).toBe(true);
    expect(mockFetch).toHaveBeenCalledTimes(1);

    // Second call returns cached result — no new fetch
    const second = await service.isAvailable();
    expect(second).toBe(true);
    expect(mockFetch).toHaveBeenCalledTimes(1);
  });
});

describe('NoopEmbeddingService', () => {
  it('is never available', async () => {
    const service = new NoopEmbeddingService();
    const available = await service.isAvailable();
    expect(available).toBe(false);
  });

  it('throws on embed', async () => {
    const service = new NoopEmbeddingService();
    await expect(service.embed('anything')).rejects.toThrow('No embedding provider configured');
  });
});

describe('OpenAICompatibleEmbeddingService', () => {
  it('generates embeddings via OpenAI-compatible API', async () => {
    const embeddingValues = Array.from({ length: 1536 }, (_, i) => (i + 1) / 1000);
    const mockFetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({ data: [{ embedding: embeddingValues, index: 0 }] }),
    });
    vi.stubGlobal('fetch', mockFetch);

    const service = new OpenAICompatibleEmbeddingService({
      baseUrl: 'https://api.openai.com/v1',
      model: 'text-embedding-3-small',
      apiKey: 'sk-test-key',
    });

    const result = await service.embed('Hello, world!');

    expect(result).toBeInstanceOf(Float32Array);
    expect(result.length).toBe(1536);
    expect(result[0]).toBeCloseTo(0.001);
    expect(mockFetch).toHaveBeenCalledWith(
      'https://api.openai.com/v1/embeddings',
      expect.objectContaining({
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Authorization: 'Bearer sk-test-key',
        },
        body: JSON.stringify({ input: 'Hello, world!', model: 'text-embedding-3-small' }),
        signal: expect.any(AbortSignal),
      }),
    );
  });

  it('omits Authorization header when no apiKey provided', async () => {
    const embeddingValues = [0.1, 0.2, 0.3];
    const mockFetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({ data: [{ embedding: embeddingValues, index: 0 }] }),
    });
    vi.stubGlobal('fetch', mockFetch);

    const service = new OpenAICompatibleEmbeddingService({
      baseUrl: 'http://localhost:11434/v1',
      model: 'nomic-embed-text',
    });

    await service.embed('test');

    const [, callOptions] = mockFetch.mock.calls[0] as [string, RequestInit];
    expect(callOptions.headers).toEqual({ 'Content-Type': 'application/json' });
    expect((callOptions.headers as Record<string, string>)['Authorization']).toBeUndefined();
  });

  it('throws when the API returns a non-ok response', async () => {
    const mockFetch = vi.fn().mockResolvedValue({
      ok: false,
      status: 401,
      statusText: 'Unauthorized',
    });
    vi.stubGlobal('fetch', mockFetch);

    const service = new OpenAICompatibleEmbeddingService({
      baseUrl: 'https://api.openai.com/v1',
      model: 'text-embedding-3-small',
      apiKey: 'bad-key',
    });

    await expect(service.embed('Hello')).rejects.toThrow(
      'Embedding request failed: 401 Unauthorized',
    );
  });

  it('throws when the network request fails', async () => {
    const mockFetch = vi.fn().mockRejectedValue(new Error('Network error'));
    vi.stubGlobal('fetch', mockFetch);

    const service = new OpenAICompatibleEmbeddingService({
      baseUrl: 'https://api.openai.com/v1',
      model: 'text-embedding-3-small',
      apiKey: 'sk-test-key',
    });

    await expect(service.embed('Hello')).rejects.toThrow('Network error');
  });

  it('checks availability with a minimal embed request', async () => {
    const mockFetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({ data: [{ embedding: [0.1], index: 0 }] }),
    });
    vi.stubGlobal('fetch', mockFetch);

    const service = new OpenAICompatibleEmbeddingService({
      baseUrl: 'https://api.openai.com/v1',
      model: 'text-embedding-3-small',
      apiKey: 'sk-test-key',
    });

    const available = await service.isAvailable();

    expect(available).toBe(true);
    expect(mockFetch).toHaveBeenCalledWith(
      'https://api.openai.com/v1/embeddings',
      expect.objectContaining({
        method: 'POST',
        body: JSON.stringify({ input: 'test', model: 'text-embedding-3-small' }),
        signal: expect.any(AbortSignal),
      }),
    );
  });

  it('reports unavailable when availability check fails', async () => {
    const mockFetch = vi.fn().mockRejectedValue(new Error('Network error'));
    vi.stubGlobal('fetch', mockFetch);

    const service = new OpenAICompatibleEmbeddingService({
      baseUrl: 'https://api.openai.com/v1',
      model: 'text-embedding-3-small',
      apiKey: 'sk-test-key',
    });

    const available = await service.isAvailable();

    expect(available).toBe(false);
  });

  it('caches availability result for subsequent calls', async () => {
    const mockFetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({ data: [{ embedding: [0.1], index: 0 }] }),
    });
    vi.stubGlobal('fetch', mockFetch);

    const service = new OpenAICompatibleEmbeddingService({
      baseUrl: 'https://api.openai.com/v1',
      model: 'text-embedding-3-small',
      apiKey: 'sk-test-key',
    });

    const first = await service.isAvailable();
    expect(first).toBe(true);
    expect(mockFetch).toHaveBeenCalledTimes(1);

    const second = await service.isAvailable();
    expect(second).toBe(true);
    expect(mockFetch).toHaveBeenCalledTimes(1);
  });
});
