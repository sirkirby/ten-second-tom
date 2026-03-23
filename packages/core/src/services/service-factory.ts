import type { AppConfig } from '../types/config.js';
import type { ConfigManager } from '../config/config-manager.js';
import type { IAudioService } from './audio.js';
import type { ITranscriptionService } from './transcription.js';
import type { IAgentService } from '../agent/tom-agent.js';
import type { IEmbeddingService } from './embedding.js';
import type { IStorageService } from './storage.js';
import { AudioService } from './audio.js';
import { WhisperTranscriptionService } from './transcription.js';
import { TomAgent } from '../agent/tom-agent.js';
import { OllamaEmbeddingService, NoopEmbeddingService } from './embedding.js';
import { SqliteStorageService } from './storage-sqlite.js';

export interface ServiceContainer {
  audio: IAudioService;
  transcription: ITranscriptionService;
  agent: IAgentService;
  embedding: IEmbeddingService;
  storage: IStorageService;
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

  const agent = new TomAgent(config.llm);

  const embedding =
    config.embedding.provider === 'ollama'
      ? new OllamaEmbeddingService({
          model: config.embedding.model,
          endpoint: config.embedding.endpoint,
        })
      : config.embedding.provider === 'cloud'
        ? // Cloud embedding not yet implemented — fall back to noop
          new NoopEmbeddingService()
        : new NoopEmbeddingService();

  const storage = new SqliteStorageService(config.storage.dbPath);

  return { audio, transcription, agent, embedding, storage };
}
