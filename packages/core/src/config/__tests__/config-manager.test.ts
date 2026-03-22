import { describe, it, expect, afterEach } from 'vitest';
import { mkdtempSync, rmSync, writeFileSync, existsSync, mkdirSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { ConfigManager } from '../config-manager.js';
import type { AppConfig } from '../../types/config.js';

let tempDir: string;

afterEach(() => {
  if (tempDir) {
    rmSync(tempDir, { recursive: true, force: true });
  }
});

function createTempDir(): string {
  tempDir = mkdtempSync(join(tmpdir(), 'tst-config-'));
  return tempDir;
}

const validConfig: AppConfig = {
  llm: { provider: 'cloud', apiKey: 'sk-ant-test' },
  stt: { engine: 'whisper-distil-en', modelPath: '/tmp/models/whisper' },
  embedding: { provider: 'none', model: '' },
  storage: { dbPath: '/tmp/tom.db' },
};

describe('ConfigManager', () => {
  it('creates the tom directory structure on init', () => {
    const dir = createTempDir();
    const homePath = join(dir, '.tom');
    const manager = new ConfigManager(homePath);

    manager.ensureDirectories();

    expect(existsSync(join(homePath, 'audio'))).toBe(true);
    expect(existsSync(join(homePath, 'models'))).toBe(true);
  });

  it('saves and loads config', () => {
    const dir = createTempDir();
    const homePath = join(dir, '.tom');
    const manager = new ConfigManager(homePath);

    manager.save(validConfig);
    const loaded = manager.load();

    expect(loaded).toBeDefined();
    expect(loaded?.llm).toEqual(validConfig.llm);
    expect(loaded?.stt).toEqual(validConfig.stt);
    expect(loaded?.embedding).toEqual(validConfig.embedding);
    expect(loaded?.storage).toEqual(validConfig.storage);
  });

  it('returns undefined when no config exists', () => {
    const dir = createTempDir();
    const homePath = join(dir, '.tom');
    const manager = new ConfigManager(homePath);

    const result = manager.load();

    expect(result).toBeUndefined();
  });

  it('validates config on load', () => {
    const dir = createTempDir();
    const homePath = join(dir, '.tom');
    mkdirSync(homePath, { recursive: true });
    writeFileSync(join(homePath, 'config.json'), JSON.stringify({ llm: { provider: 'invalid' } }), 'utf-8');

    const manager = new ConfigManager(homePath);

    expect(() => manager.load()).toThrow();
  });

  it('reports whether setup is complete', () => {
    const dir = createTempDir();
    const homePath = join(dir, '.tom');
    const manager = new ConfigManager(homePath);

    expect(manager.isSetupComplete()).toBe(false);

    manager.save(validConfig);

    expect(manager.isSetupComplete()).toBe(true);
  });

  it('returns the tom home directory path', () => {
    const dir = createTempDir();
    const homePath = join(dir, '.tom');
    const manager = new ConfigManager(homePath);

    expect(manager.homePath).toBe(homePath);
  });

  it('returns audio directory path', () => {
    const dir = createTempDir();
    const homePath = join(dir, '.tom');
    const manager = new ConfigManager(homePath);

    expect(manager.audioPath).toBe(join(homePath, 'audio'));
  });

  it('returns models directory path', () => {
    const dir = createTempDir();
    const homePath = join(dir, '.tom');
    const manager = new ConfigManager(homePath);

    expect(manager.modelsPath).toBe(join(homePath, 'models'));
  });
});
