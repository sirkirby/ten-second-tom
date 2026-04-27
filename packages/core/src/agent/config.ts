import type { LlmConfig } from '../types/config.js';
import { DEFAULT_CLOUD_MODEL_ID } from '../constants.js';

export function getModelId(config: LlmConfig): string {
  if (config.provider === 'cloud') {
    return DEFAULT_CLOUD_MODEL_ID;
  }
  return config.modelId;
}

export function getBaseUrl(config: LlmConfig): string | undefined {
  if (config.provider === 'local') {
    // Return the raw endpoint without /v1 suffix.
    // Ollama exposes a native API at /api/chat, not an Anthropic-compatible one.
    return config.localEndpoint.replace(/\/+$/, '');
  }
  return undefined;
}
