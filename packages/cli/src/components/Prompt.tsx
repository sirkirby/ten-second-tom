import React, { useState, useCallback, useMemo } from 'react';
import { Box, Text, useInput } from 'ink';
import TextInput from 'ink-text-input';
import type { TomCommand } from '../commands/registry.js';

interface PromptProps {
  commands: TomCommand[];
  onCommand: (name: string, args: string) => void;
  onSearch: (query: string) => void;
}

/**
 * Reusable REPL prompt with Tab autocomplete and search-first UX.
 *
 * Renders `tom >` in green, accepts text input, and dispatches:
 * - `/command args` → onCommand(name, args)
 * - free text → onSearch(text)
 *
 * Tab completes `/` commands: `/re` → `/record`.
 *
 * Shows contextual hints below the input:
 * - Empty input: "Type to search · / for commands"
 * - Input starts with `/`: filtered list of matching commands
 */
export function Prompt({ commands, onCommand, onSearch }: PromptProps) {
  const [value, setValue] = useState('');
  const stdinSupportsInput = process.stdin.isTTY === true;

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
      } else {
        // Free text — transition to SearchScreen
        onSearch(trimmed);
      }

      setValue('');
    },
    [onCommand, onSearch],
  );

  // Tab autocomplete for /commands
  useInput(
    (_input, key) => {
      if (!key.tab) return;

      const trimmed = value.trim();
      if (!trimmed.startsWith('/')) return;

      const partial = trimmed.slice(1).toLowerCase();
      if (!partial) return;

      const matches = commands.filter((c) => c.name.startsWith(partial));
      if (matches.length === 1 && matches[0]) {
        setValue('/' + matches[0].name + ' ');
      }
    },
    { isActive: stdinSupportsInput },
  );

  // Compute filtered command suggestions when input starts with /
  const commandSuggestions = useMemo(() => {
    const trimmed = value.trim();
    if (!trimmed.startsWith('/')) return [];

    const partial = trimmed.slice(1).toLowerCase();
    if (!partial) return commands; // Show all commands when just "/" is typed

    return commands.filter((c) => c.name.startsWith(partial));
  }, [value, commands]);

  const isSlashInput = value.trim().startsWith('/');
  const isEmpty = !value;

  return (
    <Box flexDirection="column">
      <Box>
        <Text color="green" bold>
          {'tom > '}
        </Text>
        <TextInput value={value} onChange={setValue} onSubmit={handleSubmit} />
      </Box>
      {isEmpty && (
        <Box paddingLeft={2}>
          <Text dimColor>Type to search</Text>
          <Text dimColor> {'\u00B7'} </Text>
          <Text dimColor>/ for commands</Text>
        </Box>
      )}
      {isSlashInput && commandSuggestions.length > 0 && (
        <Box flexDirection="column" paddingLeft={2}>
          {commandSuggestions.map((c) => (
            <Box key={c.name}>
              <Text color="green">{'/' + c.name.padEnd(12)}</Text>
              <Text dimColor>{c.description}</Text>
            </Box>
          ))}
        </Box>
      )}
    </Box>
  );
}
