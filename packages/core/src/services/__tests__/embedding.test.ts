import { describe, it, expect, vi, afterEach } from 'vitest';
import { OllamaEmbeddingService, NoopEmbeddingService } from '../embedding.js';

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
    expect(mockFetch).toHaveBeenCalledWith('http://localhost:11434/api/embeddings', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ model: 'nomic-embed-text', prompt: 'Hello, world!' }),
    });
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
