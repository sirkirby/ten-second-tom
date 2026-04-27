import React from 'react';
import { Box, Text } from 'ink';
import { runAnalysisPipeline } from 'ten-second-tom-core';
import type { ServiceContainer, ConfigManager } from 'ten-second-tom-core';
import { reindexEntries } from '../reindex.js';
import { ResultsSummary } from '../components/ResultsSummary.js';

// ---------------------------------------------------------------------------
// Screen + history types
// ---------------------------------------------------------------------------

export type Screen = 'home' | 'recording' | 'processing' | 'search' | 'note' | 'setup' | 'list';

export interface HistoryEntry {
  id: string;
  content: React.ReactNode;
}

// ---------------------------------------------------------------------------
// App context — passed to every command's execute()
// ---------------------------------------------------------------------------

export interface AppContext {
  services: ServiceContainer | null;
  configManager: ConfigManager | null;
  setScreen: (screen: Screen) => void;
  pushHistory: (entry: HistoryEntry) => void;
  setScreenData: (data: Record<string, unknown>) => void;
  exit: () => void;
  oneShot: boolean;
  finishCommand: () => void;
}

// ---------------------------------------------------------------------------
// Command definition
// ---------------------------------------------------------------------------

export interface TomCommand {
  name: string;
  description: string;
  execute: (args: string, context: AppContext) => void;
}

// ---------------------------------------------------------------------------
// Built-in commands
// ---------------------------------------------------------------------------

const helpCommand: TomCommand = {
  name: 'help',
  description: 'Show available commands',
  execute: (_args, ctx) => {
    const content = (
      <Box flexDirection="column">
        <Text dimColor>Type anything to search your entries. Commands use a / prefix:</Text>
        <Text> </Text>
        {COMMANDS.filter((c) => c.name !== 'help' && c.name !== 'quit').map((c) => (
          <Text key={c.name}>
            <Text color="green">{'/' + c.name.padEnd(12)}</Text>
            <Text dimColor>{c.description}</Text>
          </Text>
        ))}
        <Text> </Text>
        <Text>
          <Text color="green">{'/help'.padEnd(13)}</Text>
          <Text dimColor>Show this help</Text>
        </Text>
        <Text>
          <Text color="green">{'/quit'.padEnd(13)}</Text>
          <Text dimColor>Exit Tom</Text>
        </Text>
      </Box>
    );
    ctx.pushHistory({ id: `help-${Date.now()}`, content });
  },
};

const quitCommand: TomCommand = {
  name: 'quit',
  description: 'Exit Tom',
  execute: (_args, ctx) => {
    ctx.exit();
  },
};

const recordCmd: TomCommand = {
  name: 'record',
  description: 'Record audio with live transcription',
  execute: (_args, ctx) => {
    ctx.setScreen('recording');
  },
};

const noteCmd: TomCommand = {
  name: 'note',
  description: 'Create a text note (type or dictate)',
  execute: (_args, ctx) => {
    const text = _args.trim();
    if (ctx.oneShot && text.length > 0) {
      const svcs = ctx.services;
      if (!svcs) {
        ctx.pushHistory({
          id: `note-err-${Date.now()}`,
          content: (
            <Text color="yellow">
              Not configured. Run <Text color="green">tom setup</Text> first.
            </Text>
          ),
        });
        ctx.finishCommand();
        return;
      }

      void runAnalysisPipeline(text, undefined, svcs, {
        entryType: 'note',
        inputMethod: 'typed',
      })
        .then((result) => {
          ctx.pushHistory({
            id: `note-result-${Date.now()}`,
            content: (
              <ResultsSummary
                transcript={result.transcript}
                analysis={result.analysis}
                warnings={result.warnings}
                entryType="note"
              />
            ),
          });
        })
        .catch((err: unknown) => {
          ctx.pushHistory({
            id: `note-err-${Date.now()}`,
            content: <Text color="red">Failed to save note: {String(err)}</Text>,
          });
        })
        .finally(ctx.finishCommand);
      return;
    }

    ctx.setScreen('note');
  },
};

const searchCmd: TomCommand = {
  name: 'search',
  description: 'Search entries by meaning or keyword',
  execute: (args, ctx) => {
    const query = args.trim();
    if (query) {
      ctx.setScreenData({ query });
    } else if (ctx.oneShot) {
      ctx.pushHistory({
        id: `search-usage-${Date.now()}`,
        content: <Text color="yellow">Usage: tom search &lt;query&gt;</Text>,
      });
      ctx.finishCommand();
      return;
    }
    ctx.setScreen('search');
  },
};

const analyzeCmd: TomCommand = {
  name: 'analyze',
  description: 'Re-run analysis on an existing entry',
  execute: (args, ctx) => {
    const entryId = args.trim();
    if (!entryId) {
      ctx.pushHistory({
        id: `analyze-usage-${Date.now()}`,
        content: <Text color="yellow">Usage: tom analyze &lt;entry-id&gt;</Text>,
      });
      if (ctx.oneShot) ctx.finishCommand();
      return;
    }
    ctx.setScreenData({ entryId });
    ctx.setScreen('processing');
  },
};

const listCmd: TomCommand = {
  name: 'list',
  description: 'Browse recent entries',
  execute: (args, ctx) => {
    const filterArg = args.trim();
    const filter = filterArg === 'notes' || filterArg === 'recordings' ? filterArg : undefined;
    if (filterArg && !filter) {
      ctx.pushHistory({
        id: `list-usage-${Date.now()}`,
        content: <Text color="yellow">Usage: tom list [notes|recordings]</Text>,
      });
      if (ctx.oneShot) ctx.finishCommand();
      return;
    }
    ctx.setScreenData({ filter });
    ctx.setScreen('list');
  },
};

const setupCmd: TomCommand = {
  name: 'setup',
  description: 'Configure Tom',
  execute: (_args, ctx) => {
    ctx.setScreen('setup');
  },
};

const reindexCmd: TomCommand = {
  name: 'reindex',
  description: 'Re-embed all entries for semantic search',
  execute: (_args, ctx) => {
    const svcs = ctx.services;
    if (!svcs) {
      ctx.pushHistory({
        id: `reindex-err-${Date.now()}`,
        content: (
          <Text color="yellow">
            Not configured. Run <Text color="green">/setup</Text> first.
          </Text>
        ),
      });
      if (ctx.oneShot) ctx.finishCommand();
      return;
    }
    if (ctx.oneShot) {
      void reindexEntries(svcs, ctx.pushHistory).finally(ctx.finishCommand);
      return;
    }
    void reindexEntries(svcs, ctx.pushHistory);
  },
};

export const COMMANDS: TomCommand[] = [
  recordCmd,
  noteCmd,
  searchCmd,
  listCmd,
  analyzeCmd,
  setupCmd,
  reindexCmd,
  helpCommand,
  quitCommand,
];

/**
 * Look up a command by name.
 */
export function findCommand(name: string): TomCommand | undefined {
  return COMMANDS.find((c) => c.name === name);
}
