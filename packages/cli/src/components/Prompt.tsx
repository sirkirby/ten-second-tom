import React, { useState, useCallback } from 'react';
import { Box, Text, useInput } from 'ink';
import TextInput from 'ink-text-input';
import type { TomCommand } from '../commands/registry.js';

interface PromptProps {
  commands: TomCommand[];
  onCommand: (name: string, args: string) => void;
  onSearch: (query: string) => void;
  onExpandResult: (index: number) => void;
}

/**
 * Reusable REPL prompt with Tab autocomplete and search-first UX.
 *
 * Renders `tom >` in green, accepts text input, and dispatches:
 * - `/command args` → onCommand(name, args)
 * - bare number → onExpandResult(number)
 * - free text → onSearch(text)
 *
 * Tab completes `/` commands: `/re` → `/record`.
 */
export function Prompt({ commands, onCommand, onSearch, onExpandResult }: PromptProps) {
  const [value, setValue] = useState('');

  const handleSubmit = useCallback(
    (text: string) => {
      const trimmed = text.trim();
      if (!trimmed) return;

      if (trimmed.startsWith('/')) {
        // Slash command
        const withoutSlash = trimmed.slice(1);
        const [commandName, ...rest] = withoutSlash.split(' ');
        const args = rest.join(' ');
        onCommand(commandName ?? '', args);
      } else if (/^\d+$/.test(trimmed)) {
        // Number — expand search result
        onExpandResult(parseInt(trimmed, 10));
      } else {
        // Free text — semantic search
        onSearch(trimmed);
      }

      setValue('');
    },
    [onCommand, onSearch, onExpandResult],
  );

  // Tab autocomplete for /commands
  useInput((_input, key) => {
    if (!key.tab) return;

    const trimmed = value.trim();
    if (!trimmed.startsWith('/')) return;

    const partial = trimmed.slice(1).toLowerCase();
    if (!partial) return;

    const matches = commands.filter((c) => c.name.startsWith(partial));
    if (matches.length === 1 && matches[0]) {
      setValue('/' + matches[0].name + ' ');
    }
  });

  const commandHints = commands
    .filter((c) => c.name !== 'quit' && c.name !== 'help')
    .map((c) => '/' + c.name)
    .join(' \u00B7 ');

  return (
    <Box flexDirection="column">
      <Box>
        <Text color="green" bold>
          {'tom > '}
        </Text>
        <TextInput value={value} onChange={setValue} onSubmit={handleSubmit} />
      </Box>
      {!value && (
        <Box paddingLeft={6}>
          <Text dimColor>
            {commandHints}
            {' \u00B7 '}
            /help {'  \u2014  '}or just type to search
          </Text>
        </Box>
      )}
    </Box>
  );
}
