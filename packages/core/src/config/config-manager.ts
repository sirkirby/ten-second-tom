import { chmodSync, existsSync, mkdirSync, readFileSync, renameSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { AppConfigSchema, type AppConfig } from '../types/config.js';
import { PRIVATE_DIR_MODE, PRIVATE_FILE_MODE } from '../constants.js';

const TOM_HOME_ENV = 'TOM_HOME';

function chmodBestEffort(path: string, mode: number): void {
  try {
    chmodSync(path, mode);
  } catch {
    // Some platforms/filesystems do not support POSIX modes.
  }
}

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
      process.env[TOM_HOME_ENV] ??
      join(process.env['HOME'] ?? process.env['USERPROFILE'] ?? '', '.tom');
    this.audioPath = join(this.homePath, 'audio');
    this.modelsPath = join(this.homePath, 'models');
    this.configFilePath = join(this.homePath, 'config.json');
  }

  ensureDirectories(): void {
    mkdirSync(this.homePath, { recursive: true, mode: PRIVATE_DIR_MODE });
    mkdirSync(this.audioPath, { recursive: true, mode: PRIVATE_DIR_MODE });
    mkdirSync(this.modelsPath, { recursive: true, mode: PRIVATE_DIR_MODE });
    chmodBestEffort(this.homePath, PRIVATE_DIR_MODE);
    chmodBestEffort(this.audioPath, PRIVATE_DIR_MODE);
    chmodBestEffort(this.modelsPath, PRIVATE_DIR_MODE);
  }

  save(config: AppConfig): void {
    AppConfigSchema.parse(config);
    this.ensureDirectories();
    const tempConfigPath = `${this.configFilePath}.tmp`;
    writeFileSync(tempConfigPath, JSON.stringify(config, null, 2), {
      encoding: 'utf-8',
      mode: PRIVATE_FILE_MODE,
    });
    chmodBestEffort(tempConfigPath, PRIVATE_FILE_MODE);
    renameSync(tempConfigPath, this.configFilePath);
    chmodBestEffort(this.configFilePath, PRIVATE_FILE_MODE);
    this.cachedConfig = config;
  }

  load(): AppConfig | undefined {
    if (this.cachedConfig !== null) return this.cachedConfig;

    if (!existsSync(this.configFilePath)) {
      this.cachedConfig = undefined;
      return undefined;
    }
    chmodBestEffort(this.configFilePath, PRIVATE_FILE_MODE);
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
