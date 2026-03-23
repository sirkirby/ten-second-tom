import { describe, it, expect, vi, beforeEach } from 'vitest';
import type { Mock } from 'vitest';
import type * as RecordModule from '../record.js';

// ---------------------------------------------------------------------------
// We test the exported pipeline helper (runAnalyzePipeline) directly.
// ---------------------------------------------------------------------------

// Mock @ten-second-tom/core before importing the module under test
vi.mock('@ten-second-tom/core', () => {
  return {
    ConfigManager: vi.fn(),
    AudioService: vi.fn(),
    WhisperTranscriptionService: vi.fn(),
    TomAgent: vi.fn(),
    OllamaEmbeddingService: vi.fn(),
    NoopEmbeddingService: vi.fn(),
    SqliteStorageService: vi.fn(),
  };
});

// Mock Ink's render so the Commander .action() handler doesn't blow up
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

// Mock the local UI components so their imports don't explode
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

import type {
  IEmbeddingService,
  IAgentService,
  IStorageService,
  EntryAnalysis,
  AppConfig,
  Entry,
} from '@ten-second-tom/core';

import { buildServicesFromConfig } from '../record.js';
import type { RecordingPipelineServices } from '../record.js';
import { runAnalyzePipeline } from '../analyze.js';
import { checkSetupComplete } from '../../hooks/useSetupGuard.js';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function makeAnalysis(): EntryAnalysis {
  return {
    sentiment: { score: 0.5, label: 'positive', confidence: 0.9 },
    summary: 'A positive entry',
    raw: {},
  };
}

function makeEntry(overrides: Partial<Entry> = {}): Entry {
  return {
    id: 'entry-uuid-1234',
    type: 'recording',
    content: 'hello world',
    inputMethod: 'recorded',
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
    ...overrides,
  };
}

function makeMockServices(
  overrides: Partial<RecordingPipelineServices> = {},
): RecordingPipelineServices {
  const mockEntry = makeEntry();

  const storage: IStorageService = {
    saveEntry: vi.fn().mockResolvedValue(mockEntry),
    getEntry: vi.fn().mockResolvedValue(mockEntry),
    listEntries: vi.fn().mockResolvedValue([]),
    updateEntryAnalysis: vi.fn().mockResolvedValue(undefined),
    updateEntryEmbedding: vi.fn().mockResolvedValue(undefined),
    searchByKeyword: vi.fn().mockResolvedValue([]),
    searchByVector: vi.fn().mockResolvedValue([]),
    deleteEntry: vi.fn().mockResolvedValue(undefined),
    close: vi.fn(),
  };

  const agent = {
    analyze: vi.fn().mockResolvedValue(makeAnalysis()),
  } as IAgentService;

  const embedding: IEmbeddingService = {
    embed: vi.fn().mockResolvedValue(new Float32Array([0.1, 0.2, 0.3])),
    isAvailable: vi.fn().mockResolvedValue(true),
  };

  const audio = {
    startRecording: vi.fn(),
    stopRecording: vi.fn(),
    getAudioStream: vi.fn(),
    isRecording: vi.fn().mockReturnValue(false),
  };

  const transcription = {
    transcribeStream: vi.fn(),
    transcribeFile: vi.fn(),
    isModelLoaded: vi.fn().mockReturnValue(true),
    loadModel: vi.fn(),
  };

  return {
    audio,
    transcription,
    agent,
    embedding,
    storage,
    ...overrides,
  } as RecordingPipelineServices;
}

function mockSetupGuard(isComplete = true): void {
  if (isComplete) {
    const mockConfigManager = { audioPath: '/tmp/audio' };
    (checkSetupComplete as unknown as Mock).mockReturnValue({
      ok: true,
      config: {
        llm: { provider: 'cloud', apiKey: 'sk-test' },
        stt: { engine: 'whisper.node', modelPath: '/models/model.bin' },
        embedding: { provider: 'none', model: '' },
        storage: { dbPath: '/tmp/test.db' },
      } as AppConfig,
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
// Tests: runAnalyzePipeline — setup guard
// ---------------------------------------------------------------------------

describe('runAnalyzePipeline — setup guard', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('returns an error result when setup is not complete', async () => {
    mockSetupGuard(false);

    const result = await runAnalyzePipeline('some-entry-id');

    expect(result.error).toContain('tom setup');
    expect(result.analysis).toBeNull();
  });
});

// ---------------------------------------------------------------------------
// Tests: runAnalyzePipeline — entry lookup
// ---------------------------------------------------------------------------

describe('runAnalyzePipeline — entry lookup', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('returns an error result when the entry is not found', async () => {
    mockSetupGuard(true);

    const mockServices = makeMockServices({
      storage: {
        saveEntry: vi.fn(),
        getEntry: vi.fn().mockResolvedValue(undefined),
        listEntries: vi.fn(),
        updateEntryAnalysis: vi.fn(),
        updateEntryEmbedding: vi.fn(),
        searchByKeyword: vi.fn(),
        searchByVector: vi.fn(),
        deleteEntry: vi.fn(),
        close: vi.fn(),
      } as IStorageService,
    });
    (buildServicesFromConfig as unknown as Mock).mockReturnValue(mockServices);

    const result = await runAnalyzePipeline('nonexistent-id');

    expect(result.error).toContain('Entry not found');
    expect(result.analysis).toBeNull();
  });
});

// ---------------------------------------------------------------------------
// Tests: runAnalyzePipeline — successful re-analysis
// ---------------------------------------------------------------------------

describe('runAnalyzePipeline — successful re-analysis', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('loads the entry, runs TomAgent.analyze, updates analysis, and returns result', async () => {
    mockSetupGuard(true);

    const entry = makeEntry({ id: 'entry-uuid-1234', content: 'hello world' });
    const mockServices = makeMockServices({
      storage: {
        saveEntry: vi.fn(),
        getEntry: vi.fn().mockResolvedValue(entry),
        listEntries: vi.fn(),
        updateEntryAnalysis: vi.fn().mockResolvedValue(undefined),
        updateEntryEmbedding: vi.fn().mockResolvedValue(undefined),
        searchByKeyword: vi.fn(),
        searchByVector: vi.fn(),
        deleteEntry: vi.fn(),
        close: vi.fn(),
      } as IStorageService,
    });
    (buildServicesFromConfig as unknown as Mock).mockReturnValue(mockServices);

    const result = await runAnalyzePipeline('entry-uuid-1234');

    // Entry was loaded
    expect(mockServices.storage.getEntry).toHaveBeenCalledWith('entry-uuid-1234');

    // Analysis was run with entry content
    expect(mockServices.agent.analyze).toHaveBeenCalledWith('hello world');

    // Analysis was stored
    expect(mockServices.storage.updateEntryAnalysis).toHaveBeenCalledWith(
      'entry-uuid-1234',
      makeAnalysis(),
    );

    expect(result.error).toBeNull();
    expect(result.analysis).toEqual(makeAnalysis());
    expect(result.entryId).toBe('entry-uuid-1234');
  });

  it('also runs embedding and updates it when embedding service returns a result', async () => {
    mockSetupGuard(true);

    const entry = makeEntry({ id: 'entry-uuid-1234', content: 'hello world' });
    const mockServices = makeMockServices({
      storage: {
        saveEntry: vi.fn(),
        getEntry: vi.fn().mockResolvedValue(entry),
        listEntries: vi.fn(),
        updateEntryAnalysis: vi.fn().mockResolvedValue(undefined),
        updateEntryEmbedding: vi.fn().mockResolvedValue(undefined),
        searchByKeyword: vi.fn(),
        searchByVector: vi.fn(),
        deleteEntry: vi.fn(),
        close: vi.fn(),
      } as IStorageService,
    });
    (buildServicesFromConfig as unknown as Mock).mockReturnValue(mockServices);

    const result = await runAnalyzePipeline('entry-uuid-1234');

    expect(mockServices.embedding.embed).toHaveBeenCalledWith('hello world');
    expect(mockServices.storage.updateEntryEmbedding).toHaveBeenCalledWith(
      'entry-uuid-1234',
      expect.any(Float32Array),
    );
    expect(result.embeddingStored).toBe(true);
  });

  it('overwrites an entry that already has analysis (re-analyze means re-analyze)', async () => {
    mockSetupGuard(true);

    const existingAnalysis: EntryAnalysis = {
      sentiment: { score: -0.5, label: 'negative', confidence: 0.8 },
      summary: 'Old summary',
      raw: { old: true },
    };
    const entry = makeEntry({
      id: 'entry-uuid-1234',
      content: 'hello world',
      analysis: existingAnalysis,
    });
    const mockServices = makeMockServices({
      storage: {
        saveEntry: vi.fn(),
        getEntry: vi.fn().mockResolvedValue(entry),
        listEntries: vi.fn(),
        updateEntryAnalysis: vi.fn().mockResolvedValue(undefined),
        updateEntryEmbedding: vi.fn().mockResolvedValue(undefined),
        searchByKeyword: vi.fn(),
        searchByVector: vi.fn(),
        deleteEntry: vi.fn(),
        close: vi.fn(),
      } as IStorageService,
    });
    (buildServicesFromConfig as unknown as Mock).mockReturnValue(mockServices);

    const result = await runAnalyzePipeline('entry-uuid-1234');

    // New analysis replaces old
    expect(mockServices.storage.updateEntryAnalysis).toHaveBeenCalledWith(
      'entry-uuid-1234',
      makeAnalysis(),
    );
    expect(result.analysis).toEqual(makeAnalysis());
    expect(result.error).toBeNull();
  });
});

// ---------------------------------------------------------------------------
// Tests: runAnalyzePipeline — LLM unavailable
// ---------------------------------------------------------------------------

describe('runAnalyzePipeline — LLM unavailable', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('returns an error when TomAgent.analyze rejects (explicit re-analysis must not degrade silently)', async () => {
    mockSetupGuard(true);

    const entry = makeEntry({ id: 'entry-uuid-1234', content: 'hello world' });
    const mockServices = makeMockServices({
      storage: {
        saveEntry: vi.fn(),
        getEntry: vi.fn().mockResolvedValue(entry),
        listEntries: vi.fn(),
        updateEntryAnalysis: vi.fn(),
        updateEntryEmbedding: vi.fn(),
        searchByKeyword: vi.fn(),
        searchByVector: vi.fn(),
        deleteEntry: vi.fn(),
        close: vi.fn(),
      } as IStorageService,
      agent: {
        analyze: vi.fn().mockRejectedValue(new Error('LLM API unavailable')),
      } as unknown as TomAgent,
    });
    (buildServicesFromConfig as unknown as Mock).mockReturnValue(mockServices);

    const result = await runAnalyzePipeline('entry-uuid-1234');

    expect(result.error).toBeTruthy();
    expect(result.analysis).toBeNull();
    // updateEntryAnalysis must NOT have been called with broken data
    expect(mockServices.storage.updateEntryAnalysis).not.toHaveBeenCalled();
  });
});
