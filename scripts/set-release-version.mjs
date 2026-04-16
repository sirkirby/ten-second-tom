#!/usr/bin/env node

import fs from 'node:fs';
import path from 'node:path';

const version = process.argv[2]?.trim();

if (!version) {
  console.error('Usage: node scripts/set-release-version.mjs <version>');
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

function updateCliVersionConstant() {
  const relativePath = 'packages/cli/src/constants.ts';
  const absolutePath = path.join(repoRoot, relativePath);
  const current = fs.readFileSync(absolutePath, 'utf8');
  const next = current.replace(
    /export const APP_VERSION = '.*';/,
    `export const APP_VERSION = '${version}';`,
  );

  if (current === next) return;
  fs.writeFileSync(absolutePath, next, 'utf8');
}

updatePackageVersion('packages/cli/package.json');
updatePackageVersion('packages/core/package.json');
updateCliVersionConstant();
