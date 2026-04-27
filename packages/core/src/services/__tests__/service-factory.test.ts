import { describe, it, expect, vi, beforeEach } from 'vitest';
import type { AppConfig } from '../../types/config.js';

const mocks = vi.hoisted(() => ({
  AudioService: vi.fn(),
  WhisperTranscriptionService: vi.fn(),
  SherpaOnnxLiveTranscriptionService: vi.fn(),
  NoopLiveTranscriptionService: vi.fn(),
  TomAgent: vi.fn(),
  OllamaEmbeddingService: vi.fn(),
  OpenAICompatibleEmbeddingService: vi.fn(),
  NoopEmbeddingService: vi.fn(),
  SqliteStorageService: vi.fn(),
  SearchService: vi.fn(),
}));

vi.mock('../audio.js', () => ({ AudioService: mocks.AudioService }));
vi.mock('../transcription.js', () => ({
  WhisperTranscriptionService: mocks.WhisperTranscriptionService,
}));
vi.mock('../live-transcription.js', () => ({
  SherpaOnnxLiveTranscriptionService: mocks.SherpaOnnxLiveTranscriptionService,
  NoopLiveTranscriptionService: mocks.NoopLiveTranscriptionService,
}));
vi.mock('../../agent/tom-agent.js', () => ({ TomAgent: mocks.TomAgent }));
vi.mock('../embedding.js', () => ({
  OllamaEmbeddingService: mocks.OllamaEmbeddingService,
  OpenAICompatibleEmbeddingService: mocks.OpenAICompatibleEmbeddingService,
  NoopEmbeddingService: mocks.NoopEmbeddingService,
}));
vi.mock('../storage-sqlite.js', () => ({ SqliteStorageService: mocks.SqliteStorageService }));
vi.mock('../search.js', () => ({ SearchService: mocks.SearchService }));

const { buildServicesFromConfig } = await import('../service-factory.js');

function makeConfig(overrides: Partial<AppConfig> = {}): AppConfig {
  return {
    llm: {
      provider: 'local',
      localEndpoint: 'http://localhost:11434',
      modelId: 'qwen2.5:7b',
    },
    stt: { engine: 'whisper.node', modelPath: '/models/whisper.bin' },
    embedding: {
      provider: 'ollama',
      model: 'bge-m3:latest',
      endpoint: 'http://localhost:11434',
    },
    storage: { dbPath: '/tmp/tom.db' },
    ...overrides,
  };
}

const configManager = {
  audioPath: '/tmp/tom/audio',
  modelsPath: '/tmp/tom/models',
};

beforeEach(() => {
  vi.clearAllMocks();
  mocks.AudioService.mockImplementation(function AudioService(config) {
    return { kind: 'audio', config };
  });
  mocks.WhisperTranscriptionService.mockImplementation(function WhisperTranscriptionService() {
    return { kind: 'transcription' };
  });
  mocks.SherpaOnnxLiveTranscriptionService.mockImplementation(function SherpaService(config) {
    return {
      kind: 'sherpa',
      config,
      isAvailable: vi.fn(() => true),
    };
  });
  mocks.NoopLiveTranscriptionService.mockImplementation(function NoopLiveTranscriptionService() {
    return { kind: 'noop-live' };
  });
  mocks.TomAgent.mockImplementation(function TomAgent(config) {
    return { kind: 'agent', config };
  });
  mocks.OllamaEmbeddingService.mockImplementation(function OllamaEmbeddingService(config) {
    return { kind: 'ollama', config };
  });
  mocks.OpenAICompatibleEmbeddingService.mockImplementation(
    function OpenAIEmbeddingService(config) {
      return {
        kind: 'openai-compatible',
        config,
      };
    },
  );
  mocks.NoopEmbeddingService.mockImplementation(function NoopEmbeddingService() {
    return { kind: 'noop-embedding' };
  });
  mocks.SqliteStorageService.mockImplementation(function SqliteStorageService(dbPath, dimension) {
    return {
      kind: 'storage',
      dbPath,
      dimension,
    };
  });
  mocks.SearchService.mockImplementation(function SearchService(storage, embedding) {
    return {
      kind: 'search',
      storage,
      embedding,
    };
  });
});

describe('buildServicesFromConfig', () => {
  it('builds the service graph and derives embedding dimensions', () => {
    const services = buildServicesFromConfig(makeConfig(), configManager as never);

    expect(mocks.AudioService).toHaveBeenCalledWith({ audioDir: '/tmp/tom/audio' });
    expect(mocks.SherpaOnnxLiveTranscriptionService).toHaveBeenCalledWith(
      expect.objectContaining({
        modelsPath: '/tmp/tom/models',
      }),
    );
    expect(mocks.OllamaEmbeddingService).toHaveBeenCalledWith({
      model: 'bge-m3:latest',
      endpoint: 'http://localhost:11434',
    });
    expect(mocks.SqliteStorageService).toHaveBeenCalledWith('/tmp/tom.db', 1024);
    expect(services.search).toEqual(expect.objectContaining({ kind: 'search' }));
  });

  it('uses noop live transcription when sherpa is unavailable', () => {
    mocks.SherpaOnnxLiveTranscriptionService.mockImplementation(function SherpaService() {
      return {
        isAvailable: vi.fn(() => false),
      };
    });

    const services = buildServicesFromConfig(makeConfig(), configManager as never);

    expect(services.liveTranscription).toEqual(expect.objectContaining({ kind: 'noop-live' }));
  });

  it('uses configured sherpa model metadata and honors disabled live transcription', () => {
    buildServicesFromConfig(
      makeConfig({
        liveTranscription: {
          provider: 'sherpa',
          sherpaModelId: 'zipformer-en-kroko-2025-08-06',
        },
      }),
      configManager as never,
    );

    expect(mocks.SherpaOnnxLiveTranscriptionService).toHaveBeenCalledWith(
      expect.objectContaining({
        modelDir: 'sherpa-onnx-streaming-zipformer-en-kroko-2025-08-06',
        encoderFilename: 'encoder.onnx',
        decoderFilename: 'decoder.onnx',
        joinerFilename: 'joiner.onnx',
        tokensFilename: 'tokens.txt',
      }),
    );

    const services = buildServicesFromConfig(
      makeConfig({
        liveTranscription: { provider: 'none' },
      }),
      configManager as never,
    );

    expect(services.liveTranscription).toEqual(expect.objectContaining({ kind: 'noop-live' }));
  });

  it('builds OpenRouter embeddings with the fixed base URL and API key', () => {
    buildServicesFromConfig(
      makeConfig({
        embedding: {
          provider: 'openrouter',
          model: 'openai/text-embedding-3-small',
          apiKey: 'sk-test',
        },
      }),
      configManager as never,
    );

    expect(mocks.OpenAICompatibleEmbeddingService).toHaveBeenCalledWith({
      baseUrl: 'https://openrouter.ai/api/v1',
      model: 'openai/text-embedding-3-small',
      apiKey: 'sk-test',
    });
    expect(mocks.SqliteStorageService).toHaveBeenCalledWith('/tmp/tom.db', 1536);
  });

  it('builds custom embeddings and no-op embeddings', () => {
    buildServicesFromConfig(
      makeConfig({
        embedding: {
          provider: 'custom',
          model: 'all-minilm',
          endpoint: 'http://localhost:1234/v1',
        },
      }),
      configManager as never,
    );
    expect(mocks.OpenAICompatibleEmbeddingService).toHaveBeenCalledWith({
      baseUrl: 'http://localhost:1234/v1',
      model: 'all-minilm',
    });
    expect(mocks.SqliteStorageService).toHaveBeenLastCalledWith('/tmp/tom.db', 384);

    buildServicesFromConfig(
      makeConfig({
        embedding: { provider: 'none', model: '' },
      }),
      configManager as never,
    );
    expect(mocks.NoopEmbeddingService).toHaveBeenCalled();
    expect(mocks.SqliteStorageService).toHaveBeenLastCalledWith('/tmp/tom.db', undefined);
  });
});
