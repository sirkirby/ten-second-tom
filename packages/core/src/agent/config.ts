import type { LlmConfig } from '../types/config.js';
import {
  DEFAULT_CLOUD_MODEL_ID,
  DEFAULT_LOCAL_MODEL_ID,
  DEFAULT_OLLAMA_ENDPOINT,
} from '../constants.js';

export function getModelId(config: LlmConfig): string {
  if (config.provider === 'cloud') {
    return DEFAULT_CLOUD_MODEL_ID;
  }
  return config.modelId ?? DEFAULT_LOCAL_MODEL_ID;
}

export function getBaseUrl(config: LlmConfig): string | undefined {
  if (config.provider === 'local') {
    const endpoint = config.localEndpoint ?? DEFAULT_OLLAMA_ENDPOINT;
    // Return the raw endpoint without /v1 suffix.
    // Ollama exposes a native API at /api/chat, not an Anthropic-compatible one.
    return endpoint.replace(/\/+$/, '');
  }
  return undefined;
}
