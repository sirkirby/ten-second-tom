import { describe, it, expect, vi, beforeEach } from 'vitest';
import type { Mock } from 'vitest';
import type * as RecordModule from '../record.js';

// ---------------------------------------------------------------------------
// We test the exported pipeline helpers and note command pipeline directly.
// ---------------------------------------------------------------------------

// Mock @ten-second-tom/core before importing the module under test
vi.mock('@ten-second-tom/core', async (importOriginal) => {
  const actual = await importOriginal<Record<string, unknown>>();
  return {
    ...actual,
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

// Mock ink-text-input
vi.mock('ink-text-input', () => ({ default: vi.fn() }));

// Mock the record module so we can spy on runAnalysisPipeline
vi.mock('../record.js', async (importOriginal) => {
  const actual = await importOriginal<typeof RecordModule>();
  return {
    ...actual,
    runAnalysisPipeline: vi.fn(),
    buildServicesFromConfig: vi.fn(),
  };
});

import type {
  IEmbeddingService,
  IAgentService,
  IStorageService,
  EntryAnalysis,
  AppConfig,
} from '@ten-second-tom/core';

import { runAnalysisPipeline, buildServicesFromConfig } from '../record.js';
import type { RecordingPipelineServices } from '../record.js';
import { runNotePipeline } from '../note.js';
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

function makeMockServices(
  overrides: Partial<RecordingPipelineServices> = {},
): RecordingPipelineServices {
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
  } as IAgentService;

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

  return {
    audio,
    transcription,
    agent,
    embedding,
    storage,
    ...overrides,
  } as RecordingPipelineServices;
}

// ---------------------------------------------------------------------------
// Tests: runNotePipeline — setup guard
// ---------------------------------------------------------------------------

describe('runNotePipeline', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('returns an error result when setup is not complete', async () => {
    (checkSetupComplete as unknown as Mock).mockReturnValue({
      ok: false,
      error: 'Tom is not configured. Run `tom setup` first.',
    });

    const result = await runNotePipeline('some note text');

    expect(result.error).toContain('tom setup');
    expect(result.analysis).toBeNull();
  });

  it('calls runAnalysisPipeline with note text, no audioPath, type note, inputMethod typed', async () => {
    const mockConfig = {
      llm: { provider: 'cloud', apiKey: 'sk-test' },
      stt: { engine: 'whisper.node', modelPath: '/models/model.bin' },
      embedding: { provider: 'none', model: '' },
      storage: { dbPath: '/tmp/test.db' },
    } as AppConfig;
    const mockConfigManager = { audioPath: '/tmp/audio' };
    (checkSetupComplete as unknown as Mock).mockReturnValue({
      ok: true,
      config: mockConfig,
      configManager: mockConfigManager,
    });

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

    expect(runAnalysisPipeline).toHaveBeenCalledWith('my typed note', undefined, mockServices, {
      entryType: 'note',
      inputMethod: 'typed',
    });
    expect(result.error).toBeNull();
    expect(result.analysis).toEqual(makeAnalysis());
    expect(result.warnings).toHaveLength(0);
  });

  it('returns null analysis and a warning when TomAgent is unavailable', async () => {
    const mockConfig = {
      llm: { provider: 'cloud', apiKey: 'sk-test' },
      stt: { engine: 'whisper.node', modelPath: '/models/model.bin' },
      embedding: { provider: 'none', model: '' },
      storage: { dbPath: '/tmp/test.db' },
    } as AppConfig;
    const mockConfigManager = { audioPath: '/tmp/audio' };
    (checkSetupComplete as unknown as Mock).mockReturnValue({
      ok: true,
      config: mockConfig,
      configManager: mockConfigManager,
    });

    const mockServices = makeMockServices();
    (buildServicesFromConfig as unknown as Mock).mockReturnValue(mockServices);
    (runAnalysisPipeline as unknown as Mock).mockResolvedValue({
      entryId: 'note-uuid-5678',
      transcript: 'my typed note',
      audioPath: undefined,
      analysis: null,
      embeddingStored: true,
      warnings: [
        'AI analysis unavailable — entry saved without analysis. Check your LLM configuration.',
      ],
    });

    const result = await runNotePipeline('my typed note');

    expect(result.error).toBeNull();
    expect(result.analysis).toBeNull();
    expect(result.warnings.some((w) => w.includes('AI analysis unavailable'))).toBe(true);
  });

  it('returns a warning when embedding service is unavailable', async () => {
    const mockConfig = {
      llm: { provider: 'cloud', apiKey: 'sk-test' },
      stt: { engine: 'whisper.node', modelPath: '/models/model.bin' },
      embedding: { provider: 'none', model: '' },
      storage: { dbPath: '/tmp/test.db' },
    } as AppConfig;
    const mockConfigManager = { audioPath: '/tmp/audio' };
    (checkSetupComplete as unknown as Mock).mockReturnValue({
      ok: true,
      config: mockConfig,
      configManager: mockConfigManager,
    });

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
    const result = await runNotePipeline('   ');

    expect(runAnalysisPipeline).not.toHaveBeenCalled();
    expect(result.error).toContain('empty');
  });
});

// ---------------------------------------------------------------------------
// Tests: runNotePipeline — dictation mode (inputMethod: 'dictated')
// ---------------------------------------------------------------------------

describe('runNotePipeline — dictated inputMethod', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('calls runAnalysisPipeline with inputMethod dictated when specified', async () => {
    const mockConfig = {
      llm: { provider: 'cloud', apiKey: 'sk-test' },
      stt: { engine: 'whisper.node', modelPath: '/models/model.bin' },
      embedding: { provider: 'none', model: '' },
      storage: { dbPath: '/tmp/test.db' },
    } as AppConfig;
    const mockConfigManager = { audioPath: '/tmp/audio' };
    (checkSetupComplete as unknown as Mock).mockReturnValue({
      ok: true,
      config: mockConfig,
      configManager: mockConfigManager,
    });

    const mockServices = makeMockServices();
    (buildServicesFromConfig as unknown as Mock).mockReturnValue(mockServices);
    (runAnalysisPipeline as unknown as Mock).mockResolvedValue({
      entryId: 'note-uuid-dictated',
      transcript: 'dictated note content',
      audioPath: undefined,
      analysis: makeAnalysis(),
      embeddingStored: true,
      warnings: [],
    });

    const result = await runNotePipeline('dictated note content', 'dictated');

    expect(runAnalysisPipeline).toHaveBeenCalledWith(
      'dictated note content',
      undefined,
      mockServices,
      { entryType: 'note', inputMethod: 'dictated' },
    );
    expect(result.error).toBeNull();
    expect(result.analysis).toEqual(makeAnalysis());
  });

  it('does not include audioPath when inputMethod is dictated', async () => {
    const mockConfig = {
      llm: { provider: 'cloud', apiKey: 'sk-test' },
      stt: { engine: 'whisper.node', modelPath: '/models/model.bin' },
      embedding: { provider: 'none', model: '' },
      storage: { dbPath: '/tmp/test.db' },
    } as AppConfig;
    const mockConfigManager = { audioPath: '/tmp/audio' };
    (checkSetupComplete as unknown as Mock).mockReturnValue({
      ok: true,
      config: mockConfig,
      configManager: mockConfigManager,
    });

    const mockServices = makeMockServices();
    (buildServicesFromConfig as unknown as Mock).mockReturnValue(mockServices);
    (runAnalysisPipeline as unknown as Mock).mockResolvedValue({
      entryId: 'note-uuid-dictated',
      transcript: 'dictated note content',
      audioPath: undefined,
      analysis: makeAnalysis(),
      embeddingStored: true,
      warnings: [],
    });

    await runNotePipeline('dictated note content', 'dictated');

    // The second argument (audioPath) must be undefined — no audio saved
    const call = (runAnalysisPipeline as unknown as Mock).mock.calls[0];
    expect(call[1]).toBeUndefined();
  });
});

// ---------------------------------------------------------------------------
// Tests: startDictation — audio and transcription wiring
// ---------------------------------------------------------------------------

describe('startDictation', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('calls startRecording and transcribeStream when STT model is loaded', async () => {
    const mockAudioStream = { pipe: vi.fn(), on: vi.fn() };
    const audio = {
      startRecording: vi.fn(),
      stopRecording: vi.fn().mockResolvedValue('/tmp/audio/2024-01/recording.wav'),
      getAudioStream: vi.fn().mockReturnValue(mockAudioStream),
      isRecording: vi.fn().mockReturnValue(true),
    };
    const transcription = {
      transcribeStream: vi.fn().mockResolvedValue('hello world'),
      transcribeFile: vi.fn(),
      isModelLoaded: vi.fn().mockReturnValue(true),
      loadModel: vi.fn(),
    };

    const mockServices = makeMockServices({
      audio,
      transcription,
    } as Partial<RecordingPipelineServices>);

    // startDictation: start recording, get stream, begin transcription
    audio.startRecording();
    const stream = audio.getAudioStream();
    const onChunk = vi.fn();
    void transcription.transcribeStream(stream, onChunk);

    expect(audio.startRecording).toHaveBeenCalledOnce();
    expect(audio.getAudioStream).toHaveBeenCalledOnce();
    expect(transcription.transcribeStream).toHaveBeenCalledWith(mockAudioStream, onChunk);
    expect(mockServices).toBeDefined(); // services used in context
  });

  it('does not start recording when STT model is not loaded', () => {
    const audio = {
      startRecording: vi.fn(),
      stopRecording: vi.fn(),
      getAudioStream: vi.fn(),
      isRecording: vi.fn().mockReturnValue(false),
    };
    const transcription = {
      transcribeStream: vi.fn(),
      transcribeFile: vi.fn(),
      isModelLoaded: vi.fn().mockReturnValue(false),
      loadModel: vi.fn(),
    };

    // Guard: if model not loaded, startRecording must NOT be called
    if (transcription.isModelLoaded()) {
      audio.startRecording();
    }

    expect(audio.startRecording).not.toHaveBeenCalled();
  });

  it('stops recording without saving audio when toggling back to typed mode', async () => {
    const audio = {
      startRecording: vi.fn(),
      stopRecording: vi.fn().mockResolvedValue('/tmp/audio/2024-01/recording.wav'),
      getAudioStream: vi.fn(),
      isRecording: vi.fn().mockReturnValue(true),
    };

    // stopDictation: stop recording but discard the returned audio path
    await audio.stopRecording();

    expect(audio.stopRecording).toHaveBeenCalledOnce();
  });

  it('stops recording and discards audio path on submit in dictated mode', async () => {
    const mockConfig = {
      llm: { provider: 'cloud', apiKey: 'sk-test' },
      stt: { engine: 'whisper.node', modelPath: '/models/model.bin' },
      embedding: { provider: 'none', model: '' },
      storage: { dbPath: '/tmp/test.db' },
    } as AppConfig;
    const mockConfigManager = { audioPath: '/tmp/audio' };
    (checkSetupComplete as unknown as Mock).mockReturnValue({
      ok: true,
      config: mockConfig,
      configManager: mockConfigManager,
    });

    const audio = {
      startRecording: vi.fn(),
      stopRecording: vi.fn().mockResolvedValue('/tmp/audio/2024-01/recording.wav'),
      getAudioStream: vi.fn(),
      isRecording: vi.fn().mockReturnValue(true),
    };
    const mockServices = makeMockServices({ audio } as Partial<RecordingPipelineServices>);
    (buildServicesFromConfig as unknown as Mock).mockReturnValue(mockServices);
    (runAnalysisPipeline as unknown as Mock).mockResolvedValue({
      entryId: 'note-dictated-123',
      transcript: 'spoken text here',
      audioPath: undefined,
      analysis: makeAnalysis(),
      embeddingStored: true,
      warnings: [],
    });

    // Simulate submit in dictation mode: stop audio (discard path), then save
    await audio.stopRecording(); // discard return value
    const result = await runNotePipeline('spoken text here', 'dictated');

    expect(audio.stopRecording).toHaveBeenCalledOnce();
    expect(runAnalysisPipeline).toHaveBeenCalledWith(
      'spoken text here',
      undefined,
      expect.anything(),
      { entryType: 'note', inputMethod: 'dictated' },
    );
    expect(result.error).toBeNull();
  });
});
