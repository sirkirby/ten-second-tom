import type { Entry } from '../types/entry.js';
import type { IStorageService } from './storage.js';
import type { IEmbeddingService } from './embedding.js';

export interface ISearchService {
  search(query: string, limit?: number): Promise<Entry[]>;
}

export class SearchService implements ISearchService {
  constructor(
    private readonly storage: IStorageService,
    private readonly embedding: IEmbeddingService,
  ) {}

  async search(query: string, limit: number = 10): Promise<Entry[]> {
    const embeddingAvailable = await this.embedding.isAvailable();
    if (embeddingAvailable) {
      try {
        const queryEmbedding = await this.embedding.embed(query);
        const vectorResults = await this.storage.searchByVector(queryEmbedding, limit);
        // If vector search returned results, use them. If empty (no embeddings
        // stored yet), fall through to FTS so the user still gets results.
        if (vectorResults.length > 0) {
          return vectorResults;
        }
      } catch {
        // Fall through to FTS
      }
    }
    return this.storage.searchByKeyword(query, limit);
  }
}
