/**
 * Registry of available Whisper GGML models for speech-to-text.
 *
 * All models are downloaded from the ggerganov/whisper.cpp HuggingFace repo.
 * The recommended default is distil-small.en — best accuracy/speed/size
 * tradeoff for English speech.
 */

const WHISPER_HF_BASE_URL = 'https://huggingface.co/ggerganov/whisper.cpp/resolve/main/';

export interface WhisperModel {
  /** Short identifier (e.g. 'distil-small.en') */
  id: string;
  /** Full filename on disk (e.g. 'ggml-distil-small.en.bin') */
  filename: string;
  /** Full download URL */
  url: string;
  /** Approximate download size in bytes */
  sizeBytes: number;
  /** Human-readable size label (e.g. '380 MB') */
  sizeLabel: string;
  /** Short description for the setup wizard */
  description: string;
  /** Whether this is the recommended default */
  recommended: boolean;
}

export const WHISPER_MODELS: WhisperModel[] = [
  {
    id: 'distil-small.en',
    filename: 'ggml-distil-small.en.bin',
    url: `${WHISPER_HF_BASE_URL}ggml-distil-small.en.bin`,
    sizeBytes: 380_000_000,
    sizeLabel: '380 MB',
    description: 'English, fast, good accuracy',
    recommended: true,
  },
  {
    id: 'base.en',
    filename: 'ggml-base.en.bin',
    url: `${WHISPER_HF_BASE_URL}ggml-base.en.bin`,
    sizeBytes: 142_000_000,
    sizeLabel: '142 MB',
    description: 'English, very fast, lower accuracy',
    recommended: false,
  },
  {
    id: 'small.en',
    filename: 'ggml-small.en.bin',
    url: `${WHISPER_HF_BASE_URL}ggml-small.en.bin`,
    sizeBytes: 466_000_000,
    sizeLabel: '466 MB',
    description: 'English, moderate speed, good accuracy',
    recommended: false,
  },
  {
    id: 'medium.en',
    filename: 'ggml-medium.en.bin',
    url: `${WHISPER_HF_BASE_URL}ggml-medium.en.bin`,
    sizeBytes: 1_500_000_000,
    sizeLabel: '1.5 GB',
    description: 'English, slow, higher accuracy',
    recommended: false,
  },
  {
    id: 'tiny.en',
    filename: 'ggml-tiny.en.bin',
    url: `${WHISPER_HF_BASE_URL}ggml-tiny.en.bin`,
    sizeBytes: 75_000_000,
    sizeLabel: '75 MB',
    description: 'English, fastest, lowest accuracy',
    recommended: false,
  },
  {
    id: 'large-v3',
    filename: 'ggml-large-v3.bin',
    url: `${WHISPER_HF_BASE_URL}ggml-large-v3.bin`,
    sizeBytes: 3_000_000_000,
    sizeLabel: '3 GB',
    description: 'Multilingual, best accuracy, slowest',
    recommended: false,
  },
];

/** Returns the recommended Whisper model. */
export function getDefaultWhisperModel(): WhisperModel {
  const model = WHISPER_MODELS.find((m) => m.recommended);
  if (!model) throw new Error('No recommended Whisper model defined');
  return model;
}

/** Find a Whisper model by id, or undefined if not found. */
export function findWhisperModel(id: string): WhisperModel | undefined {
  return WHISPER_MODELS.find((m) => m.id === id);
}
