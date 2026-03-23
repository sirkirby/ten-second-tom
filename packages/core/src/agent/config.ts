import type { LlmConfig } from '../types/config.js';

/**
 * @deprecated Use LlmConfig from types/config.js directly.
 * Kept as a type alias for backward compatibility with external consumers.
 */
export type AgentConfig = LlmConfig;

export function getModelId(config: LlmConfig): string {
  if (config.provider === 'cloud') {
    return 'claude-sonnet-4-6';
  }
  return config.modelId ?? 'qwen2.5:7b';
}

export function getBaseUrl(config: LlmConfig): string | undefined {
  if (config.provider === 'local') {
    const endpoint = config.localEndpoint ?? 'http://localhost:11434';
    // Return the raw endpoint without /v1 suffix.
    // Ollama exposes a native API at /api/chat, not an Anthropic-compatible one.
    return endpoint.replace(/\/+$/, '');
  }
  return undefined;
}
