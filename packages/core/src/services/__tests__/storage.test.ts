import { describe, it, expect, afterEach } from 'vitest';
import { mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { SqliteStorageService } from '../storage-sqlite.js';
import type { CreateEntry } from '../../types/entry.js';
import { DEFAULT_EMBEDDING_DIMENSION } from '../../constants.js';

let tempDir: string;
let service: SqliteStorageService;

afterEach(() => {
  service?.close();
  if (tempDir) {
    rmSync(tempDir, { recursive: true, force: true });
  }
});

function createService(embeddingDimension?: number): SqliteStorageService {
  tempDir = mkdtempSync(join(tmpdir(), 'tst-storage-'));
  service = new SqliteStorageService(join(tempDir, 'test.db'), embeddingDimension);
  return service;
}

/**
 * Build a Float32Array of the given dimension (defaults to DEFAULT_EMBEDDING_DIMENSION)
 * all set to `value`. Using the real dimension ensures tests exercise the actual
 * vec0 table schema.
 */
function makeEmbedding(
  value: number,
  dimension: number = DEFAULT_EMBEDDING_DIMENSION,
): Float32Array {
  return new Float32Array(dimension).fill(value);
}

const baseNote: CreateEntry = {
  type: 'note',
  content: 'This is a test note',
  inputMethod: 'typed',
};

describe('SqliteStorageService', () => {
  it('saves and retrieves an entry', async () => {
    const svc = createService();

    const saved = await svc.saveEntry(baseNote);

    expect(saved.id).toBeDefined();
    expect(saved.id).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/);
    expect(saved.content).toBe(baseNote.content);
    expect(saved.type).toBe(baseNote.type);
    expect(saved.inputMethod).toBe(baseNote.inputMethod);
    expect(saved.createdAt).toBeDefined();
    expect(saved.updatedAt).toBeDefined();

    const retrieved = await svc.getEntry(saved.id);
    expect(retrieved).toBeDefined();
    expect(retrieved?.id).toBe(saved.id);
    expect(retrieved?.content).toBe(saved.content);
  });

  it('returns undefined for non-existent entry', async () => {
    const svc = createService();

    const result = await svc.getEntry('00000000-0000-0000-0000-000000000000');
    expect(result).toBeUndefined();
  });

  it('lists entries in reverse chronological order', async () => {
    const svc = createService();

    const first = await svc.saveEntry({ ...baseNote, content: 'First note' });
    const second = await svc.saveEntry({ ...baseNote, content: 'Second note' });
    const third = await svc.saveEntry({ ...baseNote, content: 'Third note' });

    const entries = await svc.listEntries({ limit: 10 });

    expect(entries).toHaveLength(3);
    // Newest first — third was inserted last
    expect(entries[0]?.id).toBe(third.id);
    expect(entries[1]?.id).toBe(second.id);
    expect(entries[2]?.id).toBe(first.id);
  });

  it('filters entries by type', async () => {
    const svc = createService();

    const note = await svc.saveEntry({ ...baseNote, type: 'note', content: 'A note' });
    await svc.saveEntry({
      type: 'recording',
      content: 'A recording transcript',
      inputMethod: 'recorded',
      audioPath: '/tmp/audio.wav',
    });

    const notes = await svc.listEntries({ type: 'note', limit: 10 });

    expect(notes).toHaveLength(1);
    expect(notes[0]?.id).toBe(note.id);
    expect(notes[0]?.type).toBe('note');
  });

  it('updates entry with analysis', async () => {
    const svc = createService();

    const saved = await svc.saveEntry(baseNote);

    const analysis = {
      sentiment: { score: 0.8, label: 'positive', confidence: 0.95 },
      summary: 'A very positive note',
      raw: { topics: ['test'] },
    };

    await svc.updateEntryAnalysis(saved.id, analysis);

    const updated = await svc.getEntry(saved.id);
    expect(updated?.analysis).toBeDefined();
    expect(updated?.analysis?.sentiment.score).toBe(0.8);
    expect(updated?.analysis?.sentiment.label).toBe('positive');
    expect(updated?.analysis?.summary).toBe('A very positive note');
  });

  it('searches entries by keyword (FTS)', async () => {
    const svc = createService();

    const matching = await svc.saveEntry({
      ...baseNote,
      content: 'The dashboard deployment went smoothly today',
    });
    await svc.saveEntry({
      ...baseNote,
      content: 'Had a team lunch meeting',
    });

    const results = await svc.searchByKeyword('dashboard');

    expect(results).toHaveLength(1);
    expect(results[0]?.id).toBe(matching.id);
  });

  it('deletes an entry', async () => {
    const svc = createService();

    const saved = await svc.saveEntry(baseNote);

    await svc.deleteEntry(saved.id);

    const result = await svc.getEntry(saved.id);
    expect(result).toBeUndefined();
  });

  // --- Vector embedding tests ---

  it('stores and retrieves embedding for an entry without error', async () => {
    const svc = createService();

    const entry = await svc.saveEntry(baseNote);
    const embedding = makeEmbedding(0.5);

    // Should not throw
    await expect(svc.updateEntryEmbedding(entry.id, embedding)).resolves.toBeUndefined();
  });

  it('overwrites an existing embedding for the same entry', async () => {
    const svc = createService();

    const entry = await svc.saveEntry(baseNote);

    await svc.updateEntryEmbedding(entry.id, makeEmbedding(0.1));
    // Second call with a different vector should not throw
    await expect(svc.updateEntryEmbedding(entry.id, makeEmbedding(0.9))).resolves.toBeUndefined();
  });

  it('searches entries by vector similarity and returns closest match first', async () => {
    const svc = createService();

    // Three entries with clearly different embeddings
    const closeEntry = await svc.saveEntry({ ...baseNote, content: 'Close to query' });
    const farEntry = await svc.saveEntry({ ...baseNote, content: 'Far from query' });
    const midEntry = await svc.saveEntry({ ...baseNote, content: 'Mid distance' });

    // Assign embeddings: close=0.1, far=0.9, mid=0.5
    await svc.updateEntryEmbedding(closeEntry.id, makeEmbedding(0.1));
    await svc.updateEntryEmbedding(farEntry.id, makeEmbedding(0.9));
    await svc.updateEntryEmbedding(midEntry.id, makeEmbedding(0.5));

    // Query vector near 0.1 — closest match should be closeEntry
    const results = await svc.searchByVector(makeEmbedding(0.15), 3);

    expect(results).toHaveLength(3);
    expect(results[0]?.id).toBe(closeEntry.id);
  });

  it('searchByVector returns only entries with embeddings', async () => {
    const svc = createService();

    const withEmbedding = await svc.saveEntry({ ...baseNote, content: 'Has embedding' });
    await svc.saveEntry({ ...baseNote, content: 'No embedding' });

    await svc.updateEntryEmbedding(withEmbedding.id, makeEmbedding(0.5));

    const results = await svc.searchByVector(makeEmbedding(0.5), 10);

    expect(results).toHaveLength(1);
    expect(results[0]?.id).toBe(withEmbedding.id);
  });

  it('searchByVector returns empty array when no embeddings exist', async () => {
    const svc = createService();

    await svc.saveEntry(baseNote);

    const results = await svc.searchByVector(makeEmbedding(0.5), 10);

    expect(results).toHaveLength(0);
  });

  it('accepts a custom embedding dimension', async () => {
    const dim = 1024;
    const svc = createService(dim);

    const entry = await svc.saveEntry(baseNote);
    const embedding = makeEmbedding(0.5, dim);

    await expect(svc.updateEntryEmbedding(entry.id, embedding)).resolves.toBeUndefined();
  });

  it('recreates vec0 table when dimension changes on the same database', async () => {
    // Create a database with 768-dim embeddings
    tempDir = mkdtempSync(join(tmpdir(), 'tst-storage-'));
    const dbPath = join(tempDir, 'test.db');

    const svc768 = new SqliteStorageService(dbPath, 768);
    const entry = await svc768.saveEntry(baseNote);
    await svc768.updateEntryEmbedding(entry.id, makeEmbedding(0.5, 768));
    svc768.close();

    // Re-open with 1024-dim — should drop and recreate the vec0 table
    service = new SqliteStorageService(dbPath, 1024);

    // Old embedding is gone (table was recreated)
    const results = await service.searchByVector(makeEmbedding(0.5, 1024), 10);
    expect(results).toHaveLength(0);

    // Can insert new 1024-dim embedding without error
    await expect(
      service.updateEntryEmbedding(entry.id, makeEmbedding(0.3, 1024)),
    ).resolves.toBeUndefined();
  });
});
