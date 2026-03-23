import { describe, it, expect, vi, beforeEach } from 'vitest';
import type { Mock } from 'vitest';
import type * as RecordModule from '../record.js';

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
    AudioService: vi.fn(),
    WhisperTranscriptionService: vi.fn(),
    TomAgent: vi.fn(),
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
  useRef: vi.fn(() => ({ current: null })),
}));

// Mock ink-text-input
vi.mock('ink-text-input', () => ({ default: vi.fn() }));

// Mock the local components
vi.mock('../../components/SearchResults.js', () => ({
  SearchResults: vi.fn(),
  SearchResultsWithDetail: vi.fn(),
}));
vi.mock('../../components/RecordingUI.js', () => ({ RecordingUI: vi.fn() }));
vi.mock('../../components/SentimentDisplay.js', () => ({ SentimentDisplay: vi.fn() }));
vi.mock('../../components/ErrorDisplay.js', () => ({ ErrorDisplay: vi.fn() }));
vi.mock('../../hooks/useAutoExit.js', () => ({ useAutoExit: vi.fn() }));
vi.mock('../../hooks/useSetupGuard.js', () => ({
  checkSetupComplete: vi.fn(() => ({ ok: true, config: {}, configManager: {} })),
}));

// Mock the record module so we can spy on buildServicesFromConfig
vi.mock('../record.js', async (importOriginal) => {
  const actual = await importOriginal<typeof RecordModule>();
  return {
    ...actual,
    buildServicesFromConfig: vi.fn(),
  };
});

// Mock ink-spinner
vi.mock('ink-spinner', () => ({ default: vi.fn() }));

import { SearchService } from '@ten-second-tom/core';
import type { Entry, AppConfig } from '@ten-second-tom/core';

import { buildServicesFromConfig } from '../record.js';
import { runSearchPipeline } from '../search.js';
import { checkSetupComplete } from '../../hooks/useSetupGuard.js';

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

function mockSetup(isSetupComplete: boolean, config?: AppConfig): void {
  if (isSetupComplete) {
    const mockConfigManager = { audioPath: '/tmp/audio' };
    (checkSetupComplete as unknown as Mock).mockReturnValue({
      ok: true,
      config: config ?? makeConfig(),
      configManager: mockConfigManager,
    });
  } else {
    (checkSetupComplete as unknown as Mock).mockReturnValue({
      ok: false,
      error: 'Tom is not configured. Run `tom setup` first.',
    });
  }
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
    const mockStorage = { close: mockStorageClose };
    const mockEmbedding = {};

    (buildServicesFromConfig as unknown as Mock).mockReturnValue({
      storage: mockStorage,
      embedding: mockEmbedding,
      audio: {},
      transcription: {},
      agent: {},
    });

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

    (buildServicesFromConfig as unknown as Mock).mockReturnValue({
      storage: { close: vi.fn() },
      embedding: {},
      audio: {},
      transcription: {},
      agent: {},
    });

    (SearchService as unknown as Mock).mockImplementation(() => ({
      search: vi.fn().mockResolvedValue([]),
    }));

    const result = await runSearchPipeline('something obscure');

    expect(result.error).toBeNull();
    expect(result.entries).toHaveLength(0);
  });

  it('uses embedding from buildServicesFromConfig', async () => {
    const config: AppConfig = {
      ...makeConfig(),
      embedding: {
        provider: 'ollama',
        model: 'nomic-embed-text',
        endpoint: 'http://localhost:11434',
      },
    } as AppConfig;

    mockSetup(true, config);

    const mockEmbeddingInstance = { embed: vi.fn() };
    const mockStorageInstance = { close: vi.fn() };

    (buildServicesFromConfig as unknown as Mock).mockReturnValue({
      storage: mockStorageInstance,
      embedding: mockEmbeddingInstance,
      audio: {},
      transcription: {},
      agent: {},
    });

    let capturedEmbeddingInstance: unknown;
    (SearchService as unknown as Mock).mockImplementation(
      (_storage: unknown, embedding: unknown) => {
        capturedEmbeddingInstance = embedding;
        return { search: vi.fn().mockResolvedValue([]) };
      },
    );

    await runSearchPipeline('test query');

    expect(capturedEmbeddingInstance).toBe(mockEmbeddingInstance);
  });

  it('closes storage even when search throws', async () => {
    mockSetup(true, makeConfig());

    const mockClose = vi.fn();
    (buildServicesFromConfig as unknown as Mock).mockReturnValue({
      storage: { close: mockClose },
      embedding: {},
      audio: {},
      transcription: {},
      agent: {},
    });

    (SearchService as unknown as Mock).mockImplementation(() => ({
      search: vi.fn().mockRejectedValue(new Error('DB error')),
    }));

    const result = await runSearchPipeline('query');

    expect(mockClose).toHaveBeenCalled();
    expect(result.error).toContain('DB error');
  });
});
