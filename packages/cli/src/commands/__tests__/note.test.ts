import { describe, it, expect, vi, beforeEach } from 'vitest';
import type { Mock } from 'vitest';

// ---------------------------------------------------------------------------
// We test the exported pipeline helpers and note command pipeline directly.
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

// Mock ink-text-input
vi.mock('ink-text-input', () => ({ default: vi.fn() }));

// Mock the record module so we can spy on runAnalysisPipeline
vi.mock('../record.js', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../record.js')>();
  return {
    ...actual,
    runAnalysisPipeline: vi.fn(),
    buildServicesFromConfig: vi.fn(),
  };
});

import {
  ConfigManager,
} from '@ten-second-tom/core';
import type {
  IEmbeddingService,
  IStorageService,
  EntryAnalysis,
  AppConfig,
} from '@ten-second-tom/core';
import type { TomAgent } from '@ten-second-tom/core';

import { runAnalysisPipeline, buildServicesFromConfig } from '../record.js';
import type { RecordingPipelineServices } from '../record.js';
import { runNotePipeline } from '../note.js';

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

function makeMockServices(overrides: Partial<RecordingPipelineServices> = {}): RecordingPipelineServices {
  const mockEntry = {
    id: 'note-uuid-5678',
    type: 'note' as const,
    content: 'my typed note',
    inputMethod: 'typed' as const,
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
  };

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
  } as unknown as TomAgent;

  const embedding: IEmbeddingService = {
    embed: vi.fn().mockResolvedValue(new Float32Array([0.1, 0.2, 0.3])),
    isAvailable: vi.fn().mockResolvedValue(true),
  };

  // audio and transcription not used for notes but required by the type
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

  return { audio, transcription, agent, embedding, storage, ...overrides } as RecordingPipelineServices;
}

// ---------------------------------------------------------------------------
// Tests: runNotePipeline — setup guard
// ---------------------------------------------------------------------------

describe('runNotePipeline', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('returns an error result when setup is not complete', async () => {
    const mockConfigManager = {
      isSetupComplete: vi.fn().mockReturnValue(false),
      load: vi.fn(),
      audioPath: '/tmp/audio',
    };
    (ConfigManager as unknown as Mock).mockImplementation(() => mockConfigManager);

    const result = await runNotePipeline('some note text');

    expect(result.error).toContain('tom setup');
    expect(result.analysis).toBeNull();
  });

  it('calls runAnalysisPipeline with note text, no audioPath, type note, inputMethod typed', async () => {
    const mockConfigManager = {
      isSetupComplete: vi.fn().mockReturnValue(true),
      load: vi.fn().mockReturnValue({
        llm: { provider: 'cloud', apiKey: 'sk-test' },
        stt: { engine: 'whisper.node', modelPath: '/models/model.bin' },
        embedding: { provider: 'none', model: '' },
        storage: { dbPath: '/tmp/test.db' },
      } as AppConfig),
      audioPath: '/tmp/audio',
    };
    (ConfigManager as unknown as Mock).mockImplementation(() => mockConfigManager);

    const mockServices = makeMockServices();
    (buildServicesFromConfig as unknown as Mock).mockReturnValue(mockServices);
    (runAnalysisPipeline as unknown as Mock).mockResolvedValue({
      entryId: 'note-uuid-5678',
      transcript: 'my typed note',
      audioPath: undefined,
      analysis: makeAnalysis(),
      embeddingStored: true,
      warnings: [],
    });

    const result = await runNotePipeline('my typed note');

    expect(runAnalysisPipeline).toHaveBeenCalledWith(
      'my typed note',
      undefined,
      mockServices,
      { entryType: 'note', inputMethod: 'typed' },
    );
    expect(result.error).toBeNull();
    expect(result.analysis).toEqual(makeAnalysis());
    expect(result.warnings).toHaveLength(0);
  });

  it('returns null analysis and a warning when TomAgent is unavailable', async () => {
    const mockConfigManager = {
      isSetupComplete: vi.fn().mockReturnValue(true),
      load: vi.fn().mockReturnValue({
        llm: { provider: 'cloud', apiKey: 'sk-test' },
        stt: { engine: 'whisper.node', modelPath: '/models/model.bin' },
        embedding: { provider: 'none', model: '' },
        storage: { dbPath: '/tmp/test.db' },
      } as AppConfig),
      audioPath: '/tmp/audio',
    };
    (ConfigManager as unknown as Mock).mockImplementation(() => mockConfigManager);

    const mockServices = makeMockServices();
    (buildServicesFromConfig as unknown as Mock).mockReturnValue(mockServices);
    (runAnalysisPipeline as unknown as Mock).mockResolvedValue({
      entryId: 'note-uuid-5678',
      transcript: 'my typed note',
      audioPath: undefined,
      analysis: null,
      embeddingStored: true,
      warnings: ['AI analysis unavailable — entry saved without analysis. Check your LLM configuration.'],
    });

    const result = await runNotePipeline('my typed note');

    expect(result.error).toBeNull();
    expect(result.analysis).toBeNull();
    expect(result.warnings.some((w) => w.includes('AI analysis unavailable'))).toBe(true);
  });

  it('returns a warning when embedding service is unavailable', async () => {
    const mockConfigManager = {
      isSetupComplete: vi.fn().mockReturnValue(true),
      load: vi.fn().mockReturnValue({
        llm: { provider: 'cloud', apiKey: 'sk-test' },
        stt: { engine: 'whisper.node', modelPath: '/models/model.bin' },
        embedding: { provider: 'none', model: '' },
        storage: { dbPath: '/tmp/test.db' },
      } as AppConfig),
      audioPath: '/tmp/audio',
    };
    (ConfigManager as unknown as Mock).mockImplementation(() => mockConfigManager);

    const mockServices = makeMockServices();
    (buildServicesFromConfig as unknown as Mock).mockReturnValue(mockServices);
    (runAnalysisPipeline as unknown as Mock).mockResolvedValue({
      entryId: 'note-uuid-5678',
      transcript: 'my typed note',
      audioPath: undefined,
      analysis: makeAnalysis(),
      embeddingStored: false,
      warnings: ['Embedding unavailable — entry saved without vector index.'],
    });

    const result = await runNotePipeline('my typed note');

    expect(result.error).toBeNull();
    expect(result.analysis).toEqual(makeAnalysis());
    expect(result.warnings.some((w) => w.includes('Embedding unavailable'))).toBe(true);
  });

  it('ignores empty input and does not call runAnalysisPipeline', async () => {
    const mockConfigManager = {
      isSetupComplete: vi.fn().mockReturnValue(true),
      load: vi.fn(),
      audioPath: '/tmp/audio',
    };
    (ConfigManager as unknown as Mock).mockImplementation(() => mockConfigManager);

    const result = await runNotePipeline('   ');

    expect(runAnalysisPipeline).not.toHaveBeenCalled();
    expect(result.error).toContain('empty');
  });
});
