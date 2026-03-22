import { describe, it, expect, afterEach } from 'vitest';
import { mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { SqliteStorageService } from '../storage-sqlite.js';
import type { CreateEntry } from '../../types/entry.js';

let tempDir: string;
let service: SqliteStorageService;

afterEach(() => {
  service?.close();
  if (tempDir) {
    rmSync(tempDir, { recursive: true, force: true });
  }
});

function createService(): SqliteStorageService {
  tempDir = mkdtempSync(join(tmpdir(), 'tst-storage-'));
  service = new SqliteStorageService(join(tempDir, 'test.db'));
  return service;
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
    expect(saved.id).toMatch(
      /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/,
    );
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
});
