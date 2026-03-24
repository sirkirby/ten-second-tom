import { describe, it, expect, vi } from 'vitest';
import { SearchService } from '../search.js';
import type { IStorageService } from '../storage.js';
import type { IEmbeddingService } from '../embedding.js';
import type { Entry } from '../../types/entry.js';

const mockEntry: Entry = {
  id: 'test-id',
  type: 'note',
  content: 'The deploy pipeline broke again this morning',
  inputMethod: 'typed',
  createdAt: '2026-04-01T10:00:00.000Z',
  updatedAt: '2026-04-01T10:00:00.000Z',
};

const mockEntry2: Entry = {
  id: 'test-id-2',
  type: 'note',
  content: 'Second result for testing relevance scores',
  inputMethod: 'typed',
  createdAt: '2026-04-01T11:00:00.000Z',
  updatedAt: '2026-04-01T11:00:00.000Z',
};

function makeStorage(overrides?: Partial<IStorageService>): IStorageService {
  return {
    saveEntry: vi.fn(),
    getEntry: vi.fn(),
    listEntries: vi.fn(),
    countEntries: vi.fn().mockResolvedValue(0),
    updateEntryAnalysis: vi.fn(),
    updateEntryEmbedding: vi.fn(),
    searchByKeyword: vi.fn().mockResolvedValue([mockEntry]),
    searchByVector: vi.fn().mockResolvedValue([mockEntry]),
    deleteEntry: vi.fn(),
    close: vi.fn(),
    ...overrides,
  };
}

function makeEmbedding(overrides?: Partial<IEmbeddingService>): IEmbeddingService {
  return {
    isAvailable: vi.fn().mockResolvedValue(false),
    embed: vi.fn().mockResolvedValue(new Float32Array([0.1, 0.2, 0.3])),
    ...overrides,
  };
}

describe('SearchService', () => {
  it('uses semantic search when embedding is available', async () => {
    const storage = makeStorage();
    const embedding = makeEmbedding({
      isAvailable: vi.fn().mockResolvedValue(true),
    });
    const service = new SearchService(storage, embedding);

    const results = await service.search('deploy pipeline');

    expect(embedding.embed).toHaveBeenCalledWith('deploy pipeline');
    expect(storage.searchByVector).toHaveBeenCalledWith(expect.any(Float32Array), 10);
    expect(storage.searchByKeyword).not.toHaveBeenCalled();
    expect(results).toEqual([{ entry: mockEntry, relevance: 1 }]);
  });

  it('falls back to FTS when embedding is unavailable', async () => {
    const storage = makeStorage();
    const embedding = makeEmbedding({
      isAvailable: vi.fn().mockResolvedValue(false),
    });
    const service = new SearchService(storage, embedding);

    const results = await service.search('deploy pipeline');

    expect(embedding.embed).not.toHaveBeenCalled();
    expect(storage.searchByKeyword).toHaveBeenCalledWith('deploy pipeline', 10);
    expect(storage.searchByVector).not.toHaveBeenCalled();
    expect(results).toEqual([{ entry: mockEntry, relevance: 1 }]);
  });

  it('falls back to FTS when embedding throws', async () => {
    const storage = makeStorage();
    const embedding = makeEmbedding({
      isAvailable: vi.fn().mockResolvedValue(true),
      embed: vi.fn().mockRejectedValue(new Error('Model not loaded')),
    });
    const service = new SearchService(storage, embedding);

    const results = await service.search('deploy pipeline');

    expect(embedding.embed).toHaveBeenCalledWith('deploy pipeline');
    expect(storage.searchByKeyword).toHaveBeenCalledWith('deploy pipeline', 10);
    expect(results).toEqual([{ entry: mockEntry, relevance: 1 }]);
  });

  it('passes custom limit to searchByVector', async () => {
    const storage = makeStorage();
    const embedding = makeEmbedding({
      isAvailable: vi.fn().mockResolvedValue(true),
    });
    const service = new SearchService(storage, embedding);

    await service.search('query', 5);

    expect(storage.searchByVector).toHaveBeenCalledWith(expect.any(Float32Array), 5);
  });

  it('passes custom limit to searchByKeyword on FTS fallback', async () => {
    const storage = makeStorage();
    const embedding = makeEmbedding({
      isAvailable: vi.fn().mockResolvedValue(false),
    });
    const service = new SearchService(storage, embedding);

    await service.search('query', 10);

    expect(storage.searchByKeyword).toHaveBeenCalledWith('query', 10);
  });

  it('assigns descending relevance scores by position', async () => {
    const storage = makeStorage({
      searchByKeyword: vi.fn().mockResolvedValue([mockEntry, mockEntry2]),
    });
    const embedding = makeEmbedding({
      isAvailable: vi.fn().mockResolvedValue(false),
    });
    const service = new SearchService(storage, embedding);

    const results = await service.search('deploy');

    expect(results).toHaveLength(2);
    expect(results[0]).toEqual(expect.objectContaining({ relevance: 1 }));
    expect(results[1]).toEqual(expect.objectContaining({ relevance: 0 }));
    expect(results[0]).toEqual(
      expect.objectContaining({ entry: expect.objectContaining({ id: 'test-id' }) }),
    );
    expect(results[1]).toEqual(
      expect.objectContaining({ entry: expect.objectContaining({ id: 'test-id-2' }) }),
    );
  });

  it('returns empty array with no results', async () => {
    const storage = makeStorage({
      searchByKeyword: vi.fn().mockResolvedValue([]),
    });
    const embedding = makeEmbedding({
      isAvailable: vi.fn().mockResolvedValue(false),
    });
    const service = new SearchService(storage, embedding);

    const results = await service.search('nonexistent');

    expect(results).toEqual([]);
  });
});
