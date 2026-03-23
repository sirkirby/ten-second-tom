import React from 'react';
import { Box, Text } from 'ink';

interface TranscriptBoxProps {
  text: string;
}

export function TranscriptBox({ text }: TranscriptBoxProps) {
  return (
    <Box borderStyle="single" paddingX={1}>
      <Text>{text}</Text>
    </Box>
  );
}
