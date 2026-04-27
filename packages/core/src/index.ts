// Core package barrel export

export * from './constants.js';
export * from './types/index.js';
export * from './models/index.js';
export { type IStorageService, type ListEntriesOptions } from './services/storage.js';
export { SqliteStorageService } from './services/storage-sqlite.js';
export { ConfigManager } from './config/config-manager.js';
export { type IAgentService, TomAgent } from './agent/tom-agent.js';
export { getModelId, getBaseUrl } from './agent/config.js';
export {
  type IEmbeddingService,
  OllamaEmbeddingService,
  OpenAICompatibleEmbeddingService,
  NoopEmbeddingService,
  type OllamaEmbeddingConfig,
  type OpenAICompatibleEmbeddingConfig,
} from './services/embedding.js';
export { type ISearchService, type SearchResult, SearchService } from './services/search.js';
export {
  type IAudioService,
  type AudioServiceConfig,
  type AudioPrerequisiteResult,
  AudioService,
  checkAudioPrerequisites,
  checkModelExists,
  createWavHeader,
  getMicrophonePermissionHint,
} from './services/audio.js';
export {
  type ITranscriptionService,
  WhisperTranscriptionService,
} from './services/transcription.js';
export {
  type ILiveTranscriptionService,
  SherpaOnnxLiveTranscriptionService,
  NoopLiveTranscriptionService,
  type SherpaOnnxLiveTranscriptionConfig,
} from './services/live-transcription.js';
export { type ServiceContainer, buildServicesFromConfig } from './services/service-factory.js';
export {
  runAnalysisPipeline,
  reanalyzeEntry,
  type PipelineOptions,
  type PipelineResult,
  type ReanalysisResult,
} from './services/pipeline.js';
export {
  reindexEntries,
  type ReindexOptions,
  type ReindexProgress,
  type ReindexResult,
} from './services/reindex.js';
export {
  buildSetupConfig,
  downloadModel,
  extractTarBz2,
  fetchOllamaModels,
  type BuildSetupConfigOptions,
  type OllamaModel,
} from './services/setup.js';
export { int16BufferToFloat32 } from './services/audio-utils.js';
