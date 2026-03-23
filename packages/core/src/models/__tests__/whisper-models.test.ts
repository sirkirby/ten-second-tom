import { describe, it, expect } from 'vitest';
import { WHISPER_MODELS, getDefaultWhisperModel, findWhisperModel } from '../whisper-models.js';

describe('WHISPER_MODELS', () => {
  it('contains at least one model', () => {
    expect(WHISPER_MODELS.length).toBeGreaterThan(0);
  });

  it('has exactly one recommended model', () => {
    const recommended = WHISPER_MODELS.filter((m) => m.recommended);
    expect(recommended).toHaveLength(1);
  });

  it('every model has a valid filename ending in .bin', () => {
    for (const model of WHISPER_MODELS) {
      expect(model.filename).toMatch(/\.bin$/);
    }
  });

  it('every model has a non-empty id, url, description, and sizeLabel', () => {
    for (const model of WHISPER_MODELS) {
      expect(model.id.length).toBeGreaterThan(0);
      expect(model.url).toMatch(/^https:\/\//);
      expect(model.description.length).toBeGreaterThan(0);
      expect(model.sizeLabel.length).toBeGreaterThan(0);
      expect(model.sizeBytes).toBeGreaterThan(0);
    }
  });

  it('every model has a unique id', () => {
    const ids = WHISPER_MODELS.map((m) => m.id);
    expect(new Set(ids).size).toBe(ids.length);
  });
});

describe('getDefaultWhisperModel', () => {
  it('returns the recommended model', () => {
    const model = getDefaultWhisperModel();
    expect(model.recommended).toBe(true);
    expect(model.id).toBe('distil-small.en');
  });
});

describe('findWhisperModel', () => {
  it('finds a model by id', () => {
    const model = findWhisperModel('base.en');
    expect(model).toBeDefined();
    expect(model?.filename).toBe('ggml-base.en.bin');
  });

  it('returns undefined for unknown id', () => {
    expect(findWhisperModel('nonexistent')).toBeUndefined();
  });
});
