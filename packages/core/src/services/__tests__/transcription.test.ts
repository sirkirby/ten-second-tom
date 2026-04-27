import { EventEmitter } from 'node:events';
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

const childProcessMocks = vi.hoisted(() => ({
  spawn: vi.fn(),
}));

vi.mock('node:child_process', () => ({
  spawn: childProcessMocks.spawn,
}));

const { WhisperTranscriptionService } = await import('../transcription.js');

interface MockChildProcess extends EventEmitter {
  stdout: EventEmitter;
  stderr: EventEmitter;
}

function createMockChildProcess(onSpawn: (args: string[]) => void): MockChildProcess {
  const child = new EventEmitter() as MockChildProcess;
  child.stdout = new EventEmitter();
  child.stderr = new EventEmitter();
  childProcessMocks.spawn.mockImplementationOnce((_command: string, args: string[]) => {
    queueMicrotask(() => {
      onSpawn(args);
      child.emit('exit', 0);
    });
    return child;
  });
  return child;
}

function workerOutputPath(args: string[]): string {
  const outputPath = args[6];
  if (!outputPath) throw new Error('missing worker output path');
  return outputPath;
}

describe('WhisperTranscriptionService', () => {
  let tempDir: string;
  let modelPath: string;

  beforeEach(() => {
    vi.clearAllMocks();
    tempDir = mkdtempSync(join(tmpdir(), 'tom-transcription-test-'));
    modelPath = join(tempDir, 'model.bin');
    writeFileSync(modelPath, 'mock model');
  });

  afterEach(() => {
    rmSync(tempDir, { recursive: true, force: true });
  });

  describe('isModelLoaded()', () => {
    it('returns false before loadModel is called', () => {
      const service = new WhisperTranscriptionService();
      expect(service.isModelLoaded()).toBe(false);
    });
  });

  describe('loadModel()', () => {
    it('records the whisper model path and isModelLoaded() returns true', async () => {
      const service = new WhisperTranscriptionService();

      await service.loadModel(modelPath);

      expect(service.isModelLoaded()).toBe(true);
      expect(childProcessMocks.spawn).not.toHaveBeenCalled();
    });

    it('throws a setup hint when the model file is missing', async () => {
      const service = new WhisperTranscriptionService();

      await expect(service.loadModel(join(tempDir, 'missing.bin'))).rejects.toThrow(
        'STT model not found',
      );
    });
  });

  describe('transcribeFile()', () => {
    it('throws if model is not loaded', async () => {
      const service = new WhisperTranscriptionService();

      await expect(service.transcribeFile('/audio.wav')).rejects.toThrow('Model not loaded');
    });

    it('returns the transcribed text from a quiet child process', async () => {
      const service = new WhisperTranscriptionService();
      await service.loadModel(modelPath);
      createMockChildProcess((args) => {
        writeFileSync(
          workerOutputPath(args),
          JSON.stringify({
            ok: true,
            result: {
              result: 'Hello, world!',
              segments: [{ text: 'Hello, world!' }],
            },
          }),
        );
      });

      const result = await service.transcribeFile('/audio.wav');

      expect(result).toBe('Hello, world!');
      expect(childProcessMocks.spawn).toHaveBeenCalledWith(
        process.execPath,
        expect.arrayContaining([modelPath, '/audio.wav']),
        { stdio: ['ignore', 'pipe', 'pipe'] },
      );
    });

    it('concatenates segment texts when result is empty', async () => {
      const service = new WhisperTranscriptionService();
      await service.loadModel(modelPath);
      createMockChildProcess((args) => {
        writeFileSync(
          workerOutputPath(args),
          JSON.stringify({
            ok: true,
            result: {
              result: '',
              segments: [{ text: ' Hello' }, { text: ' world' }],
            },
          }),
        );
      });

      const result = await service.transcribeFile('/audio.wav');

      expect(result).toBe(' Hello world');
    });

    it('reports worker failures without writing native output to the parent TUI', async () => {
      const service = new WhisperTranscriptionService();
      await service.loadModel(modelPath);
      const child = createMockChildProcess((args) => {
        child.stdout.emit('data', Buffer.from('whisper_init_from_file_with_params_no_state'));
        child.stderr.emit('data', Buffer.from('ggml_metal_init'));
        writeFileSync(
          workerOutputPath(args),
          JSON.stringify({
            ok: false,
            error: 'bad model',
          }),
        );
      });

      await expect(service.transcribeFile('/audio.wav')).rejects.toThrow(
        'Whisper transcription failed: bad model. Native output was captured and suppressed.',
      );
    });
  });

  describe('release()', () => {
    it('clears the loaded model marker', async () => {
      const service = new WhisperTranscriptionService();
      await service.loadModel(modelPath);

      expect(service.isModelLoaded()).toBe(true);

      await service.release();

      expect(service.isModelLoaded()).toBe(false);
    });

    it('is safe to call when no model is loaded', async () => {
      const service = new WhisperTranscriptionService();

      await expect(service.release()).resolves.toBeUndefined();
    });
  });
});
