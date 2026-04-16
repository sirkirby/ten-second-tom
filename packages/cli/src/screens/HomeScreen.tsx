import React, { useCallback } from 'react';
import { Box, Text } from 'ink';
import type { AppConfig } from 'ten-second-tom-core';
import { Prompt } from '../components/Prompt.js';
import { APP_VERSION } from '../constants.js';
import { COMMANDS, findCommand } from '../commands/registry.js';
import type { AppContext } from '../commands/registry.js';

const DIVIDER = '───────────────────────────────────────';

interface HomeScreenProps {
  context: AppContext;
  config: AppConfig | null;
  entryCount: number;
}

/**
 * Derive a human-readable label for the LLM provider.
 */
function llmLabel(config: AppConfig): string {
  if (config.llm.provider === 'cloud') return 'Claude';
  return config.llm.modelId;
}

/**
 * Build the config summary line, e.g. "Claude · whisper · bge-m3 · 12 entries"
 */
function configSummary(config: AppConfig, entryCount: number): string {
  const parts: string[] = [llmLabel(config), 'whisper'];

  if (config.embedding.provider !== 'none') {
    parts.push(config.embedding.model);
  }

  const entryLabel = entryCount === 1 ? 'entry' : 'entries';
  parts.push(`${entryCount} ${entryLabel}`);

  return parts.join(' \u00B7 ');
}

export function HomeScreen({ context, config, entryCount }: HomeScreenProps) {
  // ---- /command handler ----
  const handleCommand = useCallback(
    (name: string, args: string) => {
      const cmd = findCommand(name);
      if (cmd) {
        cmd.execute(args, context);
      } else {
        context.pushHistory({
          id: `unknown-${Date.now()}`,
          content: (
            <Text>
              Unknown command: /{name}. Type <Text color="green">/help</Text> for available
              commands.
            </Text>
          ),
        });
      }
    },
    [context],
  );

  // ---- search handler — transition to SearchScreen ----
  const handleSearch = useCallback(
    (query: string) => {
      context.setScreenData({ query });
      context.setScreen('search');
    },
    [context],
  );

  return (
    <Box flexDirection="column">
      <Text bold>Ten-Second Tom v{APP_VERSION}</Text>
      {config ? (
        <Text dimColor>{configSummary(config, entryCount)}</Text>
      ) : (
        <Text dimColor color="yellow">
          Not configured — type <Text color="green">/setup</Text> to get started
        </Text>
      )}
      <Text dimColor>{DIVIDER}</Text>
      <Prompt commands={COMMANDS} onCommand={handleCommand} onSearch={handleSearch} />
    </Box>
  );
}
