import { describe, it, expect } from 'vitest';
import Database from 'better-sqlite3';
import * as sqliteVec from 'sqlite-vec';
import { mkdtempSync, readFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';

describe('local migration contract', () => {
  it('creates the runtime tables, indexes, and FTS/vector objects', () => {
    const tempDir = mkdtempSync(join(tmpdir(), 'tst-migration-'));
    const db = new Database(join(tempDir, 'migration.db'));

    try {
      sqliteVec.load(db);
      const migrationSql = readFileSync(
        resolve(process.cwd(), 'migrations/local/001_entries.sql'),
        'utf-8',
      );
      db.exec(migrationSql);

      const objects = db
        .prepare("SELECT name FROM sqlite_master WHERE type IN ('table', 'index', 'trigger')")
        .all() as Array<{ name: string }>;
      const names = new Set(objects.map((object) => object.name));

      expect(names).toContain('entries');
      expect(names).toContain('entries_fts');
      expect(names).toContain('entry_embeddings');
      expect(names).toContain('idx_entries_type');
      expect(names).toContain('idx_entries_created');
      expect(names).toContain('entries_ai');
      expect(names).toContain('entries_ad');
      expect(names).toContain('entries_au');

      db.prepare(
        "INSERT INTO entries (id, type, content, input_method) VALUES ('id-1', 'note', 'hello world', 'typed')",
      ).run();
      const row = db.prepare('SELECT created_at FROM entries WHERE id = ?').get('id-1') as {
        created_at: string;
      };
      expect(row.created_at).toMatch(/^\d{4}-\d{2}-\d{2}T/);
    } finally {
      db.close();
      rmSync(tempDir, { recursive: true, force: true });
    }
  });
});
