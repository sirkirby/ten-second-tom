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
