import type { Entry } from '../types/entry.js';
import type { IStorageService } from './storage.js';
import type { IEmbeddingService } from './embedding.js';

export class SearchService {
  constructor(
    private readonly storage: IStorageService,
    private readonly embedding: IEmbeddingService,
  ) {}

  async search(query: string, limit: number = 20): Promise<Entry[]> {
    const embeddingAvailable = await this.embedding.isAvailable();
    if (embeddingAvailable) {
      try {
        const queryEmbedding = await this.embedding.embed(query);
        return await this.storage.searchByVector(queryEmbedding, limit);
      } catch {
        // Fall through to FTS
      }
    }
    return this.storage.searchByKeyword(query);
  }
}
