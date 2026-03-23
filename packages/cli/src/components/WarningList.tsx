import React from 'react';
import { Box, Text } from 'ink';

interface WarningListProps {
  warnings: string[];
}

/**
 * Shared warning list display. Renders each warning with a consistent
 * "Warning:" prefix in yellow. Used by record, note, and analyze commands.
 */
export function WarningList({ warnings }: WarningListProps) {
  if (warnings.length === 0) return null;

  return (
    <Box marginTop={1} flexDirection="column">
      {warnings.map((w, i) => (
        <Text key={i} color="yellow">
          Warning: {w}
        </Text>
      ))}
    </Box>
  );
}
