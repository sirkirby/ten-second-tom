import { describe, it, expect } from 'vitest';
import { SHERPA_MODELS, getDefaultSherpaModel, findSherpaModel } from '../sherpa-models.js';

describe('SHERPA_MODELS', () => {
  it('contains at least one model', () => {
    expect(SHERPA_MODELS.length).toBeGreaterThan(0);
  });

  it('has exactly one recommended model', () => {
    const recommended = SHERPA_MODELS.filter((m) => m.recommended);
    expect(recommended).toHaveLength(1);
  });

  it('every model has a valid archive filename ending in .tar.bz2', () => {
    for (const model of SHERPA_MODELS) {
      expect(model.archiveFilename).toMatch(/\.tar\.bz2$/);
    }
  });

  it('every model has non-empty id, url, dirName, description, and sizeLabel', () => {
    for (const model of SHERPA_MODELS) {
      expect(model.id.length).toBeGreaterThan(0);
      expect(model.url).toMatch(/^https:\/\//);
      expect(model.dirName.length).toBeGreaterThan(0);
      expect(model.description.length).toBeGreaterThan(0);
      expect(model.sizeLabel.length).toBeGreaterThan(0);
      expect(model.sizeBytes).toBeGreaterThan(0);
    }
  });

  it('every model has required filenames for recognizer config', () => {
    for (const model of SHERPA_MODELS) {
      expect(model.encoderFilename.length).toBeGreaterThan(0);
      expect(model.decoderFilename.length).toBeGreaterThan(0);
      expect(model.joinerFilename.length).toBeGreaterThan(0);
      expect(model.tokensFilename.length).toBeGreaterThan(0);
    }
  });

  it('every model has a unique id', () => {
    const ids = SHERPA_MODELS.map((m) => m.id);
    expect(new Set(ids).size).toBe(ids.length);
  });
});

describe('getDefaultSherpaModel', () => {
  it('returns the recommended model', () => {
    const model = getDefaultSherpaModel();
    expect(model.recommended).toBe(true);
    expect(model.id).toBe('zipformer-en-2023-06-26');
  });
});

describe('findSherpaModel', () => {
  it('finds a model by id', () => {
    const model = findSherpaModel('zipformer-en-2023-06-26');
    expect(model).toBeDefined();
    expect(model?.dirName).toBe('sherpa-onnx-streaming-zipformer-en-2023-06-26');
  });

  it('returns undefined for unknown id', () => {
    expect(findSherpaModel('nonexistent')).toBeUndefined();
  });
});
