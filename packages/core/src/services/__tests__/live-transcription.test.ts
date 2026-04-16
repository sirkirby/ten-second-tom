import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { PassThrough } from 'node:stream';
import type {
  SherpaOnnxOnlineRecognizer,
  SherpaOnnxOnlineStream,
  CreateRecognizerFn,
} from '../live-transcription.js';

// ---------------------------------------------------------------------------
// Mock fs.existsSync to control model availability
// ---------------------------------------------------------------------------

const mockExistsSync = vi.hoisted(() => vi.fn(() => true));
vi.mock('node:fs', async (importOriginal) => {
  const actual = await importOriginal<Record<string, unknown>>();
  return {
    ...actual,
    existsSync: mockExistsSync,
  };
});

// Import after mocks are registered
const { SherpaOnnxLiveTranscriptionService, NoopLiveTranscriptionService } =
  await import('../live-transcription.js');

// ---------------------------------------------------------------------------
// Helpers: mock recognizer and stream
// ---------------------------------------------------------------------------

function makeMockStream(): SherpaOnnxOnlineStream {
  return {
    acceptWaveform: vi.fn(),
    inputFinished: vi.fn(),
    free: vi.fn(),
  };
}

function makeMockRecognizer(stream: SherpaOnnxOnlineStream): SherpaOnnxOnlineRecognizer {
  return {
    createStream: vi.fn(() => stream),
    isReady: vi.fn(() => false),
    decode: vi.fn(),
    isEndpoint: vi.fn(() => false),
    reset: vi.fn(),
    getResult: vi.fn(() => ({ text: '' })),
    free: vi.fn(),
  };
}

beforeEach(() => {
  vi.clearAllMocks();
  // Default: all model files exist
  mockExistsSync.mockReturnValue(true);
});

afterEach(() => {
  vi.useRealTimers();
});

// ---------------------------------------------------------------------------
// SherpaOnnxLiveTranscriptionService
// ---------------------------------------------------------------------------

describe('SherpaOnnxLiveTranscriptionService', () => {
  describe('isAvailable()', () => {
    it('returns true when all model files exist', () => {
      const service = new SherpaOnnxLiveTranscriptionService({ modelsPath: '/models' });
      expect(service.isAvailable()).toBe(true);
      // Should check for 4 files: encoder, decoder, joiner, tokens
      expect(mockExistsSync).toHaveBeenCalledTimes(4);
    });

    it('returns false when any model file is missing', () => {
      // First call returns true (encoder), second returns false (decoder)
      mockExistsSync.mockReturnValueOnce(true).mockReturnValueOnce(false);

      const service = new SherpaOnnxLiveTranscriptionService({ modelsPath: '/models' });
      expect(service.isAvailable()).toBe(false);
    });
  });

  describe('start()', () => {
    it('throws if model files are not available', () => {
      mockExistsSync.mockReturnValue(false);

      const service = new SherpaOnnxLiveTranscriptionService({
        modelsPath: '/models',
        createRecognizer: vi.fn(),
      });
      const stream = new PassThrough();

      expect(() => service.start(stream, () => {})).toThrow('sherpa-onnx model not found');
    });

    it('throws if live transcription is already active', () => {
      const mockStream = makeMockStream();
      const mockRecognizer = makeMockRecognizer(mockStream);
      const createRecognizer = vi.fn(() => mockRecognizer) as CreateRecognizerFn;

      const service = new SherpaOnnxLiveTranscriptionService({
        modelsPath: '/models',
        createRecognizer,
      });
      const audioStream = new PassThrough();

      service.start(audioStream, () => {});

      expect(() => service.start(audioStream, () => {})).toThrow('already active');

      service.stop();
    });

    it('creates a recognizer and listens for audio data', () => {
      const mockStream = makeMockStream();
      const mockRecognizer = makeMockRecognizer(mockStream);
      const createRecognizer = vi.fn(() => mockRecognizer) as CreateRecognizerFn;

      const service = new SherpaOnnxLiveTranscriptionService({
        modelsPath: '/models',
        createRecognizer,
      });
      const audioStream = new PassThrough();
      const onText = vi.fn();

      service.start(audioStream, onText);

      expect(createRecognizer).toHaveBeenCalledOnce();
      expect(mockRecognizer.createStream).toHaveBeenCalledOnce();

      // Write some PCM data — should be forwarded to acceptWaveform as object
      const pcmData = Buffer.alloc(3200); // 100ms of 16kHz 16-bit mono
      audioStream.write(pcmData);

      expect(mockStream.acceptWaveform).toHaveBeenCalledWith({
        sampleRate: 16000,
        samples: expect.any(Float32Array),
      });

      service.stop();
    });

    it('passes modelType zipformer2 in recognizer config', () => {
      const mockStream = makeMockStream();
      const mockRecognizer = makeMockRecognizer(mockStream);
      const createRecognizer = vi.fn(() => mockRecognizer) as CreateRecognizerFn;

      const service = new SherpaOnnxLiveTranscriptionService({
        modelsPath: '/models',
        createRecognizer,
      });
      const audioStream = new PassThrough();

      service.start(audioStream, vi.fn());

      const config = createRecognizer.mock.calls[0][0];
      expect(config.modelConfig.modelType).toBe('zipformer2');

      service.stop();
    });

    it('calls onText when recognizer produces results at endpoint', () => {
      vi.useFakeTimers();

      const mockStream = makeMockStream();
      const mockRecognizer = makeMockRecognizer(mockStream);
      const createRecognizer = vi.fn(() => mockRecognizer) as CreateRecognizerFn;

      // Setup: recognizer has ready data after audio is fed
      vi.mocked(mockRecognizer.isReady).mockReturnValueOnce(true).mockReturnValue(false);
      vi.mocked(mockRecognizer.getResult).mockReturnValue({ text: 'hello world' });
      vi.mocked(mockRecognizer.isEndpoint).mockReturnValue(true);

      const service = new SherpaOnnxLiveTranscriptionService({
        modelsPath: '/models',
        createRecognizer,
      });
      const audioStream = new PassThrough();
      const onText = vi.fn();

      service.start(audioStream, onText);

      // Advance the poll timer
      vi.advanceTimersByTime(100);

      expect(mockRecognizer.decode).toHaveBeenCalled();
      expect(onText).toHaveBeenCalledWith('hello world');
      expect(mockRecognizer.reset).toHaveBeenCalled();

      service.stop();
    });

    it('shows in-progress text when no endpoint detected', () => {
      vi.useFakeTimers();

      const mockStream = makeMockStream();
      const mockRecognizer = makeMockRecognizer(mockStream);
      const createRecognizer = vi.fn(() => mockRecognizer) as CreateRecognizerFn;

      // Recognizer has partial result but no endpoint
      vi.mocked(mockRecognizer.isReady).mockReturnValueOnce(true).mockReturnValue(false);
      vi.mocked(mockRecognizer.getResult).mockReturnValue({ text: 'partial text' });
      vi.mocked(mockRecognizer.isEndpoint).mockReturnValue(false);

      const service = new SherpaOnnxLiveTranscriptionService({
        modelsPath: '/models',
        createRecognizer,
      });
      const audioStream = new PassThrough();
      const onText = vi.fn();

      service.start(audioStream, onText);
      vi.advanceTimersByTime(100);

      expect(onText).toHaveBeenCalledWith('partial text');

      service.stop();
    });

    it('accumulates text across endpoints', () => {
      vi.useFakeTimers();

      const mockStream = makeMockStream();
      const mockRecognizer = makeMockRecognizer(mockStream);
      const createRecognizer = vi.fn(() => mockRecognizer) as CreateRecognizerFn;

      const service = new SherpaOnnxLiveTranscriptionService({
        modelsPath: '/models',
        createRecognizer,
      });
      const audioStream = new PassThrough();
      const onText = vi.fn();

      service.start(audioStream, onText);

      // First endpoint: "hello"
      vi.mocked(mockRecognizer.isReady).mockReturnValueOnce(true).mockReturnValue(false);
      vi.mocked(mockRecognizer.getResult).mockReturnValue({ text: 'hello' });
      vi.mocked(mockRecognizer.isEndpoint).mockReturnValue(true);
      vi.advanceTimersByTime(100);

      expect(onText).toHaveBeenCalledWith('hello');

      // Second endpoint: "world"
      vi.mocked(mockRecognizer.isReady).mockReturnValueOnce(true).mockReturnValue(false);
      vi.mocked(mockRecognizer.getResult).mockReturnValue({ text: 'world' });
      vi.mocked(mockRecognizer.isEndpoint).mockReturnValue(true);
      vi.advanceTimersByTime(100);

      expect(onText).toHaveBeenCalledWith('hello world');

      service.stop();
    });

    it('does not call onText when result text is empty', () => {
      vi.useFakeTimers();

      const mockStream = makeMockStream();
      const mockRecognizer = makeMockRecognizer(mockStream);
      const createRecognizer = vi.fn(() => mockRecognizer) as CreateRecognizerFn;

      vi.mocked(mockRecognizer.isReady).mockReturnValueOnce(true).mockReturnValue(false);
      vi.mocked(mockRecognizer.getResult).mockReturnValue({ text: '' });
      vi.mocked(mockRecognizer.isEndpoint).mockReturnValue(false);

      const service = new SherpaOnnxLiveTranscriptionService({
        modelsPath: '/models',
        createRecognizer,
      });
      const audioStream = new PassThrough();
      const onText = vi.fn();

      service.start(audioStream, onText);
      vi.advanceTimersByTime(100);

      expect(onText).not.toHaveBeenCalled();

      service.stop();
    });
  });

  describe('stop()', () => {
    it('cleans up recognizer and stream resources', () => {
      const mockStream = makeMockStream();
      const mockRecognizer = makeMockRecognizer(mockStream);
      const createRecognizer = vi.fn(() => mockRecognizer) as CreateRecognizerFn;

      const service = new SherpaOnnxLiveTranscriptionService({
        modelsPath: '/models',
        createRecognizer,
      });
      const audioStream = new PassThrough();

      service.start(audioStream, () => {});
      service.stop();

      expect(mockStream.free).toHaveBeenCalled();
      expect(mockRecognizer.free).toHaveBeenCalled();
    });

    it('removes data listener from audio stream', () => {
      const mockStream = makeMockStream();
      const mockRecognizer = makeMockRecognizer(mockStream);
      const createRecognizer = vi.fn(() => mockRecognizer) as CreateRecognizerFn;

      const service = new SherpaOnnxLiveTranscriptionService({
        modelsPath: '/models',
        createRecognizer,
      });
      const audioStream = new PassThrough();
      const removeSpy = vi.spyOn(audioStream, 'removeListener');

      service.start(audioStream, () => {});
      service.stop();

      expect(removeSpy).toHaveBeenCalledWith('data', expect.any(Function));
    });

    it('is safe to call when not active (no-op after stop)', () => {
      const service = new SherpaOnnxLiveTranscriptionService({
        modelsPath: '/models',
        createRecognizer: vi.fn(),
      });

      // Should not throw
      service.stop();
    });
  });
});

// ---------------------------------------------------------------------------
// NoopLiveTranscriptionService
// ---------------------------------------------------------------------------

describe('NoopLiveTranscriptionService', () => {
  it('isAvailable() returns false', () => {
    const service = new NoopLiveTranscriptionService();
    expect(service.isAvailable()).toBe(false);
  });

  it('start() is a no-op', () => {
    const service = new NoopLiveTranscriptionService();
    const stream = new PassThrough();
    const onText = vi.fn();

    // Should not throw
    service.start(stream, onText);
  });

  it('stop() is a no-op', () => {
    const service = new NoopLiveTranscriptionService();

    // Should not throw
    service.stop();
  });
});
