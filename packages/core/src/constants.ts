// LLM defaults
export const DEFAULT_CLOUD_MODEL_ID = 'claude-sonnet-4-6';
export const DEFAULT_LOCAL_MODEL_ID = 'qwen2.5:7b';
export const DEFAULT_OLLAMA_ENDPOINT = 'http://localhost:11434';
export const ANALYSIS_MAX_TOKENS = 2048;

// Embedding defaults
export const DEFAULT_OLLAMA_EMBEDDING_MODEL = 'nomic-embed-text';
export const DEFAULT_CLOUD_EMBEDDING_MODEL = 'voyage-3-lite';
export const EMBEDDING_AVAILABILITY_TIMEOUT_MS = 3_000;
export const EMBEDDING_AVAILABILITY_CACHE_MS = 30_000;
// nomic-embed-text produces 768-dimensional vectors; bge-m3 produces 1024.
// 768 is the default — change if you switch embedding models.
export const EMBEDDING_DIMENSION = 768;

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
// Model: sherpa-onnx-streaming-zipformer-en-2023-06-26 (int8 quantized)
// Download URL: https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/sherpa-onnx-streaming-zipformer-en-2023-06-26.tar.bz2
export const SHERPA_ONNX_MODEL_DIR = 'sherpa-onnx-streaming-zipformer-en-2023-06-26';
export const SHERPA_ONNX_ENCODER_FILENAME = 'encoder-epoch-99-avg-1-chunk-16-left-128.int8.onnx';
export const SHERPA_ONNX_DECODER_FILENAME = 'decoder-epoch-99-avg-1-chunk-16-left-128.onnx';
export const SHERPA_ONNX_JOINER_FILENAME = 'joiner-epoch-99-avg-1-chunk-16-left-128.int8.onnx';
export const SHERPA_ONNX_TOKENS_FILENAME = 'tokens.txt';

// Interval (ms) between polling the sherpa-onnx recognizer for new results
export const SHERPA_ONNX_POLL_INTERVAL_MS = 100;
