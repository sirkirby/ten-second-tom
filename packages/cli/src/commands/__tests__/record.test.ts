import { describe, it, expect, vi, beforeEach } from 'vitest';
import type { Mock } from 'vitest';

// ---------------------------------------------------------------------------
// We test the exported pipeline helpers directly (no Ink rendering needed).
// ---------------------------------------------------------------------------

// Hoist mock constructors so the buildServicesFromConfig mock factory can reference them
const {
  mockAudioServiceCtor,
  mockWhisperCtor,
  mockTomAgentCtor,
  mockOllamaEmbCtor,
  mockNoopEmbCtor,
  mockSqliteCtor,
} = vi.hoisted(() => ({
  mockAudioServiceCtor: vi.fn(),
  mockWhisperCtor: vi.fn(),
  mockTomAgentCtor: vi.fn(),
  mockOllamaEmbCtor: vi.fn(),
  mockNoopEmbCtor: vi.fn(),
  mockSqliteCtor: vi.fn(),
}));

// Mock @ten-second-tom/core before importing the module under test
vi.mock('@ten-second-tom/core', () => {
  return {
    ConfigManager: vi.fn(),
    AudioService: mockAudioServiceCtor,
    WhisperTranscriptionService: mockWhisperCtor,
    TomAgent: mockTomAgentCtor,
    OllamaEmbeddingService: mockOllamaEmbCtor,
    NoopEmbeddingService: mockNoopEmbCtor,
    SqliteStorageService: mockSqliteCtor,
    buildServicesFromConfig: vi.fn(
      (config: Record<string, unknown>, configManager: Record<string, unknown>) => {
        const audio = new mockAudioServiceCtor({
          audioDir: (configManager as Record<string, unknown>)['audioPath'],
        });
        const transcription = new mockWhisperCtor();
        const agent = new mockTomAgentCtor((config as Record<string, unknown>)['llm']);
        const embeddingConfig = (config as Record<string, Record<string, unknown>>)['embedding'];
        const embedding =
          embeddingConfig['provider'] === 'ollama'
            ? new mockOllamaEmbCtor({
                model: embeddingConfig['model'],
                endpoint: embeddingConfig['endpoint'],
              })
            : new mockNoopEmbCtor();
        const storage = new mockSqliteCtor(
          (config as Record<string, Record<string, unknown>>)['storage']['dbPath'],
        );
        return { audio, transcription, agent, embedding, storage };
      },
    ),
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

// Mock ink-spinner
vi.mock('ink-spinner', () => ({ default: vi.fn() }));

// Mock React
vi.mock('react', () => ({
  default: { createElement: vi.fn() },
  useState: vi.fn(),
  useEffect: vi.fn(),
}));

// Mock the local UI components so their imports don't explode
vi.mock('../../components/RecordingUI.js', () => ({ RecordingUI: vi.fn() }));
vi.mock('../../components/SentimentDisplay.js', () => ({ SentimentDisplay: vi.fn() }));
vi.mock('../../components/ErrorDisplay.js', () => ({ ErrorDisplay: vi.fn() }));
vi.mock('../../components/WarningList.js', () => ({ WarningList: vi.fn() }));
vi.mock('../../hooks/useAutoExit.js', () => ({ useAutoExit: vi.fn() }));
vi.mock('../../hooks/useSetupGuard.js', () => ({
  checkSetupComplete: vi.fn(() => ({ ok: true, config: {}, configManager: {} })),
}));

import { ConfigManager, buildServicesFromConfig } from '@ten-second-tom/core';
import type {
  IAudioService,
  ITranscriptionService,
  IEmbeddingService,
  IAgentService,
  IStorageService,
  EntryAnalysis,
  AppConfig,
} from '@ten-second-tom/core';

import { runAnalysisPipeline } from '../record.js';
import type { RecordingPipelineServices } from '../record.js';

// Alias hoisted mocks for use in assertions
const AudioService = mockAudioServiceCtor;
const WhisperTranscriptionService = mockWhisperCtor;
const TomAgent = mockTomAgentCtor;
const OllamaEmbeddingService = mockOllamaEmbCtor;
const NoopEmbeddingService = mockNoopEmbCtor;
const SqliteStorageService = mockSqliteCtor;

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

function makeMockServices(
  overrides: Partial<RecordingPipelineServices> = {},
): RecordingPipelineServices {
  const mockEntry = {
    id: 'test-uuid-1234',
    type: 'recording' as const,
    content: 'hello world',
    audioPath: '2026-03/2026-03-22-abcd1234.wav',
    inputMethod: 'recorded' as const,
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

  const audio: IAudioService = {
    startRecording: vi.fn(),
    stopRecording: vi.fn().mockResolvedValue('2026-03/2026-03-22-abcd1234.wav'),
    getAudioStream: vi.fn(),
    isRecording: vi.fn().mockReturnValue(false),
  };

  const transcription: ITranscriptionService = {
    transcribeStream: vi.fn().mockResolvedValue('hello world'),
    transcribeFile: vi.fn().mockResolvedValue('hello world'),
    isModelLoaded: vi.fn().mockReturnValue(true),
    loadModel: vi.fn().mockResolvedValue(undefined),
  };

  const agent = {
    analyze: vi.fn().mockResolvedValue(makeAnalysis()),
  } as IAgentService;

  const embedding: IEmbeddingService = {
    embed: vi.fn().mockResolvedValue(new Float32Array([0.1, 0.2, 0.3])),
    isAvailable: vi.fn().mockResolvedValue(true),
  };

  return { audio, transcription, agent, embedding, storage, ...overrides };
}

// ---------------------------------------------------------------------------
// Tests: runAnalysisPipeline
// ---------------------------------------------------------------------------

describe('runAnalysisPipeline', () => {
  it('saves entry, runs analysis and embedding, returns result', async () => {
    const services = makeMockServices();
    const transcript = 'hello world';
    const audioPath = '2026-03/2026-03-22-abcd1234.wav';

    const result = await runAnalysisPipeline(transcript, audioPath, services);

    // Entry was saved
    expect(services.storage.saveEntry).toHaveBeenCalledWith({
      type: 'recording',
      content: transcript,
      audioPath,
      inputMethod: 'recorded',
    });

    // Analysis was run and stored
    expect(services.agent.analyze).toHaveBeenCalledWith(transcript);
    expect(services.storage.updateEntryAnalysis).toHaveBeenCalledWith(
      'test-uuid-1234',
      makeAnalysis(),
    );

    // Embedding was run and stored
    expect(services.embedding.embed).toHaveBeenCalledWith(transcript);
    expect(services.storage.updateEntryEmbedding).toHaveBeenCalledWith(
      'test-uuid-1234',
      expect.any(Float32Array),
    );

    expect(result.analysis).toEqual(makeAnalysis());
    expect(result.embeddingStored).toBe(true);
    expect(result.warnings).toHaveLength(0);
    expect(result.entryId).toBe('test-uuid-1234');
  });

  it('saves entry without analysis when TomAgent.analyze rejects', async () => {
    const services = makeMockServices({
      agent: {
        analyze: vi.fn().mockRejectedValue(new Error('API key invalid')),
      } as unknown as TomAgent,
    });

    const result = await runAnalysisPipeline('hello world', 'audio.wav', services);

    // Entry was still saved
    expect(services.storage.saveEntry).toHaveBeenCalled();

    // Analysis update was NOT called
    expect(services.storage.updateEntryAnalysis).not.toHaveBeenCalled();

    // Result reflects degraded state
    expect(result.analysis).toBeNull();
    expect(result.warnings).toHaveLength(1);
    expect(result.warnings[0]).toContain('AI analysis unavailable');
  });

  it('saves entry without embedding when embed rejects', async () => {
    const services = makeMockServices({
      embedding: {
        embed: vi.fn().mockRejectedValue(new Error('Ollama down')),
        isAvailable: vi.fn().mockResolvedValue(false),
      } as IEmbeddingService,
    });

    const result = await runAnalysisPipeline('hello world', 'audio.wav', services);

    // Entry was still saved
    expect(services.storage.saveEntry).toHaveBeenCalled();

    // Embedding update was NOT called
    expect(services.storage.updateEntryEmbedding).not.toHaveBeenCalled();

    // Analysis still succeeded
    expect(result.analysis).toEqual(makeAnalysis());
    expect(result.embeddingStored).toBe(false);
    expect(result.warnings).toHaveLength(1);
    expect(result.warnings[0]).toContain('Embedding unavailable');
  });

  it('saves entry with only transcript when both agent and embedding fail', async () => {
    const services = makeMockServices({
      agent: {
        analyze: vi.fn().mockRejectedValue(new Error('LLM offline')),
      } as unknown as TomAgent,
      embedding: {
        embed: vi.fn().mockRejectedValue(new Error('Ollama down')),
        isAvailable: vi.fn().mockResolvedValue(false),
      } as IEmbeddingService,
    });

    const result = await runAnalysisPipeline('hello world', 'audio.wav', services);

    // Entry was still saved
    expect(services.storage.saveEntry).toHaveBeenCalled();

    expect(result.analysis).toBeNull();
    expect(result.embeddingStored).toBe(false);
    expect(result.warnings).toHaveLength(2);
  });
});

// ---------------------------------------------------------------------------
// Tests: buildServicesFromConfig
// ---------------------------------------------------------------------------

describe('buildServicesFromConfig', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    // Provide constructor implementations
    (AudioService as unknown as Mock).mockImplementation(() => ({ startRecording: vi.fn() }));
    (WhisperTranscriptionService as unknown as Mock).mockImplementation(() => ({
      isModelLoaded: vi.fn(),
    }));
    (TomAgent as unknown as Mock).mockImplementation(() => ({ analyze: vi.fn() }));
    (OllamaEmbeddingService as unknown as Mock).mockImplementation(() => ({ embed: vi.fn() }));
    (NoopEmbeddingService as unknown as Mock).mockImplementation(() => ({ embed: vi.fn() }));
    (SqliteStorageService as unknown as Mock).mockImplementation(() => ({ saveEntry: vi.fn() }));
  });

  it('builds OllamaEmbeddingService for ollama provider', () => {
    const config: AppConfig = {
      llm: { provider: 'cloud', apiKey: 'sk-test' },
      stt: { engine: 'whisper.node', modelPath: '/models/model.bin' },
      embedding: {
        provider: 'ollama',
        model: 'nomic-embed-text',
        endpoint: 'http://localhost:11434',
      },
      storage: { dbPath: '/tmp/test.db' },
    };
    const configManager = new ConfigManager('/tmp/test-home');
    (configManager as unknown as Record<string, unknown>)['audioPath'] = '/tmp/test-home/audio';

    buildServicesFromConfig(config, configManager);

    expect(OllamaEmbeddingService).toHaveBeenCalledWith({
      model: 'nomic-embed-text',
      endpoint: 'http://localhost:11434',
    });
    expect(NoopEmbeddingService).not.toHaveBeenCalled();
  });

  it('builds NoopEmbeddingService for none provider', () => {
    const config: AppConfig = {
      llm: { provider: 'cloud', apiKey: 'sk-test' },
      stt: { engine: 'whisper.node', modelPath: '/models/model.bin' },
      embedding: { provider: 'none', model: '' },
      storage: { dbPath: '/tmp/test.db' },
    };
    const configManager = new ConfigManager('/tmp/test-home');
    (configManager as unknown as Record<string, unknown>)['audioPath'] = '/tmp/test-home/audio';

    buildServicesFromConfig(config, configManager);

    expect(NoopEmbeddingService).toHaveBeenCalled();
    expect(OllamaEmbeddingService).not.toHaveBeenCalled();
  });

  it('builds TomAgent with cloud provider config', () => {
    const config: AppConfig = {
      llm: { provider: 'cloud', apiKey: 'sk-ant-abc' },
      stt: { engine: 'whisper.node', modelPath: '/models/model.bin' },
      embedding: { provider: 'none', model: '' },
      storage: { dbPath: '/tmp/test.db' },
    };
    const configManager = new ConfigManager('/tmp/test-home');
    (configManager as unknown as Record<string, unknown>)['audioPath'] = '/tmp/test-home/audio';

    buildServicesFromConfig(config, configManager);

    expect(TomAgent).toHaveBeenCalledWith({
      provider: 'cloud',
      apiKey: 'sk-ant-abc',
    });
  });

  it('builds TomAgent with local provider config', () => {
    const config: AppConfig = {
      llm: { provider: 'local', localEndpoint: 'http://localhost:11434', modelId: 'qwen2.5:7b' },
      stt: { engine: 'whisper.node', modelPath: '/models/model.bin' },
      embedding: { provider: 'none', model: '' },
      storage: { dbPath: '/tmp/test.db' },
    };
    const configManager = new ConfigManager('/tmp/test-home');
    (configManager as unknown as Record<string, unknown>)['audioPath'] = '/tmp/test-home/audio';

    buildServicesFromConfig(config, configManager);

    expect(TomAgent).toHaveBeenCalledWith({
      provider: 'local',
      localEndpoint: 'http://localhost:11434',
      modelId: 'qwen2.5:7b',
    });
  });
});
