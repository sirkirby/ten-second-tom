#!/usr/bin/env node
import React from 'react';
import { render } from 'ink';
import { App } from './app.js';

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
  const rows = process.stdout.rows ?? 24;
  process.stdout.write('\n'.repeat(rows));
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
  // Move cursor to bottom of terminal so Ink renders content at the bottom
  // and it grows upward naturally (like Claude Code). Without this, Ink
  // renders at the current cursor position, leaving a large gap above.
  const rows = process.stdout.rows ?? 24;
  process.stdout.write('\n'.repeat(rows));
  render(React.createElement(App, { mode: 'repl' }));
}
