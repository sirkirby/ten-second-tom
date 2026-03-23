/**
 * Registry of available sherpa-onnx streaming models for live transcription.
 *
 * Models are downloaded from HuggingFace as individual files (encoder, decoder,
 * joiner, tokens) into a model directory under ~/.tom/models/.
 */

const SHERPA_ONNX_HF_BASE_URL =
  'https://huggingface.co/csukuangfj/sherpa-onnx-streaming-zipformer-en-kroko-2025-08-06/resolve/main/';

export interface SherpaModelFile {
  /** Filename within the model directory */
  filename: string;
  /** Full download URL */
  url: string;
}

export interface SherpaModel {
  /** Short identifier (e.g. 'zipformer-en-kroko-2025-08-06') */
  id: string;
  /** Directory name after download */
  dirName: string;
  /** Individual files to download */
  files: SherpaModelFile[];
  /** Approximate total download size in bytes */
  sizeBytes: number;
  /** Human-readable size label */
  sizeLabel: string;
  /** Short description for the setup wizard */
  description: string;
  /** Whether this is the recommended default */
  recommended: boolean;
  /** Encoder filename within the model directory */
  encoderFilename: string;
  /** Decoder filename within the model directory */
  decoderFilename: string;
  /** Joiner filename within the model directory */
  joinerFilename: string;
  /** Tokens filename within the model directory */
  tokensFilename: string;
}

export const SHERPA_MODELS: SherpaModel[] = [
  {
    id: 'zipformer-en-kroko-2025-08-06',
    dirName: 'sherpa-onnx-streaming-zipformer-en-kroko-2025-08-06',
    files: [
      {
        filename: 'encoder.onnx',
        url: `${SHERPA_ONNX_HF_BASE_URL}encoder.onnx`,
      },
      {
        filename: 'decoder.onnx',
        url: `${SHERPA_ONNX_HF_BASE_URL}decoder.onnx`,
      },
      {
        filename: 'joiner.onnx',
        url: `${SHERPA_ONNX_HF_BASE_URL}joiner.onnx`,
      },
      {
        filename: 'tokens.txt',
        url: `${SHERPA_ONNX_HF_BASE_URL}tokens.txt`,
      },
    ],
    sizeBytes: 67_000_000,
    sizeLabel: '67 MB',
    description: 'English streaming, optimized for sherpa-onnx-node v1.12+',
    recommended: true,
    encoderFilename: 'encoder.onnx',
    decoderFilename: 'decoder.onnx',
    joinerFilename: 'joiner.onnx',
    tokensFilename: 'tokens.txt',
  },
];

/** Returns the recommended sherpa-onnx model. */
export function getDefaultSherpaModel(): SherpaModel {
  const model = SHERPA_MODELS.find((m) => m.recommended);
  if (!model) throw new Error('No recommended sherpa-onnx model defined');
  return model;
}

/** Find a sherpa-onnx model by id, or undefined if not found. */
export function findSherpaModel(id: string): SherpaModel | undefined {
  return SHERPA_MODELS.find((m) => m.id === id);
}
