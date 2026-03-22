import { readFileSync, writeFileSync, existsSync, mkdirSync } from 'node:fs';
import { join } from 'node:path';
import { AppConfigSchema, type AppConfig } from '../types/config.js';

export class ConfigManager {
  readonly homePath: string;
  readonly audioPath: string;
  readonly modelsPath: string;

  private readonly configFilePath: string;

  /** Instance-level cache. `null` means "not loaded yet". */
  private cachedConfig: AppConfig | undefined | null = null;

  constructor(homePath?: string) {
    this.homePath =
      homePath ??
      join(process.env['HOME'] ?? process.env['USERPROFILE'] ?? '', '.tom');
    this.audioPath = join(this.homePath, 'audio');
    this.modelsPath = join(this.homePath, 'models');
    this.configFilePath = join(this.homePath, 'config.json');
  }

  ensureDirectories(): void {
    mkdirSync(this.homePath, { recursive: true });
    mkdirSync(this.audioPath, { recursive: true });
    mkdirSync(this.modelsPath, { recursive: true });
  }

  save(config: AppConfig): void {
    AppConfigSchema.parse(config);
    this.ensureDirectories();
    writeFileSync(this.configFilePath, JSON.stringify(config, null, 2), 'utf-8');
    this.cachedConfig = config;
  }

  load(): AppConfig | undefined {
    if (this.cachedConfig !== null) return this.cachedConfig;

    if (!existsSync(this.configFilePath)) {
      this.cachedConfig = undefined;
      return undefined;
    }
    const raw = readFileSync(this.configFilePath, 'utf-8');
    const parsed: unknown = JSON.parse(raw);
    const result = AppConfigSchema.parse(parsed);
    this.cachedConfig = result;
    return result;
  }

  isSetupComplete(): boolean {
    try {
      return this.load() !== undefined;
    } catch {
      return false;
    }
  }
}
