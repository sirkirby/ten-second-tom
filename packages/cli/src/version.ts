import { readFileSync } from 'node:fs';

const FALLBACK_VERSION = '0.0.0-dev';
const PACKAGE_JSON_URL = new URL('../package.json', import.meta.url);

type PackageMetadata = {
  version?: unknown;
};

function readPackageVersion(): string {
  try {
    const metadata = JSON.parse(readFileSync(PACKAGE_JSON_URL, 'utf8')) as PackageMetadata;
    return typeof metadata.version === 'string' && metadata.version.length > 0
      ? metadata.version
      : FALLBACK_VERSION;
  } catch {
    return FALLBACK_VERSION;
  }
}

export const APP_VERSION = readPackageVersion();
