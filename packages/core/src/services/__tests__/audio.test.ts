import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { PassThrough } from 'node:stream';
import { mkdtempSync, rmSync, writeFileSync, readFileSync } from 'node:fs';
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
const { AudioService, checkAudioPrerequisites, checkModelExists, createWavHeader } = await import(
  '../audio.js'
);

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
    expect(() => service.getAudioStream()).toThrow('Not recording — call startRecording() first');
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
    expect(resultPath).toMatch(/^\d{4}-\d{2}\/\d{4}-\d{2}-\d{2}-[0-9a-f]{8}\.wav$/);

    // After stopping, isRecording() should be false
    expect(service.isRecording()).toBe(false);

    // The stop() method on the fake recording should have been called
    expect(fakeRecording.stop).toHaveBeenCalledOnce();
  });

  it('stopRecording() writes a valid WAV header before raw PCM data', async () => {
    const stream = new PassThrough();
    const fakeRecording = makeFakeRecording(stream);
    mockRecordFn.mockReturnValue(fakeRecording);

    const service = new AudioService({ audioDir });
    service.startRecording();

    // Write 4 bytes of PCM data
    const pcmData = Buffer.from([0x00, 0x01, 0x02, 0x03]);
    stream.write(pcmData);

    const resultPath = await service.stopRecording();

    // Read the saved file
    const filePath = join(audioDir, resultPath);
    const fileData = readFileSync(filePath);

    // File should be 44 (WAV header) + 4 (PCM data) = 48 bytes
    expect(fileData.length).toBe(48);

    // Verify WAV header magic bytes
    expect(fileData.toString('ascii', 0, 4)).toBe('RIFF');
    expect(fileData.toString('ascii', 8, 12)).toBe('WAVE');
    expect(fileData.toString('ascii', 12, 16)).toBe('fmt ');
    expect(fileData.toString('ascii', 36, 40)).toBe('data');

    // Verify data chunk size matches PCM data length
    expect(fileData.readUInt32LE(40)).toBe(4);

    // Verify the raw PCM data follows the header
    expect(fileData.subarray(44)).toEqual(pcmData);
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

// ---------------------------------------------------------------------------
// Tests: createWavHeader
// ---------------------------------------------------------------------------

describe('createWavHeader', () => {
  it('returns a 44-byte buffer', () => {
    const header = createWavHeader(0);
    expect(header.length).toBe(44);
  });

  it('contains correct RIFF/WAVE identifiers', () => {
    const header = createWavHeader(1000);
    expect(header.toString('ascii', 0, 4)).toBe('RIFF');
    expect(header.toString('ascii', 8, 12)).toBe('WAVE');
    expect(header.toString('ascii', 12, 16)).toBe('fmt ');
    expect(header.toString('ascii', 36, 40)).toBe('data');
  });

  it('encodes correct file size (36 + dataLength)', () => {
    const dataLength = 32000;
    const header = createWavHeader(dataLength);
    expect(header.readUInt32LE(4)).toBe(36 + dataLength);
  });

  it('encodes 16kHz sample rate, mono, 16-bit PCM', () => {
    const header = createWavHeader(0);
    expect(header.readUInt16LE(20)).toBe(1); // PCM format
    expect(header.readUInt16LE(22)).toBe(1); // mono
    expect(header.readUInt32LE(24)).toBe(16000); // sample rate
    expect(header.readUInt32LE(28)).toBe(32000); // byte rate (16000 * 1 * 2)
    expect(header.readUInt16LE(32)).toBe(2); // block align (1 * 2)
    expect(header.readUInt16LE(34)).toBe(16); // bits per sample
  });

  it('encodes correct data chunk size', () => {
    const header = createWavHeader(64000);
    expect(header.readUInt32LE(40)).toBe(64000);
  });
});

// ---------------------------------------------------------------------------
// Tests: checkAudioPrerequisites
// ---------------------------------------------------------------------------

describe('checkAudioPrerequisites', () => {
  it('returns { ok: true } when sox is available', () => {
    // We cannot fully control whether sox is installed in the test env,
    // but we can at least verify the function returns a valid shape.
    const result = checkAudioPrerequisites();
    expect(result).toHaveProperty('ok');
    if (result.ok) {
      expect(result).toEqual({ ok: true });
    } else {
      expect(result).toHaveProperty('message');
      expect(result.message).toContain('SoX');
    }
  });

  it('returns an object with ok and message properties', () => {
    const result = checkAudioPrerequisites();
    expect(typeof result.ok).toBe('boolean');
    if (!result.ok) {
      expect(typeof result.message).toBe('string');
      expect(result.message.length).toBeGreaterThan(0);
    }
  });
});

// ---------------------------------------------------------------------------
// Tests: checkModelExists
// ---------------------------------------------------------------------------

describe('checkModelExists', () => {
  it('returns { ok: true } when model file exists', () => {
    const modelPath = join(audioDir, 'test-model.bin');
    writeFileSync(modelPath, 'fake model data');

    const result = checkModelExists(modelPath);
    expect(result).toEqual({ ok: true });
  });

  it('returns { ok: false } with message when model file does not exist', () => {
    const modelPath = join(audioDir, 'nonexistent-model.bin');

    const result = checkModelExists(modelPath);
    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.message).toContain('Whisper model not found');
      expect(result.message).toContain(modelPath);
      expect(result.message).toContain('tom setup');
    }
  });
});
