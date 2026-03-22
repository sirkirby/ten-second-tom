export interface AgentConfig {
  provider: 'cloud' | 'local';
  apiKey?: string;
  localEndpoint?: string;
  modelId?: string;
}

export function getModelId(config: AgentConfig): string {
  if (config.provider === 'cloud') {
    return 'claude-sonnet-4-6';
  }
  return config.modelId ?? 'qwen2.5:7b';
}

export function getBaseUrl(config: AgentConfig): string | undefined {
  if (config.provider === 'local') {
    return config.localEndpoint ?? 'http://localhost:11434/v1';
  }
  return undefined;
}
