import { describe, it, expect, vi } from 'vitest';
import type {
  IAgentService,
  IEmbeddingService,
  IStorageService,
  ISearchService,
  IAudioService,
  ITranscriptionService,
  ILiveTranscriptionService,
  Entry,
  EntryAnalysis,
  ServiceContainer,
} from '../../index.js';
import { reanalyzeEntry, runAnalysisPipeline } from '../pipeline.js';

function makeAnalysis(): EntryAnalysis {
  return {
    sentiment: { score: 0.5, label: 'positive', confidence: 0.9 },
    summary: 'A positive entry',
    raw: {},
  };
}

function makeEntry(overrides: Partial<Entry> = {}): Entry {
  return {
    id: '00000000-0000-4000-8000-000000000001',
    type: 'recording',
    content: 'hello world',
    audioPath: '2026-03/2026-03-22-abcd1234.wav',
    inputMethod: 'recorded',
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
    ...overrides,
  };
}

function makeMockServices(overrides: Partial<ServiceContainer> = {}): ServiceContainer {
  const mockEntry = makeEntry();

  const storage: IStorageService = {
    saveEntry: vi.fn().mockResolvedValue(mockEntry),
    getEntry: vi.fn().mockResolvedValue(mockEntry),
    listEntries: vi.fn().mockResolvedValue([]),
    countEntries: vi.fn().mockResolvedValue(0),
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
    transcribeFile: vi.fn().mockResolvedValue('hello world'),
    isModelLoaded: vi.fn().mockReturnValue(true),
    loadModel: vi.fn().mockResolvedValue(undefined),
    release: vi.fn().mockResolvedValue(undefined),
  };

  const liveTranscription: ILiveTranscriptionService = {
    start: vi.fn(),
    stop: vi.fn(),
    isAvailable: vi.fn().mockReturnValue(false),
  };

  const agent = {
    analyze: vi.fn().mockResolvedValue(makeAnalysis()),
  } as IAgentService;

  const embedding: IEmbeddingService = {
    embed: vi.fn().mockResolvedValue(new Float32Array([0.1, 0.2, 0.3])),
    isAvailable: vi.fn().mockResolvedValue(true),
  };

  const search: ISearchService = {
    search: vi.fn().mockResolvedValue([]),
  };

  return {
    audio,
    transcription,
    liveTranscription,
    agent,
    embedding,
    storage,
    search,
    ...overrides,
  };
}

describe('runAnalysisPipeline', () => {
  it('saves entry, runs analysis and embedding, returns result', async () => {
    const services = makeMockServices();
    const transcript = 'hello world';
    const audioPath = '2026-03/2026-03-22-abcd1234.wav';

    const result = await runAnalysisPipeline(transcript, audioPath, services);

    expect(services.storage.saveEntry).toHaveBeenCalledWith({
      type: 'recording',
      content: transcript,
      audioPath,
      inputMethod: 'recorded',
    });
    expect(services.agent.analyze).toHaveBeenCalledWith(transcript);
    expect(services.storage.updateEntryAnalysis).toHaveBeenCalledWith(
      '00000000-0000-4000-8000-000000000001',
      makeAnalysis(),
    );
    expect(services.embedding.embed).toHaveBeenCalledWith(transcript);
    expect(services.storage.updateEntryEmbedding).toHaveBeenCalledWith(
      '00000000-0000-4000-8000-000000000001',
      expect.any(Float32Array),
    );
    expect(result.analysis).toEqual(makeAnalysis());
    expect(result.embeddingStored).toBe(true);
    expect(result.warnings).toHaveLength(0);
    expect(result.entryId).toBe('00000000-0000-4000-8000-000000000001');
  });

  it('saves entry without analysis when agent.analyze rejects', async () => {
    const services = makeMockServices({
      agent: {
        analyze: vi.fn().mockRejectedValue(new Error('API key invalid')),
      } as unknown as IAgentService,
    });

    const result = await runAnalysisPipeline('hello world', 'audio.wav', services);

    expect(services.storage.saveEntry).toHaveBeenCalled();
    expect(services.storage.updateEntryAnalysis).not.toHaveBeenCalled();
    expect(result.analysis).toBeNull();
    expect(result.warnings).toHaveLength(1);
    expect(result.warnings[0]).toContain('AI analysis unavailable');
    expect(result.warnings[0]).toContain('API key invalid');
  });

  it('warns and continues when analysis storage fails after capture is saved', async () => {
    const services = makeMockServices({
      storage: {
        ...makeMockServices().storage,
        saveEntry: vi.fn().mockResolvedValue(makeEntry()),
        updateEntryAnalysis: vi.fn().mockRejectedValue(new Error('database locked')),
      },
    });

    const result = await runAnalysisPipeline('hello world', 'audio.wav', services);

    expect(services.storage.saveEntry).toHaveBeenCalled();
    expect(result.analysis).toEqual(makeAnalysis());
    expect(result.warnings).toContain(
      'Analysis storage unavailable — entry saved without persisted analysis.',
    );
  });

  it('saves entry without embedding when embed rejects', async () => {
    const services = makeMockServices({
      embedding: {
        embed: vi.fn().mockRejectedValue(new Error('Ollama down')),
        isAvailable: vi.fn().mockResolvedValue(false),
      } as IEmbeddingService,
    });

    const result = await runAnalysisPipeline('hello world', 'audio.wav', services);

    expect(services.storage.saveEntry).toHaveBeenCalled();
    expect(services.storage.updateEntryEmbedding).not.toHaveBeenCalled();
    expect(result.analysis).toEqual(makeAnalysis());
    expect(result.embeddingStored).toBe(false);
    expect(result.warnings).toHaveLength(1);
    expect(result.warnings[0]).toContain('Embedding unavailable');
  });

  it('saves entry with only transcript when both agent and embedding fail', async () => {
    const services = makeMockServices({
      agent: {
        analyze: vi.fn().mockRejectedValue(new Error('LLM offline')),
      } as unknown as IAgentService,
      embedding: {
        embed: vi.fn().mockRejectedValue(new Error('Ollama down')),
        isAvailable: vi.fn().mockResolvedValue(false),
      } as IEmbeddingService,
    });

    const result = await runAnalysisPipeline('hello world', 'audio.wav', services);

    expect(services.storage.saveEntry).toHaveBeenCalled();
    expect(result.analysis).toBeNull();
    expect(result.embeddingStored).toBe(false);
    expect(result.warnings).toHaveLength(2);
  });

  it('respects PipelineOptions for note entries', async () => {
    const services = makeMockServices();
    const transcript = 'my typed note';

    const result = await runAnalysisPipeline(transcript, undefined, services, {
      entryType: 'note',
      inputMethod: 'typed',
    });

    expect(services.storage.saveEntry).toHaveBeenCalledWith({
      type: 'note',
      content: transcript,
      audioPath: undefined,
      inputMethod: 'typed',
    });
    expect(result.entryId).toBe('00000000-0000-4000-8000-000000000001');
  });
});

describe('reanalyzeEntry', () => {
  it('returns undefined when the entry does not exist', async () => {
    const services = makeMockServices({
      storage: {
        ...makeMockServices().storage,
        getEntry: vi.fn().mockResolvedValue(undefined),
      },
    });

    await expect(reanalyzeEntry('missing', services)).resolves.toBeUndefined();
  });

  it('reruns analysis and embedding for an existing entry', async () => {
    const services = makeMockServices();

    const result = await reanalyzeEntry('00000000-0000-4000-8000-000000000001', services);

    expect(result?.entry.id).toBe('00000000-0000-4000-8000-000000000001');
    expect(result?.analysis).toEqual(makeAnalysis());
    expect(result?.embeddingStored).toBe(true);
  });
});
