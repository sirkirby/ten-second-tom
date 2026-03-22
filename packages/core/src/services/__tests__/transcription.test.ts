import { describe, it, expect, vi, beforeEach } from 'vitest';
import { PassThrough } from 'node:stream';

// Mock @fugood/whisper.node before importing the module under test
vi.mock('@fugood/whisper.node', () => {
  const mockContext = {
    transcribeFile: vi.fn(),
    transcribeData: vi.fn(),
    release: vi.fn().mockResolvedValue(undefined),
  };

  return {
    initWhisper: vi.fn().mockResolvedValue(mockContext),
    default: {
      initWhisper: vi.fn().mockResolvedValue(mockContext),
    },
    __mockContext: mockContext,
  };
});

// Import after mock is registered
const { WhisperTranscriptionService } = await import('../transcription.js');
const whisperModule = await import('@fugood/whisper.node');
const mockInitWhisper = vi.mocked(whisperModule.initWhisper);
// Access the shared mock context
const mockContext = (whisperModule as unknown as { __mockContext: typeof whisperModule & {
  transcribeFile: ReturnType<typeof vi.fn>;
  transcribeData: ReturnType<typeof vi.fn>;
  release: ReturnType<typeof vi.fn>;
} }).__mockContext;

beforeEach(() => {
  vi.clearAllMocks();
  // Re-setup defaults after clear
  mockContext.release.mockResolvedValue(undefined);
});

describe('WhisperTranscriptionService', () => {
  describe('isModelLoaded()', () => {
    it('returns false before loadModel is called', () => {
      const service = new WhisperTranscriptionService();
      expect(service.isModelLoaded()).toBe(false);
    });
  });

  describe('loadModel()', () => {
    it('loads the whisper model and isModelLoaded() returns true', async () => {
      const service = new WhisperTranscriptionService();

      await service.loadModel('/path/to/ggml-model.bin');

      expect(mockInitWhisper).toHaveBeenCalledOnce();
      expect(mockInitWhisper).toHaveBeenCalledWith({ filePath: '/path/to/ggml-model.bin' });
      expect(service.isModelLoaded()).toBe(true);
    });

    it('releases the previous context before loading a new model', async () => {
      const service = new WhisperTranscriptionService();

      await service.loadModel('/path/to/model1.bin');
      await service.loadModel('/path/to/model2.bin');

      expect(mockContext.release).toHaveBeenCalledOnce();
      expect(mockInitWhisper).toHaveBeenCalledTimes(2);
    });
  });

  describe('transcribeFile()', () => {
    it('throws if model is not loaded', async () => {
      const service = new WhisperTranscriptionService();

      await expect(service.transcribeFile('/audio.wav')).rejects.toThrow(
        'Model not loaded',
      );
    });

    it('returns the transcribed text from a file', async () => {
      const service = new WhisperTranscriptionService();
      await service.loadModel('/model.bin');

      mockContext.transcribeFile.mockReturnValue({
        promise: Promise.resolve({
          result: 'Hello, world!',
          segments: [{ text: 'Hello, world!', t0: 0, t1: 100 }],
          isAborted: false,
        }),
        stop: vi.fn().mockResolvedValue(undefined),
      });

      const result = await service.transcribeFile('/audio.wav');

      expect(mockContext.transcribeFile).toHaveBeenCalledOnce();
      expect(mockContext.transcribeFile).toHaveBeenCalledWith('/audio.wav', expect.any(Object));
      expect(result).toBe('Hello, world!');
    });

    it('concatenates segment texts when result is empty', async () => {
      const service = new WhisperTranscriptionService();
      await service.loadModel('/model.bin');

      mockContext.transcribeFile.mockReturnValue({
        promise: Promise.resolve({
          result: '',
          segments: [
            { text: ' Hello', t0: 0, t1: 50 },
            { text: ' world', t0: 50, t1: 100 },
          ],
          isAborted: false,
        }),
        stop: vi.fn().mockResolvedValue(undefined),
      });

      const result = await service.transcribeFile('/audio.wav');

      expect(result).toBe(' Hello world');
    });
  });

  describe('transcribeStream()', () => {
    it('throws if model is not loaded', async () => {
      const service = new WhisperTranscriptionService();
      const stream = new PassThrough();
      stream.end();

      await expect(service.transcribeStream(stream, () => {})).rejects.toThrow(
        'Model not loaded',
      );
    });

    it('collects audio chunks, transcribes, and calls onChunk with partial text', async () => {
      const service = new WhisperTranscriptionService();
      await service.loadModel('/model.bin');

      const onChunk = vi.fn();

      // Set up transcribeData to call onNewSegments synchronously before returning
      mockContext.transcribeData.mockImplementation(
        (_audioData: ArrayBuffer, options: { onNewSegments?: (result: { result: string }) => void }) => {
          // Call onNewSegments synchronously to simulate a segment arriving
          options?.onNewSegments?.({ result: 'Hello from stream' });
          return {
            promise: Promise.resolve({
              result: 'Hello from stream',
              segments: [{ text: 'Hello from stream', t0: 0, t1: 100 }],
              isAborted: false,
            }),
            stop: vi.fn().mockResolvedValue(undefined),
          };
        },
      );

      const stream = new PassThrough();

      // Write PCM-like data (Int16 samples at 16kHz)
      const pcmData = Buffer.alloc(32000 * 2); // 1 second of 16kHz 16-bit mono
      stream.write(pcmData);
      stream.end();

      const result = await service.transcribeStream(stream, onChunk);

      expect(mockContext.transcribeData).toHaveBeenCalledOnce();
      expect(onChunk).toHaveBeenCalledWith('Hello from stream');
      expect(result).toBe('Hello from stream');
    });

    it('returns an empty string when the stream has no audio data', async () => {
      const service = new WhisperTranscriptionService();
      await service.loadModel('/model.bin');

      const onChunk = vi.fn();
      const stream = new PassThrough();
      stream.end(); // End immediately with no data

      const result = await service.transcribeStream(stream, onChunk);

      // No audio to transcribe — should not call transcribeData
      expect(mockContext.transcribeData).not.toHaveBeenCalled();
      expect(onChunk).not.toHaveBeenCalled();
      expect(result).toBe('');
    });

    it('returns full accumulated transcript when stream ends', async () => {
      const service = new WhisperTranscriptionService();
      await service.loadModel('/model.bin');

      mockContext.transcribeData.mockReturnValue({
        promise: Promise.resolve({
          result: 'Full transcript text',
          segments: [{ text: 'Full transcript text', t0: 0, t1: 500 }],
          isAborted: false,
        }),
        stop: vi.fn().mockResolvedValue(undefined),
      });

      const stream = new PassThrough();
      const pcmData = Buffer.alloc(32000 * 2); // 1 second of audio
      stream.write(pcmData);
      stream.end();

      const result = await service.transcribeStream(stream, () => {});

      expect(result).toBe('Full transcript text');
    });
  });
});
