import { ConfigManager } from 'ten-second-tom-core';
import type { AppConfig } from 'ten-second-tom-core';

const SETUP_REQUIRED_MESSAGE = 'Tom is not configured. Run `tom setup` first.';

export type SetupGuardResult =
  | { ok: true; config: AppConfig; configManager: ConfigManager }
  | { ok: false; error: string };

/**
 * Check whether Tom is configured. Returns the loaded config and
 * ConfigManager on success, or an error message on failure.
 *
 * Calls load() once — it returns undefined if no config file exists,
 * avoiding the redundant isSetupComplete() + load() double-read.
 */
export function checkSetupComplete(): SetupGuardResult {
  const configManager = new ConfigManager();
  const config = configManager.load();
  if (config === undefined) {
    return { ok: false, error: SETUP_REQUIRED_MESSAGE };
  }
  return { ok: true, config, configManager };
}
