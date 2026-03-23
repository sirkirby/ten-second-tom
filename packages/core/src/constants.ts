// LLM defaults
export const DEFAULT_CLOUD_MODEL_ID = 'claude-sonnet-4-6';
export const DEFAULT_LOCAL_MODEL_ID = 'qwen2.5:7b';
export const DEFAULT_OLLAMA_ENDPOINT = 'http://localhost:11434';
export const ANALYSIS_MAX_TOKENS = 1024;

// Embedding defaults
export const DEFAULT_OLLAMA_EMBEDDING_MODEL = 'nomic-embed-text';
export const DEFAULT_CLOUD_EMBEDDING_MODEL = 'voyage-3-lite';
export const EMBEDDING_AVAILABILITY_TIMEOUT_MS = 3_000;
export const EMBEDDING_AVAILABILITY_CACHE_MS = 30_000;

// Audio
export const AUDIO_SAMPLE_RATE = 16000;
export const AUDIO_CHANNELS = 1;
export const AUDIO_BITS_PER_SAMPLE = 16;
export const MAX_AUDIO_BUFFER_BYTES = 100 * 1024 * 1024; // ~55 min at 16kHz mono 16-bit

// Anthropic
export const ANTHROPIC_API_KEY_PREFIX = 'sk-ant-';

// Whisper
export const WHISPER_MODEL_FILENAME = 'ggml-distil-small.en.bin';
