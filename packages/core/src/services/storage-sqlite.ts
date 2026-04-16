import Database from 'better-sqlite3';
import * as sqliteVec from 'sqlite-vec';
import { randomUUID } from 'node:crypto';
import type { Entry, CreateEntry, EntryAnalysis } from '../types/entry.js';
import type { IStorageService, ListEntriesOptions } from './storage.js';
import { DEFAULT_EMBEDDING_DIMENSION } from '../constants.js';

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

/**
 * Build the vec0 DDL for the given embedding dimension. The dimension must
 * match the vectors that will be inserted — a mismatch causes sqlite-vec to
 * throw at INSERT time.
 */
function vectorMigrationSql(dimension: number): string {
  return `
CREATE VIRTUAL TABLE IF NOT EXISTS entry_embeddings USING vec0(
  entry_id TEXT PRIMARY KEY,
  embedding float[${dimension}]
);
`;
}

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
  private readonly stmtDeleteEmbedding: Database.Statement;
  private readonly stmtInsertEmbedding: Database.Statement;
  private readonly stmtCountEntries: Database.Statement;
  private readonly stmtSearchVector: Database.Statement;
  private readonly txnUpsertEmbedding: (id: string, buffer: Buffer) => void;

  constructor(dbPath: string, embeddingDimension: number = DEFAULT_EMBEDDING_DIMENSION) {
    this.db = new Database(dbPath);
    this.db.pragma('journal_mode = WAL');
    this.db.pragma('foreign_keys = ON');

    // Load sqlite-vec extension before running migrations so vec0 is available
    sqliteVec.load(this.db);

    this.db.exec(MIGRATION_SQL);

    // Ensure the vec0 table matches the requested embedding dimension.
    // If the table already exists with a different dimension (e.g. the user
    // switched embedding models), drop and recreate it.  This loses existing
    // embeddings, but they can be regenerated via `tom analyze --all`.
    this.ensureVectorTable(embeddingDimension);

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
    this.stmtCountEntries = this.db.prepare('SELECT COUNT(*) as count FROM entries');

    // Vector embedding statements — vec0 does not support INSERT OR REPLACE,
    // so we use DELETE + INSERT (two steps, both cheap).
    this.stmtDeleteEmbedding = this.db.prepare('DELETE FROM entry_embeddings WHERE entry_id = ?');
    this.stmtInsertEmbedding = this.db.prepare(
      'INSERT INTO entry_embeddings (entry_id, embedding) VALUES (?, ?)',
    );
    this.stmtSearchVector = this.db.prepare(
      `SELECT e.*
       FROM entry_embeddings ev
       JOIN entries e ON e.id = ev.entry_id
       WHERE ev.embedding MATCH ?
       AND k = ?
       ORDER BY distance`,
    );

    // Pre-prepare the embedding upsert transaction (DELETE + INSERT)
    this.txnUpsertEmbedding = this.db.transaction((id: string, buffer: Buffer) => {
      this.stmtDeleteEmbedding.run(id);
      this.stmtInsertEmbedding.run(id, buffer);
    });
  }

  /**
   * Ensure the entry_embeddings vec0 table exists with the correct dimension.
   * If a table already exists with a different dimension, drop and recreate it.
   */
  private ensureVectorTable(dimension: number): void {
    // Check if the table already exists by querying sqlite_master
    const exists = this.db
      .prepare("SELECT sql FROM sqlite_master WHERE type='table' AND name='entry_embeddings'")
      .get() as { sql: string } | undefined;

    if (exists) {
      // Extract the dimension from the existing DDL, e.g. "float[768]"
      const match = exists.sql.match(/float\[(\d+)\]/);
      const existingDim = match?.[1] ? parseInt(match[1], 10) : null;

      if (existingDim !== null && existingDim !== dimension) {
        // Dimension mismatch — drop and recreate. Existing embeddings are lost
        // but can be regenerated.
        this.db.exec('DROP TABLE IF EXISTS entry_embeddings');
        this.db.exec(vectorMigrationSql(dimension));
        return;
      }
    }

    // Table either doesn't exist or matches — create if needed (idempotent).
    this.db.exec(vectorMigrationSql(dimension));
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

  async countEntries(): Promise<number> {
    const row = this.stmtCountEntries.get() as { count: number };
    return row.count;
  }

  async updateEntryAnalysis(id: string, analysis: EntryAnalysis): Promise<void> {
    const now = new Date().toISOString();
    this.stmtUpdateAnalysis.run(JSON.stringify(analysis), now, id);
  }

  async updateEntryEmbedding(id: string, embedding: Float32Array): Promise<void> {
    // sqlite-vec expects the raw float bytes as a Buffer/Uint8Array
    const buffer = Buffer.from(embedding.buffer, embedding.byteOffset, embedding.byteLength);

    // vec0 does not support INSERT OR REPLACE — use pre-prepared DELETE + INSERT transaction
    this.txnUpsertEmbedding(id, buffer);
  }

  async searchByKeyword(query: string, limit: number = 20): Promise<Entry[]> {
    const rows = this.stmtSearchFts.all(query, limit) as EntryRow[];
    return rows.map(rowToEntry);
  }

  async searchByVector(embedding: Float32Array, limit: number): Promise<Entry[]> {
    const buffer = Buffer.from(embedding.buffer, embedding.byteOffset, embedding.byteLength);
    const rows = this.stmtSearchVector.all(buffer, limit) as EntryRow[];
    return rows.map(rowToEntry);
  }

  async deleteEntry(id: string): Promise<void> {
    this.stmtDeleteEntry.run(id);
  }

  close(): void {
    this.db.close();
  }
}
