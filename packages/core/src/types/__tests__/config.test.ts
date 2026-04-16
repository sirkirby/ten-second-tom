import { describe, it, expect } from 'vitest';
import { AppConfigSchema } from '../config.js';

describe('AppConfigSchema', () => {
  it('validates a cloud config', () => {
    const config = {
      llm: { provider: 'cloud' as const, apiKey: 'sk-ant-test-key' },
      stt: { engine: 'whisper-distil-en', modelPath: '/Users/test/.tom/models/whisper-distil-en' },
      embedding: {
        provider: 'openrouter' as const,
        model: 'openai/text-embedding-3-small',
        apiKey: 'sk-or-test',
      },
      storage: { dbPath: '/Users/test/.tom/tom.db' },
    };
    const result = AppConfigSchema.safeParse(config);
    expect(result.success).toBe(true);
  });

  it('validates openrouter embedding config', () => {
    const config = {
      llm: { provider: 'cloud' as const, apiKey: 'sk-ant-test-key' },
      stt: { engine: 'whisper-distil-en', modelPath: '/Users/test/.tom/models/whisper-distil-en' },
      embedding: {
        provider: 'openrouter' as const,
        model: 'openai/text-embedding-3-small',
        apiKey: 'sk-or-test',
      },
      storage: { dbPath: '/Users/test/.tom/tom.db' },
    };
    const result = AppConfigSchema.safeParse(config);
    expect(result.success).toBe(true);
  });

  it('validates custom embedding config', () => {
    const config = {
      llm: { provider: 'cloud' as const, apiKey: 'sk-ant-test-key' },
      stt: { engine: 'whisper-distil-en', modelPath: '/Users/test/.tom/models/whisper-distil-en' },
      embedding: {
        provider: 'custom' as const,
        model: 'bge-m3',
        endpoint: 'http://localhost:8080',
      },
      storage: { dbPath: '/Users/test/.tom/tom.db' },
    };
    const result = AppConfigSchema.safeParse(config);
    expect(result.success).toBe(true);
  });

  it('rejects openrouter embedding without apiKey', () => {
    const config = {
      llm: { provider: 'cloud' as const, apiKey: 'sk-ant-test-key' },
      stt: { engine: 'whisper', modelPath: '/tmp/model' },
      embedding: { provider: 'openrouter', model: 'openai/text-embedding-3-small' },
      storage: { dbPath: '/tmp/tom.db' },
    };
    const result = AppConfigSchema.safeParse(config);
    expect(result.success).toBe(false);
  });

  it('rejects custom embedding without endpoint', () => {
    const config = {
      llm: { provider: 'cloud' as const, apiKey: 'sk-ant-test-key' },
      stt: { engine: 'whisper', modelPath: '/tmp/model' },
      embedding: { provider: 'custom', model: 'bge-m3' },
      storage: { dbPath: '/tmp/tom.db' },
    };
    const result = AppConfigSchema.safeParse(config);
    expect(result.success).toBe(false);
  });

  it('rejects removed cloud embedding provider', () => {
    const config = {
      llm: { provider: 'cloud' as const, apiKey: 'sk-ant-test-key' },
      stt: { engine: 'whisper', modelPath: '/tmp/model' },
      embedding: { provider: 'cloud', model: 'voyage-3-lite' },
      storage: { dbPath: '/tmp/tom.db' },
    };
    const result = AppConfigSchema.safeParse(config);
    expect(result.success).toBe(false);
  });

  it('validates a local config with ollama', () => {
    const config = {
      llm: {
        provider: 'local' as const,
        localEndpoint: 'http://localhost:11434',
        modelId: 'qwen2.5:7b',
      },
      stt: { engine: 'whisper-distil-en', modelPath: '/Users/test/.tom/models/whisper-distil-en' },
      embedding: {
        provider: 'ollama' as const,
        model: 'nomic-embed-text',
        endpoint: 'http://localhost:11434',
      },
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

  it('rejects cloud LLM provider without apiKey', () => {
    const config = {
      llm: { provider: 'cloud' },
      stt: { engine: 'whisper', modelPath: '/tmp/model' },
      embedding: { provider: 'none', model: '' },
      storage: { dbPath: '/tmp/tom.db' },
    };
    const result = AppConfigSchema.safeParse(config);
    expect(result.success).toBe(false);
  });

  it('rejects local LLM provider without endpoint', () => {
    const config = {
      llm: { provider: 'local', modelId: 'qwen2.5:7b' },
      stt: { engine: 'whisper', modelPath: '/tmp/model' },
      embedding: { provider: 'none', model: '' },
      storage: { dbPath: '/tmp/tom.db' },
    };
    const result = AppConfigSchema.safeParse(config);
    expect(result.success).toBe(false);
  });

  it('rejects ollama embedding provider without endpoint', () => {
    const config = {
      llm: { provider: 'cloud' as const, apiKey: 'sk-ant-test-key' },
      stt: { engine: 'whisper', modelPath: '/tmp/model' },
      embedding: { provider: 'ollama', model: 'nomic-embed-text' },
      storage: { dbPath: '/tmp/tom.db' },
    };
    const result = AppConfigSchema.safeParse(config);
    expect(result.success).toBe(false);
  });
});
