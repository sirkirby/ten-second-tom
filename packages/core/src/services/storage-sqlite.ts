import Database from 'better-sqlite3';
import { randomUUID } from 'node:crypto';
import type { Entry, CreateEntry, EntryAnalysis } from '../types/entry.js';
import type { IStorageService, ListEntriesOptions } from './storage.js';

const MIGRATION_SQL = `
CREATE TABLE IF NOT EXISTS entries (
  id TEXT PRIMARY KEY,
  type TEXT NOT NULL CHECK(type IN ('recording', 'note')),
  content TEXT NOT NULL,
  audio_path TEXT,
  input_method TEXT NOT NULL CHECK(input_method IN ('typed', 'dictated', 'recorded')),
  analysis TEXT,
  created_at TEXT NOT NULL DEFAULT (datetime('now')),
  updated_at TEXT NOT NULL DEFAULT (datetime('now'))
);
CREATE INDEX IF NOT EXISTS idx_entries_type ON entries(type);
CREATE INDEX IF NOT EXISTS idx_entries_created ON entries(created_at DESC);

CREATE VIRTUAL TABLE IF NOT EXISTS entries_fts USING fts5(content, content='entries', content_rowid='rowid');

CREATE TRIGGER IF NOT EXISTS entries_ai AFTER INSERT ON entries BEGIN
  INSERT INTO entries_fts(rowid, content) VALUES (new.rowid, new.content);
END;
CREATE TRIGGER IF NOT EXISTS entries_ad AFTER DELETE ON entries BEGIN
  INSERT INTO entries_fts(entries_fts, rowid, content) VALUES('delete', old.rowid, old.content);
END;
CREATE TRIGGER IF NOT EXISTS entries_au AFTER UPDATE ON entries BEGIN
  INSERT INTO entries_fts(entries_fts, rowid, content) VALUES('delete', old.rowid, old.content);
  INSERT INTO entries_fts(rowid, content) VALUES (new.rowid, new.content);
END;
`;

interface EntryRow {
  id: string;
  type: string;
  content: string;
  audio_path: string | null;
  input_method: string;
  analysis: string | null;
  created_at: string;
  updated_at: string;
}

function rowToEntry(row: EntryRow): Entry {
  return {
    id: row.id,
    type: row.type as Entry['type'],
    content: row.content,
    audioPath: row.audio_path ?? undefined,
    inputMethod: row.input_method as Entry['inputMethod'],
    analysis: row.analysis ? (JSON.parse(row.analysis) as EntryAnalysis) : undefined,
    createdAt: row.created_at,
    updatedAt: row.updated_at,
  };
}

export class SqliteStorageService implements IStorageService {
  private readonly db: Database.Database;

  // Prepared statements — cached after migrations run in constructor
  private readonly stmtInsertEntry: Database.Statement;
  private readonly stmtGetEntry: Database.Statement;
  private readonly stmtListByType: Database.Statement;
  private readonly stmtListAll: Database.Statement;
  private readonly stmtUpdateAnalysis: Database.Statement;
  private readonly stmtSearchFts: Database.Statement;
  private readonly stmtDeleteEntry: Database.Statement;

  constructor(dbPath: string) {
    this.db = new Database(dbPath);
    this.db.pragma('journal_mode = WAL');
    this.db.pragma('foreign_keys = ON');
    this.db.exec(MIGRATION_SQL);

    // Prepare all statements once
    this.stmtInsertEntry = this.db.prepare(
      `INSERT INTO entries (id, type, content, audio_path, input_method, created_at, updated_at)
       VALUES (@id, @type, @content, @audio_path, @input_method, @created_at, @updated_at)`,
    );
    this.stmtGetEntry = this.db.prepare('SELECT * FROM entries WHERE id = ?');
    this.stmtListByType = this.db.prepare(
      'SELECT * FROM entries WHERE type = ? ORDER BY created_at DESC, rowid DESC LIMIT ? OFFSET ?',
    );
    this.stmtListAll = this.db.prepare(
      'SELECT * FROM entries ORDER BY created_at DESC, rowid DESC LIMIT ? OFFSET ?',
    );
    this.stmtUpdateAnalysis = this.db.prepare(
      'UPDATE entries SET analysis = ?, updated_at = ? WHERE id = ?',
    );
    this.stmtSearchFts = this.db.prepare(
      `SELECT e.* FROM entries e
       JOIN entries_fts ON e.rowid = entries_fts.rowid
       WHERE entries_fts MATCH ?
       ORDER BY rank
       LIMIT ?`,
    );
    this.stmtDeleteEntry = this.db.prepare('DELETE FROM entries WHERE id = ?');
  }

  async saveEntry(input: CreateEntry): Promise<Entry> {
    const id = randomUUID();
    const now = new Date().toISOString();

    this.stmtInsertEntry.run({
      id,
      type: input.type,
      content: input.content,
      audio_path: input.audioPath ?? null,
      input_method: input.inputMethod,
      created_at: now,
      updated_at: now,
    });

    // Construct the Entry directly from input values instead of re-querying
    return {
      id,
      type: input.type,
      content: input.content,
      audioPath: input.audioPath,
      inputMethod: input.inputMethod,
      createdAt: now,
      updatedAt: now,
    };
  }

  async getEntry(id: string): Promise<Entry | undefined> {
    const row = this.stmtGetEntry.get(id) as EntryRow | undefined;
    return row ? rowToEntry(row) : undefined;
  }

  async listEntries(options: ListEntriesOptions): Promise<Entry[]> {
    const { type, limit, offset = 0 } = options;

    if (type !== undefined) {
      const rows = this.stmtListByType.all(type, limit, offset) as EntryRow[];
      return rows.map(rowToEntry);
    }

    const rows = this.stmtListAll.all(limit, offset) as EntryRow[];
    return rows.map(rowToEntry);
  }

  async updateEntryAnalysis(id: string, analysis: EntryAnalysis): Promise<void> {
    const now = new Date().toISOString();
    this.stmtUpdateAnalysis.run(JSON.stringify(analysis), now, id);
  }

  async updateEntryEmbedding(_id: string, _embedding: Float32Array): Promise<void> {
    // Vector storage is pending Sprint 1 research spike — throw so callers
    // can handle gracefully (e.g. runAnalysisPipeline catches and warns).
    throw new Error('Vector storage not yet implemented');
  }

  async searchByKeyword(query: string, limit: number = 20): Promise<Entry[]> {
    const rows = this.stmtSearchFts.all(query, limit) as EntryRow[];
    return rows.map(rowToEntry);
  }

  async searchByVector(_embedding: Float32Array, _limit: number): Promise<Entry[]> {
    // Vector search is pending Sprint 1 research spike — throw so callers
    // (e.g. SearchService) fall back to FTS instead of silently returning [].
    throw new Error('Vector search not yet implemented');
  }

  async deleteEntry(id: string): Promise<void> {
    this.stmtDeleteEntry.run(id);
  }

  close(): void {
    this.db.close();
  }
}
