import React from 'react';
import { Box, Text } from 'ink';

interface RecordingUIProps {
  transcript: string;
  duration: number;
  isRecording: boolean;
}

function formatDuration(seconds: number): string {
  const minutes = Math.floor(seconds / 60);
  const secs = seconds % 60;
  return `${minutes}:${String(secs).padStart(2, '0')}`;
}

export function RecordingUI({ transcript, duration, isRecording }: RecordingUIProps) {
  return (
    <Box flexDirection="column" gap={1}>
      <Box>
        <Text color="red" bold>
          {'🎙️  RECORDING'}
        </Text>
        <Text bold>{` — ${formatDuration(duration)}`}</Text>
      </Box>

      <Box paddingLeft={2}>
        <Text>{transcript}</Text>
      </Box>

      {isRecording && (
        <Box paddingLeft={2}>
          <Text dimColor>{'◀ Esc to cancel  ▶ Enter to finish'}</Text>
        </Box>
      )}
    </Box>
  );
}
