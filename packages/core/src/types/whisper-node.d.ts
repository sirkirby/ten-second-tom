declare module '@fugood/whisper.node' {
  export interface NativeContextOptions {
    filePath: string;
    useFlashAttn?: boolean;
    useGpu?: boolean;
  }

  export interface TranscribeOptions {
    language?: string;
    translate?: boolean;
    maxThreads?: number;
    maxContext?: number;
    maxLen?: number;
    tokenTimestamps?: boolean;
    tdrzEnable?: boolean;
    wordThold?: number;
    offset?: number;
    duration?: number;
    temperature?: number;
    temperatureInc?: number;
    beamSize?: number;
    bestOf?: number;
    prompt?: string;
    nProcessors?: number;
    /** Progress callback — progress is between 0 and 100 */
    onProgress?: (progress: number) => void;
    /** Called when new segments are transcribed */
    onNewSegments?: (result: TranscribeNewSegmentsResult) => void;
  }

  export interface TranscribeNewSegmentsResult {
    nNew: number;
    totalNNew: number;
    result: string;
    segments: TranscribeResult['segments'];
  }

  export interface TranscribeResult {
    language?: string;
    result: string;
    segments: Array<{
      text: string;
      t0: number;
      t1: number;
    }>;
    isAborted: boolean;
  }

  export interface WhisperContext {
    transcribeFile(
      filePath: string,
      options?: TranscribeOptions,
    ): {
      stop: () => Promise<void>;
      promise: Promise<TranscribeResult>;
    };
    transcribeData(
      audioData: ArrayBuffer,
      options?: TranscribeOptions,
    ): {
      stop: () => Promise<void>;
      promise: Promise<TranscribeResult>;
    };
    bench(nThreads: number): Promise<{
      config: string;
      nThreads: number;
      encodeMs: number;
      decodeMs: number;
      batchdMs: number;
      promptMs: number;
    }>;
    release(): Promise<void>;
    getModelInfo(): object;
  }

  export type LibVariant = 'default' | 'vulkan' | 'cuda';

  export function initWhisper(
    options: NativeContextOptions,
    variant?: LibVariant,
  ): Promise<WhisperContext>;

  /** Toggle native ggml/whisper.cpp log output. Call with `false` before initWhisper to suppress. */
  export function toggleNativeLog(enabled: boolean): Promise<void>;
}
