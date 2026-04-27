import { describe, it, expect } from 'vitest';
import { DEFAULT_EMBEDDING_DIMENSION, getEmbeddingDimension } from '../constants.js';

describe('getEmbeddingDimension', () => {
  it('returns exact known dimensions', () => {
    expect(getEmbeddingDimension('openai/text-embedding-3-small')).toBe(1536);
  });

  it('handles Ollama tag suffixes', () => {
    expect(getEmbeddingDimension('bge-m3:latest')).toBe(1024);
  });

  it('matches model names case-insensitively', () => {
    expect(getEmbeddingDimension('NOMIC-EMBED-TEXT')).toBe(768);
  });

  it('falls back to the default dimension for unknown models', () => {
    expect(getEmbeddingDimension('unknown-model')).toBe(DEFAULT_EMBEDDING_DIMENSION);
  });
});
