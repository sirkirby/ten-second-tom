declare module 'node-record-lpcm16' {
  import type { Readable } from 'node:stream';

  interface RecordOptions {
    sampleRate?: number;
    channels?: number;
    threshold?: number;
    silence?: string;
    recorder?: string;
    device?: string | null;
    audioType?: string;
  }

  interface Recording {
    stream(): Readable;
    stop(): void;
    pause(): void;
    resume(): void;
  }

  function record(options?: RecordOptions): Recording;
  export { record, Recording, RecordOptions };
}
