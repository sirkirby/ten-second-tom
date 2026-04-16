// LLM defaults
export const DEFAULT_CLOUD_MODEL_ID = 'claude-sonnet-4-6';
export const DEFAULT_LOCAL_MODEL_ID = 'qwen2.5:7b';
export const DEFAULT_OLLAMA_ENDPOINT = 'http://localhost:11434';
export const ANALYSIS_MAX_TOKENS = 2048;

// Embedding defaults
export const DEFAULT_OLLAMA_EMBEDDING_MODEL = 'nomic-embed-text';
export const OPENROUTER_BASE_URL = 'https://openrouter.ai/api/v1';
export const DEFAULT_OPENROUTER_EMBEDDING_MODEL = 'openai/text-embedding-3-small';
export const EMBEDDING_AVAILABILITY_TIMEOUT_MS = 3_000;
export const EMBEDDING_AVAILABILITY_CACHE_MS = 30_000;
export const DEFAULT_EMBEDDING_DIMENSION = 768;

/**
 * Known embedding model dimensions. Used to determine the vec0 table column
 * size without needing to call the embedding provider at startup. Keys are
 * matched against the start of the configured model name (case-insensitive)
 * so that tagged variants like "bge-m3:latest" resolve correctly.
 */
export const EMBEDDING_MODEL_DIMENSIONS: Record<string, number> = {
  'nomic-embed-text': 768,
  'bge-m3': 1024,
  'mxbai-embed-large': 1024,
  'all-minilm': 384,
  'snowflake-arctic-embed': 1024,
  'qwen3-embedding': 1536,
  'jina-embeddings': 768,
  'voyage-3-lite': 512,
  'openai/text-embedding-3-small': 1536,
  'openai/text-embedding-3-large': 3072,
  'openai/text-embedding-ada-002': 1536,
};

/**
 * Look up the embedding dimension for a model name. Handles Ollama-style
 * tagged names (e.g. "bge-m3:latest") by matching the prefix before the colon.
 * Falls back to DEFAULT_EMBEDDING_DIMENSION if the model is not recognized.
 */
export function getEmbeddingDimension(modelName: string): number {
  const lower = modelName.toLowerCase();
  // Try exact match first
  if (EMBEDDING_MODEL_DIMENSIONS[lower] !== undefined) {
    return EMBEDDING_MODEL_DIMENSIONS[lower];
  }
  // Try prefix match (strip ":tag" suffix for Ollama models like "bge-m3:latest")
  const base = lower.split(':')[0];
  if (base && EMBEDDING_MODEL_DIMENSIONS[base] !== undefined) {
    return EMBEDDING_MODEL_DIMENSIONS[base];
  }
  return DEFAULT_EMBEDDING_DIMENSION;
}

// Audio
export const AUDIO_SAMPLE_RATE = 16000;
export const AUDIO_CHANNELS = 1;
export const AUDIO_BITS_PER_SAMPLE = 16;
export const MAX_AUDIO_BUFFER_BYTES = 100 * 1024 * 1024; // ~55 min at 16kHz mono 16-bit

// Anthropic
export const ANTHROPIC_API_KEY_PREFIX = 'sk-ant-';

// Whisper
export const WHISPER_MODEL_FILENAME = 'ggml-distil-small.en.bin';

// Live transcription chunking — each chunk sent to Whisper for incremental transcript
// 5 seconds of 16kHz mono 16-bit PCM = 5 * 16000 * 2 = 160,000 bytes
export const LIVE_TRANSCRIPTION_CHUNK_SEC = 5;
export const LIVE_TRANSCRIPTION_CHUNK_BYTES =
  LIVE_TRANSCRIPTION_CHUNK_SEC * AUDIO_SAMPLE_RATE * (AUDIO_BITS_PER_SAMPLE / 8) * AUDIO_CHANNELS;

// sherpa-onnx streaming model (used for live preview during recording)
// Model: sherpa-onnx-streaming-zipformer-en-kroko-2025-08-06
// Downloaded as individual files from HuggingFace (not a tar.bz2 archive)
export const SHERPA_ONNX_MODEL_DIR = 'sherpa-onnx-streaming-zipformer-en-kroko-2025-08-06';
export const SHERPA_ONNX_ENCODER_FILENAME = 'encoder.onnx';
export const SHERPA_ONNX_DECODER_FILENAME = 'decoder.onnx';
export const SHERPA_ONNX_JOINER_FILENAME = 'joiner.onnx';
export const SHERPA_ONNX_TOKENS_FILENAME = 'tokens.txt';

// Interval (ms) between polling the sherpa-onnx recognizer for new results
export const SHERPA_ONNX_POLL_INTERVAL_MS = 100;
