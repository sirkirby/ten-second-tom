import { z } from 'zod';

export const LlmConfigSchema = z.object({
  provider: z.enum(['cloud', 'local']),
  apiKey: z.string().optional(),
  localEndpoint: z.string().url().optional(),
  modelId: z.string().optional(),
});

export const SttConfigSchema = z.object({
  engine: z.string().min(1),
  modelPath: z.string().min(1),
});

export const EmbeddingConfigSchema = z.object({
  provider: z.enum(['ollama', 'cloud', 'none']),
  model: z.string(),
  endpoint: z.string().url().optional(),
});

export const StorageConfigSchema = z.object({
  dbPath: z.string().min(1),
});

export const AppConfigSchema = z.object({
  llm: LlmConfigSchema,
  stt: SttConfigSchema,
  embedding: EmbeddingConfigSchema,
  storage: StorageConfigSchema,
});

export type AppConfig = z.infer<typeof AppConfigSchema>;
export type LlmConfig = z.infer<typeof LlmConfigSchema>;
export type SttConfig = z.infer<typeof SttConfigSchema>;
export type EmbeddingConfig = z.infer<typeof EmbeddingConfigSchema>;
