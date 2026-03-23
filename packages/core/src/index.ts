// Core package barrel export

export * from './constants.js';
export * from './types/index.js';
export { type IStorageService, type ListEntriesOptions } from './services/storage.js';
export { SqliteStorageService } from './services/storage-sqlite.js';
export { ConfigManager } from './config/config-manager.js';
export { type IAgentService, TomAgent } from './agent/tom-agent.js';
export { getModelId, getBaseUrl } from './agent/config.js';
export {
  type IEmbeddingService,
  OllamaEmbeddingService,
  NoopEmbeddingService,
  type OllamaEmbeddingConfig,
} from './services/embedding.js';
export { type ISearchService, SearchService } from './services/search.js';
export {
  type IAudioService,
  type AudioServiceConfig,
  type AudioPrerequisiteResult,
  AudioService,
  checkAudioPrerequisites,
  checkModelExists,
  createWavHeader,
} from './services/audio.js';
export {
  type ITranscriptionService,
  WhisperTranscriptionService,
} from './services/transcription.js';
export { type ServiceContainer, buildServicesFromConfig } from './services/service-factory.js';
