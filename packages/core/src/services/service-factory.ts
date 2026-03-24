import type { AppConfig } from '../types/config.js';
import type { ConfigManager } from '../config/config-manager.js';
import type { IAudioService } from './audio.js';
import type { ITranscriptionService } from './transcription.js';
import type { ILiveTranscriptionService } from './live-transcription.js';
import type { IAgentService } from '../agent/tom-agent.js';
import type { IEmbeddingService } from './embedding.js';
import type { IStorageService } from './storage.js';
import type { ISearchService } from './search.js';
import { AudioService } from './audio.js';
import { WhisperTranscriptionService } from './transcription.js';
import {
  SherpaOnnxLiveTranscriptionService,
  NoopLiveTranscriptionService,
} from './live-transcription.js';
import { TomAgent } from '../agent/tom-agent.js';
import { OllamaEmbeddingService, NoopEmbeddingService, OpenAICompatibleEmbeddingService } from './embedding.js';
import { SqliteStorageService } from './storage-sqlite.js';
import { SearchService } from './search.js';
import { getEmbeddingDimension, OPENROUTER_BASE_URL } from '../constants.js';

export interface ServiceContainer {
  audio: IAudioService;
  transcription: ITranscriptionService;
  liveTranscription: ILiveTranscriptionService;
  agent: IAgentService;
  embedding: IEmbeddingService;
  storage: IStorageService;
  search: ISearchService;
}

/**
 * Build services from a loaded AppConfig + ConfigManager.
 * This is the single factory for constructing the service graph from configuration.
 */
export function buildServicesFromConfig(
  config: AppConfig,
  configManager: ConfigManager,
): ServiceContainer {
  const audio = new AudioService({ audioDir: configManager.audioPath });

  const transcription = new WhisperTranscriptionService();

  // Live transcription (sherpa-onnx) — degrades gracefully to noop if model not available
  const sherpaLive = new SherpaOnnxLiveTranscriptionService({
    modelsPath: configManager.modelsPath,
  });
  const liveTranscription: ILiveTranscriptionService = sherpaLive.isAvailable()
    ? sherpaLive
    : new NoopLiveTranscriptionService();

  const agent = new TomAgent(config.llm);

  const embedding =
    config.embedding.provider === 'ollama'
      ? new OllamaEmbeddingService({
          model: config.embedding.model,
          endpoint: config.embedding.endpoint,
        })
      : config.embedding.provider === 'openrouter'
        ? new OpenAICompatibleEmbeddingService({
            baseUrl: OPENROUTER_BASE_URL,
            model: config.embedding.model,
            apiKey: config.embedding.apiKey,
          })
        : config.embedding.provider === 'custom'
          ? new OpenAICompatibleEmbeddingService({
              baseUrl: config.embedding.endpoint,
              model: config.embedding.model,
            })
          : new NoopEmbeddingService();

  // Derive the embedding dimension from the configured model name so the
  // vec0 table is created (or recreated) with the correct column size.
  const embeddingModel = config.embedding.provider !== 'none' ? config.embedding.model : '';
  const embeddingDimension = embeddingModel ? getEmbeddingDimension(embeddingModel) : undefined;

  const storage = new SqliteStorageService(config.storage.dbPath, embeddingDimension);

  const search = new SearchService(storage, embedding);

  return { audio, transcription, liveTranscription, agent, embedding, storage, search };
}
