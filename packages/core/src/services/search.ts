import type { Entry } from '../types/entry.js';
import type { IStorageService } from './storage.js';
import type { IEmbeddingService } from './embedding.js';

export interface SearchResult {
  entry: Entry;
  relevance: number; // 0-1, higher = more relevant
}

export interface ISearchService {
  search(query: string, limit?: number): Promise<SearchResult[]>;
}

/**
 * Assign a relevance score based on result position.
 * The first result gets 1.0, the last gets a floor near 0.
 * This is a heuristic — the underlying storage already returns results
 * ordered by relevance (vector distance or FTS rank).
 */
function scoreByPosition(entries: Entry[]): SearchResult[] {
  if (entries.length === 0) return [];
  const first = entries[0];
  if (entries.length === 1 && first) return [{ entry: first, relevance: 1 }];

  return entries.map((entry, i) => ({
    entry,
    relevance: Math.round(Math.max(0, 1 - i / (entries.length - 1)) * 100) / 100,
  }));
}

export class SearchService implements ISearchService {
  constructor(
    private readonly storage: IStorageService,
    private readonly embedding: IEmbeddingService,
  ) {}

  async search(query: string, limit: number = 10): Promise<SearchResult[]> {
    const embeddingAvailable = await this.embedding.isAvailable();
    if (embeddingAvailable) {
      try {
        const queryEmbedding = await this.embedding.embed(query);
        const vectorResults = await this.storage.searchByVector(queryEmbedding, limit);
        // If vector search returned results, use them. If empty (no embeddings
        // stored yet), fall through to FTS so the user still gets results.
        if (vectorResults.length > 0) {
          return scoreByPosition(vectorResults);
        }
      } catch {
        // Fall through to FTS
      }
    }
    return scoreByPosition(await this.storage.searchByKeyword(query, limit));
  }
}
