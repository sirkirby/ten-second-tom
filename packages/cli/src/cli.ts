#!/usr/bin/env node
import { APP_VERSION } from './constants.js';

const args = process.argv.slice(2);
const KNOWN_COMMANDS = ['record', 'note', 'search', 'analyze', 'setup', 'list', 'reindex'];

const firstArg = args[0];

async function renderApp(mode: 'repl' | 'oneshot', initialCommand?: string, initialArgs?: string) {
  const [{ default: React }, { render }, { App }] = await Promise.all([
    import('react'),
    import('ink'),
    import('./app.js'),
  ]);

  render(
    React.createElement(App, {
      mode,
      initialCommand,
      initialArgs,
    }),
  );
}

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
      '  list       Browse recent entries',
      '  reindex    Re-embed all entries for semantic search',
      '',
      'Run tom with no arguments for interactive mode.',
      '',
    ].join('\n'),
  );
} else if (firstArg === '--version' || firstArg === '-V') {
  process.stdout.write(`${APP_VERSION}\n`);
} else if (firstArg && KNOWN_COMMANDS.includes(firstArg)) {
  // One-shot mode — run a single command then exit
  const command = firstArg;
  const commandArgs = args.slice(1).join(' ');
  await renderApp('oneshot', command, commandArgs);
} else {
  // REPL mode — persistent interactive app
  await renderApp('repl');
}
