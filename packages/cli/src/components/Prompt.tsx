import React, { useState, useCallback } from 'react';
import { Box, Text, useInput } from 'ink';
import TextInput from 'ink-text-input';
import type { TomCommand } from '../commands/registry.js';

interface PromptProps {
  commands: TomCommand[];
  onCommand: (name: string, args: string) => void;
}

/**
 * Reusable REPL prompt with Tab autocomplete.
 *
 * Renders `tom >` in green, accepts text input, and dispatches
 * commands on Enter. Tab completes a partial match against the
 * command list (single unique prefix match only).
 */
export function Prompt({ commands, onCommand }: PromptProps) {
  const [value, setValue] = useState('');

  const handleSubmit = useCallback(
    (text: string) => {
      const trimmed = text.trim();
      if (!trimmed) return;

      const spaceIdx = trimmed.indexOf(' ');
      const name = spaceIdx === -1 ? trimmed : trimmed.slice(0, spaceIdx);
      const args = spaceIdx === -1 ? '' : trimmed.slice(spaceIdx + 1);

      onCommand(name, args);
      setValue('');
    },
    [onCommand],
  );

  // Tab autocomplete
  useInput((_input, key) => {
    if (!key.tab) return;

    const partial = value.trim().toLowerCase();
    if (!partial) return;

    const matches = commands.filter((c) => c.name.startsWith(partial));
    if (matches.length === 1 && matches[0]) {
      setValue(matches[0].name + ' ');
    }
  });

  const commandHints = commands
    .filter((c) => c.name !== 'quit' && c.name !== 'help')
    .map((c) => c.name)
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
            {' \u00B7 '}help
          </Text>
        </Box>
      )}
    </Box>
  );
}
