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
    toggleNativeLog: vi.fn().mockResolvedValue(undefined),
    default: {
      initWhisper: vi.fn().mockResolvedValue(mockContext),
      toggleNativeLog: vi.fn().mockResolvedValue(undefined),
    },
    __mockContext: mockContext,
  };
});

// Import after mock is registered
const { WhisperTranscriptionService } = await import('../transcription.js');
const whisperModule = await import('@fugood/whisper.node');
const mockInitWhisper = vi.mocked(whisperModule.initWhisper);
const mockToggleNativeLog = vi.mocked(whisperModule.toggleNativeLog);
// Access the shared mock context
const mockContext = (
  whisperModule as unknown as {
    __mockContext: typeof whisperModule & {
      transcribeFile: ReturnType<typeof vi.fn>;
      transcribeData: ReturnType<typeof vi.fn>;
      release: ReturnType<typeof vi.fn>;
    };
  }
).__mockContext;

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

    it('disables native logging before initialising whisper', async () => {
      const service = new WhisperTranscriptionService();

      await service.loadModel('/path/to/ggml-model.bin');

      expect(mockToggleNativeLog).toHaveBeenCalledWith(false);
      // toggleNativeLog must be called before initWhisper
      const logOrder = mockToggleNativeLog.mock.invocationCallOrder[0];
      const initOrder = mockInitWhisper.mock.invocationCallOrder[0];
      expect(logOrder).toBeDefined();
      expect(initOrder).toBeDefined();
      expect(logOrder).toBeLessThan(initOrder as number);
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

      await expect(service.transcribeFile('/audio.wav')).rejects.toThrow('Model not loaded');
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

      await expect(service.transcribeStream(stream, () => {})).rejects.toThrow('Model not loaded');
    });

    it('collects audio chunks, transcribes, and calls onChunk with partial text', async () => {
      const service = new WhisperTranscriptionService();
      await service.loadModel('/model.bin');

      const onChunk = vi.fn();

      // Set up transcribeData to call onNewSegments synchronously before returning
      mockContext.transcribeData.mockImplementation(
        (
          _audioData: ArrayBuffer,
          options: { onNewSegments?: (result: { result: string }) => void },
        ) => {
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

  describe('release()', () => {
    it('releases the whisper context and isModelLoaded() returns false', async () => {
      const service = new WhisperTranscriptionService();
      await service.loadModel('/model.bin');

      expect(service.isModelLoaded()).toBe(true);

      await service.release();

      expect(mockContext.release).toHaveBeenCalledOnce();
      expect(service.isModelLoaded()).toBe(false);
    });

    it('is safe to call when no model is loaded', async () => {
      const service = new WhisperTranscriptionService();

      await expect(service.release()).resolves.toBeUndefined();
      expect(mockContext.release).not.toHaveBeenCalled();
    });
  });

  describe('startLiveTranscription() / stopLiveTranscription()', () => {
    it('throws if model is not loaded', () => {
      const service = new WhisperTranscriptionService();
      const stream = new PassThrough();

      expect(() => service.startLiveTranscription(stream, () => {})).toThrow('Model not loaded');
    });

    it('throws if a live session is already active', async () => {
      const service = new WhisperTranscriptionService();
      await service.loadModel('/model.bin');

      const stream = new PassThrough();
      service.startLiveTranscription(stream, () => {});

      expect(() => service.startLiveTranscription(stream, () => {})).toThrow(
        'Live transcription already active',
      );

      // Clean up
      await service.stopLiveTranscription();
    });

    it('throws stopLiveTranscription if no active session', async () => {
      const service = new WhisperTranscriptionService();
      await service.loadModel('/model.bin');

      await expect(service.stopLiveTranscription()).rejects.toThrow(
        'No active live transcription session',
      );
    });

    it('transcribes a full chunk when enough audio arrives and calls onChunk', async () => {
      const service = new WhisperTranscriptionService();
      await service.loadModel('/model.bin');

      const onChunk = vi.fn();

      mockContext.transcribeData.mockImplementation(
        (
          _audioData: ArrayBuffer,
          options: { onNewSegments?: (result: { result: string }) => void },
        ) => {
          options?.onNewSegments?.({ result: 'Live segment' });
          return {
            promise: Promise.resolve({
              result: 'Live segment',
              segments: [{ text: 'Live segment', t0: 0, t1: 500 }],
              isAborted: false,
            }),
            stop: vi.fn().mockResolvedValue(undefined),
          };
        },
      );

      const stream = new PassThrough();
      service.startLiveTranscription(stream, onChunk);

      // Write more than LIVE_TRANSCRIPTION_CHUNK_BYTES (5s = 160000 bytes) to trigger chunking
      const chunkBytes = 5 * 16000 * 2; // 160000 bytes
      stream.write(Buffer.alloc(chunkBytes));

      // Wait for the async transcription to complete
      const result = await service.stopLiveTranscription();

      expect(mockContext.transcribeData).toHaveBeenCalled();
      expect(onChunk).toHaveBeenCalledWith('Live segment');
      expect(result).toContain('Live segment');
    });

    it('flushes partial audio buffer on stop and returns accumulated transcript', async () => {
      const service = new WhisperTranscriptionService();
      await service.loadModel('/model.bin');

      const onChunk = vi.fn();

      mockContext.transcribeData.mockImplementation(
        (_audioData: ArrayBuffer, options: { onNewSegments?: (r: { result: string }) => void }) => {
          options?.onNewSegments?.({ result: 'Partial' });
          return {
            promise: Promise.resolve({
              result: 'Partial',
              segments: [{ text: 'Partial', t0: 0, t1: 100 }],
              isAborted: false,
            }),
            stop: vi.fn().mockResolvedValue(undefined),
          };
        },
      );

      const stream = new PassThrough();
      service.startLiveTranscription(stream, onChunk);

      // Write less than a full chunk (won't trigger automatic transcription)
      stream.write(Buffer.alloc(1000));

      // stopLiveTranscription should flush the remaining buffer
      const result = await service.stopLiveTranscription();

      expect(mockContext.transcribeData).toHaveBeenCalledOnce();
      expect(result).toBe('Partial');
    });

    it('returns empty string when no audio was captured', async () => {
      const service = new WhisperTranscriptionService();
      await service.loadModel('/model.bin');

      const stream = new PassThrough();
      service.startLiveTranscription(stream, () => {});

      // Don't write any data — immediate stop
      const result = await service.stopLiveTranscription();

      expect(mockContext.transcribeData).not.toHaveBeenCalled();
      expect(result).toBe('');
    });

    it('serialises chunks — does not call transcribeData concurrently', async () => {
      const service = new WhisperTranscriptionService();
      await service.loadModel('/model.bin');

      let concurrentCalls = 0;
      let maxConcurrent = 0;

      mockContext.transcribeData.mockImplementation(() => {
        concurrentCalls++;
        maxConcurrent = Math.max(maxConcurrent, concurrentCalls);
        return {
          promise: Promise.resolve({
            result: 'chunk',
            segments: [],
            isAborted: false,
          }).then((r) => {
            concurrentCalls--;
            return r;
          }),
          stop: vi.fn().mockResolvedValue(undefined),
        };
      });

      const stream = new PassThrough();
      service.startLiveTranscription(stream, () => {});

      // Write 3 full chunks at once
      const chunkBytes = 5 * 16000 * 2; // 160000 bytes each
      stream.write(Buffer.alloc(chunkBytes * 3));

      await service.stopLiveTranscription();

      // Chunks should have been serialised — max concurrent calls should be 1
      expect(maxConcurrent).toBe(1);
    });
  });
});
