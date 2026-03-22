import { describe, it, expect, vi, beforeEach } from 'vitest';
import type { Mock } from 'vitest';

// ---------------------------------------------------------------------------
// Mock @ten-second-tom/core before importing the module under test
// ---------------------------------------------------------------------------
vi.mock('@ten-second-tom/core', () => {
  return {
    ConfigManager: vi.fn(),
    SqliteStorageService: vi.fn(),
    SearchService: vi.fn(),
    OllamaEmbeddingService: vi.fn(),
    NoopEmbeddingService: vi.fn(),
  };
});

// Mock Ink so the Commander .action() handler doesn't blow up
vi.mock('ink', () => ({
  render: vi.fn(),
  Box: vi.fn(),
  Text: vi.fn(),
  useApp: vi.fn(() => ({ exit: vi.fn() })),
  useInput: vi.fn(),
}));

// Mock React
vi.mock('react', () => ({
  default: { createElement: vi.fn() },
  useState: vi.fn(),
  useEffect: vi.fn(),
}));

// Mock ink-text-input
vi.mock('ink-text-input', () => ({ default: vi.fn() }));

// Mock the local components
vi.mock('../../components/SearchResults.js', () => ({
  SearchResults: vi.fn(),
  SearchResultsWithDetail: vi.fn(),
}));

// Mock ink-spinner
vi.mock('ink-spinner', () => ({ default: vi.fn() }));

import {
  ConfigManager,
  SqliteStorageService,
  SearchService,
  OllamaEmbeddingService,
  NoopEmbeddingService,
} from '@ten-second-tom/core';
import type { Entry, AppConfig } from '@ten-second-tom/core';

import { runSearchPipeline } from '../search.js';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function makeEntry(overrides: Partial<Entry> = {}): Entry {
  return {
    id: '00000000-0000-0000-0000-000000000001',
    type: 'recording',
    content: 'We shipped the new dashboard today and the team is really excited.',
    inputMethod: 'recorded',
    createdAt: new Date('2026-04-01T10:00:00Z').toISOString(),
    updatedAt: new Date('2026-04-01T10:00:00Z').toISOString(),
    ...overrides,
  };
}

function makeConfig(): AppConfig {
  return {
    llm: { provider: 'cloud', apiKey: 'sk-test' },
    stt: { engine: 'whisper.node', modelPath: '/models/model.bin' },
    embedding: { provider: 'none', model: '' },
    storage: { dbPath: '/tmp/test.db' },
  } as AppConfig;
}

function mockSetup(
  isSetupComplete: boolean,
  config?: AppConfig,
): ReturnType<typeof vi.fn> {
  const mockConfigManager = {
    isSetupComplete: vi.fn().mockReturnValue(isSetupComplete),
    load: vi.fn().mockReturnValue(config ?? makeConfig()),
    audioPath: '/tmp/audio',
  };
  (ConfigManager as unknown as Mock).mockImplementation(() => mockConfigManager);
  return mockConfigManager as unknown as ReturnType<typeof vi.fn>;
}

// ---------------------------------------------------------------------------
// Tests: runSearchPipeline
// ---------------------------------------------------------------------------

describe('runSearchPipeline', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('returns an error when setup is not complete', async () => {
    mockSetup(false);

    const result = await runSearchPipeline('my query');

    expect(result.error).toContain('tom setup');
    expect(result.entries).toHaveLength(0);
  });

  it('returns an error when query is empty', async () => {
    mockSetup(true);

    const result = await runSearchPipeline('   ');

    expect(result.error).toContain('empty');
    expect(result.entries).toHaveLength(0);
  });

  it('calls SearchService.search and returns entries on success', async () => {
    mockSetup(true, makeConfig());

    const entry1 = makeEntry();
    const entry2 = makeEntry({
      id: '00000000-0000-0000-0000-000000000002',
      type: 'note',
      content: 'Need to think about the deploy pipeline more carefully.',
    });

    const mockSearch = vi.fn().mockResolvedValue([entry1, entry2]);
    const mockStorageClose = vi.fn();

    (SqliteStorageService as unknown as Mock).mockImplementation(() => ({
      close: mockStorageClose,
    }));

    (NoopEmbeddingService as unknown as Mock).mockImplementation(() => ({}));

    (SearchService as unknown as Mock).mockImplementation(() => ({
      search: mockSearch,
    }));

    const result = await runSearchPipeline('dashboard');

    expect(mockSearch).toHaveBeenCalledWith('dashboard');
    expect(result.error).toBeNull();
    expect(result.entries).toHaveLength(2);
    expect(result.entries[0]).toEqual(entry1);
    expect(result.entries[1]).toEqual(entry2);
  });

  it('returns empty entries when search yields no results', async () => {
    mockSetup(true, makeConfig());

    (SqliteStorageService as unknown as Mock).mockImplementation(() => ({
      close: vi.fn(),
    }));

    (NoopEmbeddingService as unknown as Mock).mockImplementation(() => ({}));

    (SearchService as unknown as Mock).mockImplementation(() => ({
      search: vi.fn().mockResolvedValue([]),
    }));

    const result = await runSearchPipeline('something obscure');

    expect(result.error).toBeNull();
    expect(result.entries).toHaveLength(0);
  });

  it('uses OllamaEmbeddingService when embedding provider is ollama', async () => {
    const config: AppConfig = {
      ...makeConfig(),
      embedding: { provider: 'ollama', model: 'nomic-embed-text', endpoint: 'http://localhost:11434' },
    } as AppConfig;

    mockSetup(true, config);

    (SqliteStorageService as unknown as Mock).mockImplementation(() => ({
      close: vi.fn(),
    }));

    const mockOllamaInstance = {};
    (OllamaEmbeddingService as unknown as Mock).mockImplementation(() => mockOllamaInstance);

    let capturedEmbeddingInstance: unknown;
    (SearchService as unknown as Mock).mockImplementation((_storage: unknown, embedding: unknown) => {
      capturedEmbeddingInstance = embedding;
      return { search: vi.fn().mockResolvedValue([]) };
    });

    await runSearchPipeline('test query');

    expect(OllamaEmbeddingService).toHaveBeenCalledWith({
      model: 'nomic-embed-text',
      endpoint: 'http://localhost:11434',
    });
    expect(capturedEmbeddingInstance).toBe(mockOllamaInstance);
  });

  it('falls back to NoopEmbeddingService when embedding provider is none', async () => {
    const config: AppConfig = {
      ...makeConfig(),
      embedding: { provider: 'none', model: '' },
    } as AppConfig;

    mockSetup(true, config);

    (SqliteStorageService as unknown as Mock).mockImplementation(() => ({
      close: vi.fn(),
    }));

    const mockNoopInstance = {};
    (NoopEmbeddingService as unknown as Mock).mockImplementation(() => mockNoopInstance);

    let capturedEmbeddingInstance: unknown;
    (SearchService as unknown as Mock).mockImplementation((_storage: unknown, embedding: unknown) => {
      capturedEmbeddingInstance = embedding;
      return { search: vi.fn().mockResolvedValue([]) };
    });

    await runSearchPipeline('test query');

    expect(NoopEmbeddingService).toHaveBeenCalled();
    expect(capturedEmbeddingInstance).toBe(mockNoopInstance);
  });

  it('closes storage even when search throws', async () => {
    mockSetup(true, makeConfig());

    const mockClose = vi.fn();
    (SqliteStorageService as unknown as Mock).mockImplementation(() => ({
      close: mockClose,
    }));

    (NoopEmbeddingService as unknown as Mock).mockImplementation(() => ({}));

    (SearchService as unknown as Mock).mockImplementation(() => ({
      search: vi.fn().mockRejectedValue(new Error('DB error')),
    }));

    const result = await runSearchPipeline('query');

    expect(mockClose).toHaveBeenCalled();
    expect(result.error).toContain('DB error');
  });
});
