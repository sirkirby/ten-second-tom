# Database Evaluation: SQLite + sqlite-vec vs PGlite

**Date:** 2026-03-22
**Author:** Research spike for Ten-Second Tom v2
**Status:** Complete — recommendation included

---

## Context

Ten-Second Tom v2 needs a local-only embedded database supporting:

1. Basic CRUD on an `entries` table
2. Full-text search (FTS) across entry content
3. Vector storage and cosine/L2 similarity search (768 or 1024 dimensions)
4. No server — embedded in-process
5. Node.js with clean TypeScript API
6. Migration tooling for schema evolution
7. Cross-platform: macOS and Windows

The existing implementation uses **better-sqlite3** with FTS5 for CRUD and keyword search. The `updateEntryEmbedding` and `searchByVector` methods on `IStorageService` are currently stubs pending this evaluation.

---

## Option A: SQLite + sqlite-vec

**Stack:** `better-sqlite3` (already in use) + `sqlite-vec` extension

### What it is

sqlite-vec is a pure-C SQLite extension that adds a `vec0` virtual table for storing and querying float32 (or binary-quantized) vectors. It ships as a pre-built native binary for each platform and loads at runtime via `db.loadExtension()`. The `sqlite-vec` npm package wraps the platform-specific binaries and exposes a single `load(db)` call.

### Vector search quality and performance

- Uses **brute-force KNN search only** as of v0.1.7-alpha.2 (January 2025). There is no HNSW or IVFFlat index yet; ANN support (DiskANN) is tracked in the roadmap but not yet released.
- For the scale Ten-Second Tom targets (hundreds to low thousands of entries), brute-force is entirely appropriate. Benchmarks show query times under 75ms for 768- and 1024-dimensional vectors at up to ~100k rows on disk.
- SIMD acceleration is included; the extension selects AVX2, NEON, or scalar paths automatically at runtime.
- Supports cosine distance, L2 distance, and inner product.
- Binary quantization is available for significantly reduced storage size at the cost of some recall.

### Node.js integration

The `sqlite-vec` npm package's `load()` function is directly compatible with better-sqlite3:

```typescript
import * as sqliteVec from 'sqlite-vec';
import Database from 'better-sqlite3';

const db = new Database(dbPath);
sqliteVec.load(db);  // loads the platform-native .dylib/.dll/.so

// Create vector table linked to entries via rowid
db.prepare(`
  CREATE VIRTUAL TABLE IF NOT EXISTS entries_vec
  USING vec0(embedding float[768])
`).run();

// Insert embedding (Float32Array as buffer)
db.prepare(
  'INSERT INTO entries_vec(rowid, embedding) VALUES (?, ?)'
).run(rowid, new Float32Array(embedding).buffer);

// Cosine KNN query, joining back to entries
const rows = db.prepare(`
  SELECT e.*, ev.distance
  FROM entries_vec ev
  JOIN entries e ON e.rowid = ev.rowid
  WHERE ev.embedding MATCH ?
    AND k = ?
  ORDER BY ev.distance
`).all(new Float32Array(query).buffer, limit);
```

The `sqlite-vec` npm package ships platform-specific binaries (`darwin-arm64`, `darwin-x64`, `win32-x64`, `linux-x64`) as optional dependencies. Installation is a single `pnpm add sqlite-vec` with no build step required.

### Bundle/install size

- `better-sqlite3`: ~10 MB (includes native Node.js addon, pre-built binaries via node-pre-gyp)
- `sqlite-vec` native binary: ~500 KB–1 MB per platform (shipped as optional peer packages, only the matching platform is downloaded)
- No WASM overhead; pure native code

### Migration tooling

SQLite migrations work with the project's existing hand-rolled SQL runner (`migrations/local/`) or with **Drizzle Kit**, which has first-class SQLite support. The `vec0` virtual table is plain DDL and migrates the same way as any other table.

### Maturity and community

- sqlite-vec is authored by Alex Garcia (previously of Datasette/Observable), who also wrote sqlite-vss (the predecessor based on FAISS). The library has strong community interest.
- **Current status:** v0.1.7-alpha.2 (January 2025). The `alpha` label reflects that the ANN feature is still in development; the brute-force core was declared stable in v0.1.0 (August 2024).
- **Known maintenance concern:** There was a ~6-month gap in activity in 2024. The author confirmed in early 2025 that development has resumed with an active DiskANN branch in progress.
- better-sqlite3 is the de facto standard SQLite binding for Node.js: ~10M weekly downloads, actively maintained.

### Known limitations

- Brute-force only — scales to ~1M vectors but slows noticeably beyond that (not a concern for this app)
- No ANN index yet; large-scale recall/performance trade-offs cannot be tuned until DiskANN lands
- `better-sqlite3` is synchronous; cannot be used safely from multiple threads simultaneously (not a constraint for a CLI app)
- The `alpha` npm version tag may cause hesitation in dependency audits

---

## Option B: PGlite

**Stack:** `@electric-sql/pglite` — PostgreSQL compiled to WASM, running in-process

### What it is

PGlite is PostgreSQL 16 compiled to WebAssembly via Emscripten. It runs entirely in-process in Node.js, Deno, Bun, and the browser with no external server or native addon. The `pgvector` extension is bundled and loadable at startup.

### Vector search quality and performance

- Uses **pgvector** (the same library powering Supabase, Neon, and Tembo). pgvector supports both **HNSW** and **IVFFlat** ANN indexes as well as exact KNN search.
- HNSW with `vector_cosine_ops` provides excellent recall/speed trade-offs and can be created without pre-existing data.
- pgvector supports up to 16,000 dimensions by default; half-precision vectors extend indexed search to 4,000 dimensions.
- Performance is bounded by WASM overhead. PGlite is measurably slower than native SQLite for equivalent workloads. Initialization (cold start) takes 500ms–2s. Query throughput is lower than a native binary.

### Node.js integration

The API is fully async (Promise-based) with built-in TypeScript types:

```typescript
import { PGlite } from '@electric-sql/pglite';
import { vector } from '@electric-sql/pglite/vector';

const db = await PGlite.create({
  dataDir: dbPath,
  extensions: { vector },
});

await db.query('CREATE EXTENSION IF NOT EXISTS vector;');
await db.query(`
  CREATE TABLE IF NOT EXISTS entry_embeddings (
    id TEXT PRIMARY KEY,
    embedding vector(768)
  )
`);
await db.query(`
  CREATE INDEX IF NOT EXISTS entry_embeddings_hnsw
  ON entry_embeddings
  USING hnsw (embedding vector_cosine_ops)
`);

// Insert — pgvector accepts JSON array string
await db.query(
  'INSERT INTO entry_embeddings (id, embedding) VALUES ($1, $2)',
  [id, JSON.stringify(Array.from(embedding))]
);

// Cosine similarity query
const result = await db.query(
  `SELECT id, embedding <=> $1 AS distance
   FROM entry_embeddings
   ORDER BY distance
   LIMIT $2`,
  [JSON.stringify(Array.from(query)), limit]
);
```

### Bundle/install size

- `@electric-sql/pglite`: ~3 MB gzipped (~15–20 MB uncompressed WASM binary on disk)
- No native addon; pure WASM — no node-gyp, no platform-specific binaries
- The large uncompressed size can complicate tooling; some bundlers require special configuration to handle the WASM binary

### Migration tooling

PGlite has **first-class Drizzle ORM support** via a dedicated adapter (`drizzle-orm/pglite`). Drizzle Kit can target PGlite directly with `driver: "pglite"` in `drizzle.config.ts`, generating and running standard Postgres migrations. Knex is also documented as compatible.

This is the strongest point in PGlite's favor if the project intends to adopt a full ORM.

### Maturity and community

- PGlite is developed by ElectricSQL, a well-funded team building local-first sync infrastructure. The project is actively maintained with releases approximately every 1–2 weeks.
- **Current status:** v0.2.x series (0.2.3x range as of March 2026). Pre-1.0; the API had breaking changes between 0.1 and 0.2.
- The Hacker News launch (August 2024) generated significant interest; the GitHub repo has thousands of stars.
- Real production usage is primarily in **browser-based** local-first apps. Node.js production usage is less documented; most examples are for prototyping or browser deployments.

### Known limitations

- **500ms–2s cold start** per process launch — noticeable for a CLI that starts and exits quickly
- Single-connection only — not a problem for a CLI, but worth noting
- WASM overhead makes it slower than native SQLite for all operations, not just vectors
- Pre-1.0 with documented breaking changes between minor versions
- Bundler configuration complexity when packaging for distribution
- FTS must use Postgres `tsvector` / `GIN` indexes rather than SQLite FTS5 — requires rewriting existing keyword search queries

---

## Comparison Table

| Criterion | SQLite + sqlite-vec | PGlite |
|---|---|---|
| **Vector search** | Brute-force KNN (ANN roadmap) | HNSW + IVFFlat via pgvector |
| **Vector index type** | None (full scan) | HNSW, IVFFlat, or exact |
| **Max dimensions** | No hard limit (float32 arrays) | 16,000 (default); 4,000 indexed |
| **Cosine similarity** | Yes (`vec_distance_cosine`) | Yes (`<=>` operator) |
| **FTS** | FTS5 (already working) | `tsvector` + GIN (rewrite needed) |
| **Query API** | Synchronous (better-sqlite3) | Async (Promise-based) |
| **Cold start time** | < 10ms | 500ms–2s (WASM init) |
| **Install size** | ~10 MB (better-sqlite3 + vec binary) | ~15–20 MB uncompressed WASM |
| **Native addon** | Yes (pre-built, no build step) | No (pure WASM) |
| **Cross-platform** | macOS arm64/x64, Windows x64 | macOS, Windows, Linux (WASM) |
| **Migration tooling** | Hand-rolled SQL or Drizzle Kit | Drizzle ORM (first-class), Knex |
| **TypeScript types** | `@types/better-sqlite3` (maintained) | Built-in |
| **Stability** | v0.1.7-alpha.2 (brute-force core stable) | v0.2.x (pre-1.0, active) |
| **Production usage** | Mature (better-sqlite3 is the standard) | Primarily browser / prototype |
| **Existing codebase fit** | Drop-in — already using better-sqlite3 | Full rewrite of SqliteStorageService |

---

## Recommendation: Option A — SQLite + sqlite-vec

### Rationale

**1. The existing implementation is already 80% done.**
`SqliteStorageService` is working, FTS5 is wired up, migrations run, and the codebase uses better-sqlite3 throughout. Adding sqlite-vec means implementing the two stub methods (`updateEntryEmbedding`, `searchByVector`) — no architecture change, no new migration format, no new ORM.

**2. Cold start latency disqualifies PGlite for a CLI.**
Ten-Second Tom is a CLI tool that starts, runs a command, and exits. A 500ms–2s WASM initialization on every invocation would be a jarring UX regression. SQLite with better-sqlite3 opens in under 10ms.

**3. Brute-force is sufficient at the expected scale.**
The app stores personal journal entries — hundreds to a few thousand rows at most over years of use. sqlite-vec's brute-force KNN with SIMD acceleration returns results in under 75ms for 768/1024-dimensional vectors at up to 100k rows. There is no scenario where ANN indexing is needed here.

**4. No FTS rewrite required.**
The FTS5 implementation is already tested and working. Switching to PGlite would require rewriting keyword search using Postgres `tsvector` and `GIN` indexes — added complexity for zero benefit.

**5. Better operational simplicity.**
A single `.db` file managed by better-sqlite3 is easy to back up, inspect with any SQLite viewer, and migrate. PGlite writes its own internal Postgres data directory structure which is opaque to standard tooling.

**6. Cross-platform works out of the box.**
sqlite-vec ships pre-built binaries for `darwin-arm64`, `darwin-x64`, and `win32-x64` as optional npm dependencies. No build step beyond what better-sqlite3 already requires.

### Implementation steps

1. Add the dependency:

   ```
   pnpm add sqlite-vec --filter @ten-second-tom/core
   ```

2. In the `SqliteStorageService` constructor, call `sqliteVec.load(this.db)` after the existing pragma setup.

3. Add a `entries_vec` virtual table to the migration SQL (either inline in the service or as a new migration file `002_embeddings.sql`):

   ```sql
   CREATE VIRTUAL TABLE IF NOT EXISTS entries_vec
   USING vec0(embedding float[768])
   ```

   The embedding dimension should be a constructor parameter to support both 768 (`nomic-embed-text`) and 1024 (alternative models).

4. Implement `updateEntryEmbedding`: upsert the rowid + embedding buffer into `entries_vec`.

5. Implement `searchByVector`: KNN MATCH query on `entries_vec`, join to `entries` on rowid, return typed `Entry[]`.

### When to reconsider PGlite

Revisit PGlite if:
- The app evolves into a long-running background daemon (cold start is no longer per-invocation)
- Vector recall quality at scale becomes critical and HNSW indexing is needed
- The project adopts Drizzle ORM comprehensively and wants a unified Postgres-flavored SQL layer
- PGlite reaches 1.0 with stable Node.js production usage documented

---

## Migration Path (if we later switch to PGlite)

Because `IStorageService` provides a clean interface boundary, switching storage backends is mechanical:

1. Create `PgliteStorageService implements IStorageService`
2. Write a one-time data export/import script: open the SQLite db, read all entries with embeddings, insert into PGlite
3. Update the DI wiring in the CLI package to instantiate `PgliteStorageService`
4. Remove better-sqlite3 and sqlite-vec from dependencies

No application-layer code outside of `storage-sqlite.ts` would need to change.

---

## Sources consulted

- sqlite-vec documentation and Node.js integration guide: https://alexgarcia.xyz/sqlite-vec/js.html
- sqlite-vec v0.1.0 stable release announcement: https://alexgarcia.xyz/blog/2024/sqlite-vec-stable-release/index.html
- sqlite-vec GitHub (releases, ANN tracking issue): https://github.com/asg017/sqlite-vec
- sqlite-vec ANN (Approximate Nearest Neighbors) tracking issue: https://github.com/asg017/sqlite-vec/issues/25
- better-sqlite3 GitHub: https://github.com/WiseLibs/better-sqlite3
- PGlite official docs and getting started: https://pglite.dev/docs/
- PGlite GitHub: https://github.com/electric-sql/pglite
- PGlite extensions (pgvector): https://pglite.dev/extensions/
- PGlite ORM support (Drizzle, Knex): https://pglite.dev/docs/orm-support
- PGlite benchmarks: https://pglite.dev/benchmarks
- Drizzle ORM + PGlite integration: https://orm.drizzle.team/docs/get-started/pglite-new
- pgvector GitHub: https://github.com/pgvector/pgvector
- sqlite-vec usage in Node.js (DEV article): https://dev.to/stephenc222/how-to-use-sqlite-vec-to-store-and-query-vector-embeddings-58mf
- Embedded database comparison for Node.js: https://codenote.net/en/posts/vercel-nextjs-embedded-database-prototyping/
- Vector search in 100 lines with PGlite + pgvector: https://zenn.dev/mizchi/articles/pglite-vector-search
- sqlite-vec Medium article (brute-force mechanics): https://medium.com/@stephenc211/how-sqlite-vec-works-for-storing-and-querying-vector-embeddings-165adeeeceea
