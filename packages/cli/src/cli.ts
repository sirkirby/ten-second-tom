#!/usr/bin/env node
import React from 'react';
import { render } from 'ink';
import { openSync, closeSync } from 'node:fs';
import { App } from './app.js';

// Suppress native C++ stderr output (whisper.cpp, ggml, sherpa-onnx)
// permanently for the entire app lifecycle. These libraries write directly
// to file descriptor 2 via fprintf(stderr, ...) and cannot be suppressed
// via Node.js APIs. Redirecting fd 2 to /dev/null is the only reliable fix.
// We do NOT restore it — stderr is unused by Tom (all output goes to stdout via Ink).
try {
  closeSync(2);
  openSync('/dev/null', 'w'); // gets fd 2
} catch {
  // Non-fatal — best effort suppression
}

const args = process.argv.slice(2);
const KNOWN_COMMANDS = ['record', 'note', 'search', 'analyze', 'setup'];

const firstArg = args[0];

if (firstArg === '--help' || firstArg === '-h') {
  // Static help — no Ink needed
  process.stdout.write(
    [
      'Usage: tom [command]',
      '',
      'Commands:',
      '  record     Record audio with live transcription',
      '  note       Create a text note (type or dictate)',
      '  search     Search entries by meaning or keyword',
      '  analyze    Re-run analysis on an existing entry',
      '  setup      Configure Tom',
      '',
      'Run tom with no arguments for interactive mode.',
      '',
    ].join('\n'),
  );
} else if (firstArg === '--version' || firstArg === '-V') {
  process.stdout.write('2.0.0\n');
} else if (firstArg && KNOWN_COMMANDS.includes(firstArg)) {
  // One-shot mode — run a single command then exit
  const command = firstArg;
  const commandArgs = args.slice(1).join(' ');
  render(
    React.createElement(App, {
      mode: 'oneshot',
      initialCommand: command,
      initialArgs: commandArgs,
    }),
  );
} else {
  // REPL mode — persistent interactive app
  render(React.createElement(App, { mode: 'repl' }));
}
