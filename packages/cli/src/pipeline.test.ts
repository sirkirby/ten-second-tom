import { describe, it, expect, vi } from 'vitest';

// ---------------------------------------------------------------------------
// We test the exported pipeline helpers directly (no Ink rendering needed).
// ---------------------------------------------------------------------------

// Mock ten-second-tom-core before importing the module under test
vi.mock('ten-second-tom-core', () => ({
  // Only the type re-exports are needed; the pipeline function itself
  // doesn't call any core constructors — it receives services as an arg.
}));

import type {
  IAgentService,
  IEmbeddingService,
  IStorageService,
  ISearchService,
  IAudioService,
  ITranscriptionService,
  ILiveTranscriptionService,
  EntryAnalysis,
  ServiceContainer,
} from 'ten-second-tom-core';

import { runAnalysisPipeline } from './pipeline.js';

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

function makeMockServices(overrides: Partial<ServiceContainer> = {}): ServiceContainer {
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
    transcribeStream: vi.fn().mockResolvedValue('hello world'),
    transcribeFile: vi.fn().mockResolvedValue('hello world'),
    isModelLoaded: vi.fn().mockReturnValue(true),
    loadModel: vi.fn().mockResolvedValue(undefined),
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

  it('saves entry without analysis when agent.analyze rejects', async () => {
    const services = makeMockServices({
      agent: {
        analyze: vi.fn().mockRejectedValue(new Error('API key invalid')),
      } as unknown as IAgentService,
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
      } as unknown as IAgentService,
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

    expect(result.entryId).toBe('test-uuid-1234');
    expect(result.audioPath).toBeUndefined();
  });
});
