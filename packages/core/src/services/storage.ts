import type { Entry, CreateEntry, EntryAnalysis } from '../types/entry.js';

export interface ListEntriesOptions {
  type?: 'recording' | 'note';
  limit: number;
  offset?: number;
}

export interface IStorageService {
  saveEntry(input: CreateEntry): Promise<Entry>;
  getEntry(id: string): Promise<Entry | undefined>;
  listEntries(options: ListEntriesOptions): Promise<Entry[]>;
  updateEntryAnalysis(id: string, analysis: EntryAnalysis): Promise<void>;
  updateEntryEmbedding(id: string, embedding: Float32Array): Promise<void>;
  searchByKeyword(query: string, limit?: number): Promise<Entry[]>;
  searchByVector(embedding: Float32Array, limit: number): Promise<Entry[]>;
  deleteEntry(id: string): Promise<void>;
  close(): void;
}
