import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { PassThrough } from 'node:stream';
import { mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

// Mock node-record-lpcm16 before importing the module under test
const { mockRecordFn } = vi.hoisted(() => {
  const mockRecordFn = vi.fn();
  return { mockRecordFn };
});

vi.mock('node-record-lpcm16', () => {
  return {
    default: { record: mockRecordFn },
  };
});

// Import after mock is registered
const { AudioService } = await import('../audio.js');

function makeFakeRecording(stream: PassThrough) {
  return {
    stream: () => stream,
    stop: vi.fn(() => {
      // Simulate the recorder ending the stream when stop() is called
      setImmediate(() => stream.end());
    }),
    pause: vi.fn(),
    resume: vi.fn(),
  };
}

let audioDir: string;

beforeEach(() => {
  audioDir = mkdtempSync(join(tmpdir(), 'tst-audio-'));
  vi.clearAllMocks();
});

afterEach(() => {
  rmSync(audioDir, { recursive: true, force: true });
});

describe('AudioService', () => {
  it('isRecording() returns false before start', () => {
    const service = new AudioService({ audioDir });
    expect(service.isRecording()).toBe(false);
  });

  it('startRecording() sets recording state to true', () => {
    const stream = new PassThrough();
    mockRecordFn.mockReturnValue(makeFakeRecording(stream));

    const service = new AudioService({ audioDir });
    service.startRecording();

    expect(service.isRecording()).toBe(true);
  });

  it('throws if startRecording is called while already recording', () => {
    const stream = new PassThrough();
    mockRecordFn.mockReturnValue(makeFakeRecording(stream));

    const service = new AudioService({ audioDir });
    service.startRecording();

    expect(() => service.startRecording()).toThrow('Already recording');
  });

  it('getAudioStream() throws if not recording', () => {
    const service = new AudioService({ audioDir });
    expect(() => service.getAudioStream()).toThrow(
      'Not recording — call startRecording() first',
    );
  });

  it('getAudioStream() returns a Readable stream while recording', () => {
    const stream = new PassThrough();
    mockRecordFn.mockReturnValue(makeFakeRecording(stream));

    const service = new AudioService({ audioDir });
    service.startRecording();

    const readable = service.getAudioStream();
    expect(readable).toBe(stream);
  });

  it('stopRecording() throws if not recording', async () => {
    const service = new AudioService({ audioDir });
    await expect(service.stopRecording()).rejects.toThrow('Not recording');
  });

  it('stopRecording() saves WAV and returns path matching YYYY-MM/YYYY-MM-DD-{id}.wav', async () => {
    const stream = new PassThrough();
    const fakeRecording = makeFakeRecording(stream);
    mockRecordFn.mockReturnValue(fakeRecording);

    const service = new AudioService({ audioDir });
    service.startRecording();

    // Emit some audio data then stop
    stream.write(Buffer.from([0x00, 0x01, 0x02, 0x03]));

    const resultPath = await service.stopRecording();

    // Path format: YYYY-MM/YYYY-MM-DD-{8-char-id}.wav
    expect(resultPath).toMatch(
      /^\d{4}-\d{2}\/\d{4}-\d{2}-\d{2}-[0-9a-f]{8}\.wav$/,
    );

    // After stopping, isRecording() should be false
    expect(service.isRecording()).toBe(false);

    // The stop() method on the fake recording should have been called
    expect(fakeRecording.stop).toHaveBeenCalledOnce();
  });

  it('stopRecording() resets state so recording can start again', async () => {
    const stream1 = new PassThrough();
    const fakeRecording1 = makeFakeRecording(stream1);
    mockRecordFn.mockReturnValueOnce(fakeRecording1);

    const stream2 = new PassThrough();
    const fakeRecording2 = makeFakeRecording(stream2);
    mockRecordFn.mockReturnValueOnce(fakeRecording2);

    const service = new AudioService({ audioDir });

    service.startRecording();
    await service.stopRecording();

    expect(service.isRecording()).toBe(false);

    // Should be able to start again
    service.startRecording();
    expect(service.isRecording()).toBe(true);
  });
});
