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

  it('every model has non-empty files array with valid URLs', () => {
    for (const model of SHERPA_MODELS) {
      expect(model.files.length).toBeGreaterThan(0);
      for (const file of model.files) {
        expect(file.filename.length).toBeGreaterThan(0);
        expect(file.url).toMatch(/^https:\/\//);
      }
    }
  });

  it('every model has non-empty id, dirName, description, and sizeLabel', () => {
    for (const model of SHERPA_MODELS) {
      expect(model.id.length).toBeGreaterThan(0);
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
    expect(model.id).toBe('zipformer-en-kroko-2025-08-06');
  });
});

describe('findSherpaModel', () => {
  it('finds a model by id', () => {
    const model = findSherpaModel('zipformer-en-kroko-2025-08-06');
    expect(model).toBeDefined();
    expect(model?.dirName).toBe('sherpa-onnx-streaming-zipformer-en-kroko-2025-08-06');
  });

  it('returns undefined for unknown id', () => {
    expect(findSherpaModel('nonexistent')).toBeUndefined();
  });
});
