import { REINDEX_BATCH_SIZE } from '../constants.js';
import type { ServiceContainer } from './service-factory.js';

export interface ReindexProgress {
  total: number;
  processed: number;
  updated: number;
  failed: number;
}

export interface ReindexResult {
  total: number;
  updated: number;
  failed: number;
  embeddingAvailable: boolean;
}

export interface ReindexOptions {
  batchSize?: number;
  onProgress?: (progress: ReindexProgress) => void;
}

export async function reindexEntries(
  services: ServiceContainer,
  options: ReindexOptions = {},
): Promise<ReindexResult> {
  const available = await services.embedding.isAvailable();
  if (!available) {
    return { total: 0, updated: 0, failed: 0, embeddingAvailable: false };
  }

  const batchSize = options.batchSize ?? REINDEX_BATCH_SIZE;
  const total = await services.storage.countEntries();
  let processed = 0;
  let updated = 0;
  let failed = 0;

  while (processed < total) {
    const entries = await services.storage.listEntries({ limit: batchSize, offset: processed });
    if (entries.length === 0) break;

    for (const entry of entries) {
      try {
        const embedding = await services.embedding.embed(entry.content);
        await services.storage.updateEntryEmbedding(entry.id, embedding);
        updated++;
      } catch {
        failed++;
      } finally {
        processed++;
      }
    }

    options.onProgress?.({ total, processed, updated, failed });
  }

  return { total, updated, failed, embeddingAvailable: true };
}
