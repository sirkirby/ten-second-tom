import React from 'react';
import { Box, Text } from 'ink';

export type RecordingPhase = 'recording' | 'transcribing';

interface RecordingUIProps {
  phase: RecordingPhase;
  transcript: string;
  duration: number;
}

function formatDuration(seconds: number): string {
  const minutes = Math.floor(seconds / 60);
  const secs = seconds % 60;
  return `${minutes}:${String(secs).padStart(2, '0')}`;
}

export function RecordingUI({ phase, transcript, duration }: RecordingUIProps) {
  if (phase === 'transcribing') {
    return (
      <Box flexDirection="column" gap={1}>
        <Box>
          <Text color="cyan" bold>
            {'Transcribing...'}
          </Text>
        </Box>

        {transcript.length > 0 && (
          <Box paddingLeft={2}>
            <Text>{transcript}</Text>
          </Box>
        )}
      </Box>
    );
  }

  return (
    <Box flexDirection="column" gap={1}>
      <Box>
        <Text color="red" bold>
          {'RECORDING'}
        </Text>
        <Text bold>{` — ${formatDuration(duration)}`}</Text>
      </Box>

      <Box paddingLeft={2}>
        <Text dimColor>{'Esc to cancel | Enter to finish'}</Text>
      </Box>
    </Box>
  );
}
