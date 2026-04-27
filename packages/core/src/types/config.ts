import { z } from 'zod';

export const LlmConfigSchema = z.discriminatedUnion('provider', [
  z.object({
    provider: z.literal('cloud'),
    apiKey: z.string().min(1),
  }),
  z.object({
    provider: z.literal('local'),
    localEndpoint: z.string().url(),
    modelId: z.string().min(1),
  }),
]);

export const SttConfigSchema = z.object({
  engine: z.string().min(1),
  modelPath: z.string().min(1),
});

export const EmbeddingConfigSchema = z.discriminatedUnion('provider', [
  z.object({
    provider: z.literal('ollama'),
    model: z.string().min(1),
    endpoint: z.string().url(),
  }),
  z.object({
    provider: z.literal('openrouter'),
    model: z.string().min(1),
    apiKey: z.string().min(1),
  }),
  z.object({
    provider: z.literal('custom'),
    model: z.string().min(1),
    endpoint: z.string().url(),
  }),
  z.object({
    provider: z.literal('none'),
    model: z.literal(''),
  }),
]);

export const StorageConfigSchema = z.object({
  dbPath: z.string().min(1),
});

export const LiveTranscriptionConfigSchema = z.discriminatedUnion('provider', [
  z.object({
    provider: z.literal('sherpa'),
    sherpaModelId: z.string().min(1),
  }),
  z.object({
    provider: z.literal('none'),
  }),
]);

export const AppConfigSchema = z.object({
  llm: LlmConfigSchema,
  stt: SttConfigSchema,
  embedding: EmbeddingConfigSchema,
  storage: StorageConfigSchema,
  liveTranscription: LiveTranscriptionConfigSchema.optional(),
});

export type AppConfig = z.infer<typeof AppConfigSchema>;
export type LlmConfig = z.infer<typeof LlmConfigSchema>;
export type SttConfig = z.infer<typeof SttConfigSchema>;
export type EmbeddingConfig = z.infer<typeof EmbeddingConfigSchema>;
export type StorageConfig = z.infer<typeof StorageConfigSchema>;
export type LiveTranscriptionConfig = z.infer<typeof LiveTranscriptionConfigSchema>;
