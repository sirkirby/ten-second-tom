/**
 * Registry of available sherpa-onnx streaming models for live transcription.
 *
 * These models are downloaded from the k2-fsa/sherpa-onnx GitHub releases as
 * .tar.bz2 archives that are extracted after download. They provide real-time
 * streaming speech-to-text during recording (live preview).
 */

const SHERPA_ONNX_BASE_URL = 'https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/';

export interface SherpaModel {
  /** Short identifier (e.g. 'zipformer-en-2023-06-26') */
  id: string;
  /** Directory name after extraction */
  dirName: string;
  /** Archive filename for download */
  archiveFilename: string;
  /** Full download URL */
  url: string;
  /** Approximate download size in bytes */
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
    id: 'zipformer-en-2023-06-26',
    dirName: 'sherpa-onnx-streaming-zipformer-en-2023-06-26',
    archiveFilename: 'sherpa-onnx-streaming-zipformer-en-2023-06-26.tar.bz2',
    url: `${SHERPA_ONNX_BASE_URL}sherpa-onnx-streaming-zipformer-en-2023-06-26.tar.bz2`,
    sizeBytes: 68_000_000,
    sizeLabel: '68 MB',
    description: 'English streaming, good balance',
    recommended: true,
    encoderFilename: 'encoder-epoch-99-avg-1-chunk-16-left-128.int8.onnx',
    decoderFilename: 'decoder-epoch-99-avg-1-chunk-16-left-128.onnx',
    joinerFilename: 'joiner-epoch-99-avg-1-chunk-16-left-128.int8.onnx',
    tokensFilename: 'tokens.txt',
  },
  {
    id: 'zipformer-small-bilingual-zh-en',
    dirName: 'sherpa-onnx-streaming-zipformer-small-bilingual-zh-en-2023-02-16',
    archiveFilename: 'sherpa-onnx-streaming-zipformer-small-bilingual-zh-en-2023-02-16.tar.bz2',
    url: `${SHERPA_ONNX_BASE_URL}sherpa-onnx-streaming-zipformer-small-bilingual-zh-en-2023-02-16.tar.bz2`,
    sizeBytes: 40_000_000,
    sizeLabel: '40 MB',
    description: 'English + Chinese, smaller model',
    recommended: false,
    encoderFilename: 'encoder-epoch-99-avg-1.int8.onnx',
    decoderFilename: 'decoder-epoch-99-avg-1.onnx',
    joinerFilename: 'joiner-epoch-99-avg-1.int8.onnx',
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
