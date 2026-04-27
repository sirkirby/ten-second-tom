import { describe, it, expect, vi } from 'vitest';
import { reindexEntries } from '../reindex.js';
import type { ServiceContainer } from '../service-factory.js';
import type { Entry } from '../../types/entry.js';

function makeEntry(id: string, content: string): Entry {
  const now = new Date().toISOString();
  return {
    id,
    type: 'note',
    content,
    audioPath: undefined,
    durationSeconds: undefined,
    analysis: undefined,
    createdAt: now,
    updatedAt: now,
  };
}

function makeServices(entries: Entry[], embeddingAvailable = true): ServiceContainer {
  return {
    embedding: {
      isAvailable: vi.fn().mockResolvedValue(embeddingAvailable),
      embed: vi.fn().mockResolvedValue(new Float32Array([1, 2, 3])),
    },
    storage: {
      countEntries: vi.fn().mockResolvedValue(entries.length),
      listEntries: vi.fn(({ limit, offset = 0 }: { limit: number; offset?: number }) =>
        Promise.resolve(entries.slice(offset, offset + limit)),
      ),
      updateEntryEmbedding: vi.fn().mockResolvedValue(undefined),
    },
  } as unknown as ServiceContainer;
}

describe('reindexEntries', () => {
  it('returns without scanning entries when embeddings are unavailable', async () => {
    const services = makeServices([makeEntry('entry-1', 'hello')], false);

    const result = await reindexEntries(services);

    expect(result).toEqual({
      total: 0,
      updated: 0,
      failed: 0,
      embeddingAvailable: false,
    });
    expect(services.storage.countEntries).not.toHaveBeenCalled();
  });

  it('embeds entries in paginated batches and reports progress', async () => {
    const entries = [
      makeEntry('entry-1', 'one'),
      makeEntry('entry-2', 'two'),
      makeEntry('entry-3', 'three'),
    ];
    const services = makeServices(entries);
    const onProgress = vi.fn();

    const result = await reindexEntries(services, { batchSize: 2, onProgress });

    expect(result).toEqual({
      total: 3,
      updated: 3,
      failed: 0,
      embeddingAvailable: true,
    });
    expect(services.storage.listEntries).toHaveBeenCalledWith({ limit: 2, offset: 0 });
    expect(services.storage.listEntries).toHaveBeenCalledWith({ limit: 2, offset: 2 });
    expect(services.embedding.embed).toHaveBeenNthCalledWith(1, 'one');
    expect(services.embedding.embed).toHaveBeenNthCalledWith(2, 'two');
    expect(services.embedding.embed).toHaveBeenNthCalledWith(3, 'three');
    expect(services.storage.updateEntryEmbedding).toHaveBeenNthCalledWith(
      1,
      'entry-1',
      expect.any(Float32Array),
    );
    expect(onProgress).toHaveBeenLastCalledWith({
      total: 3,
      processed: 3,
      updated: 3,
      failed: 0,
    });
  });

  it('counts failed entries and continues indexing the remaining entries', async () => {
    const entries = [makeEntry('entry-1', 'one'), makeEntry('entry-2', 'two')];
    const services = makeServices(entries);
    vi.mocked(services.embedding.embed)
      .mockResolvedValueOnce(new Float32Array([1]))
      .mockRejectedValueOnce(new Error('embedding failed'));

    const result = await reindexEntries(services, { batchSize: 10 });

    expect(result).toEqual({
      total: 2,
      updated: 1,
      failed: 1,
      embeddingAvailable: true,
    });
    expect(services.storage.updateEntryEmbedding).toHaveBeenCalledTimes(1);
  });
});
