import React from 'react';
import { Box, Text } from 'ink';

interface ErrorDisplayProps {
  message: string;
}

/**
 * Shared error display component used across commands.
 * Replaces the duplicated error render block in record, note,
 * search, and analyze commands.
 */
export function ErrorDisplay({ message }: ErrorDisplayProps) {
  return (
    <Box flexDirection="column" paddingY={1}>
      <Text color="red" bold>
        Error
      </Text>
      <Text color="red">{message}</Text>
    </Box>
  );
}
