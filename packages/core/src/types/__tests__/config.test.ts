import { describe, it, expect } from 'vitest';
import { AppConfigSchema } from '../config.js';

describe('AppConfigSchema', () => {
  it('validates a cloud config', () => {
    const config = {
      llm: { provider: 'cloud' as const, apiKey: 'sk-ant-test-key' },
      stt: { engine: 'whisper-distil-en', modelPath: '/Users/test/.tom/models/whisper-distil-en' },
      embedding: { provider: 'cloud' as const, model: 'voyage-3-lite' },
      storage: { dbPath: '/Users/test/.tom/tom.db' },
    };
    const result = AppConfigSchema.safeParse(config);
    expect(result.success).toBe(true);
  });

  it('validates a local config with ollama', () => {
    const config = {
      llm: { provider: 'local' as const, localEndpoint: 'http://localhost:11434', modelId: 'qwen2.5:7b' },
      stt: { engine: 'whisper-distil-en', modelPath: '/Users/test/.tom/models/whisper-distil-en' },
      embedding: { provider: 'ollama' as const, model: 'nomic-embed-text', endpoint: 'http://localhost:11434' },
      storage: { dbPath: '/Users/test/.tom/tom.db' },
    };
    const result = AppConfigSchema.safeParse(config);
    expect(result.success).toBe(true);
  });

  it('validates config with no embedding provider', () => {
    const config = {
      llm: { provider: 'cloud' as const, apiKey: 'sk-ant-test-key' },
      stt: { engine: 'whisper-distil-en', modelPath: '/Users/test/.tom/models/whisper-distil-en' },
      embedding: { provider: 'none' as const, model: '' },
      storage: { dbPath: '/Users/test/.tom/tom.db' },
    };
    const result = AppConfigSchema.safeParse(config);
    expect(result.success).toBe(true);
  });

  it('rejects invalid LLM provider', () => {
    const config = {
      llm: { provider: 'openai' },
      stt: { engine: 'whisper', modelPath: '/tmp/model' },
      embedding: { provider: 'none', model: '' },
      storage: { dbPath: '/tmp/tom.db' },
    };
    const result = AppConfigSchema.safeParse(config);
    expect(result.success).toBe(false);
  });
});
