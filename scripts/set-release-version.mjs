#!/usr/bin/env node

import fs from 'node:fs';
import path from 'node:path';

const version = process.argv[2]?.trim();
const SEMVER_PATTERN = /^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$/;

if (!version) {
  console.error('Usage: node scripts/set-release-version.mjs <version>');
  process.exit(1);
}

if (!SEMVER_PATTERN.test(version)) {
  console.error(`Invalid semver version: ${version}`);
  process.exit(1);
}

const repoRoot = process.cwd();

function readJson(relativePath) {
  return JSON.parse(fs.readFileSync(path.join(repoRoot, relativePath), 'utf8'));
}

function writeJson(relativePath, value) {
  fs.writeFileSync(
    path.join(repoRoot, relativePath),
    `${JSON.stringify(value, null, 2)}\n`,
    'utf8',
  );
}

function updatePackageVersion(relativePath) {
  const pkg = readJson(relativePath);
  if (pkg.version === version) return;
  pkg.version = version;
  writeJson(relativePath, pkg);
}

updatePackageVersion('packages/cli/package.json');
updatePackageVersion('packages/core/package.json');
