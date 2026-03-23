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
  process.stdout.write('\x1b[2J\x1b[3J\x1b[H');
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
  // Clear the terminal so Ink starts from a clean slate (no gap above content)
  process.stdout.write('\x1b[2J\x1b[3J\x1b[H');
  render(React.createElement(App, { mode: 'repl' }));
}
