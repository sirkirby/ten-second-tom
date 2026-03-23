import React from 'react';
import { Box, Text } from 'ink';

export type RecordingPhase = 'recording' | 'transcribing';

interface RecordingUIProps {
  phase: RecordingPhase;
  transcript: string;
  duration: number;
  /** Whether the transcript shown during recording is a live draft (sherpa-onnx). */
  isLivePreview?: boolean;
}

function formatDuration(seconds: number): string {
  const minutes = Math.floor(seconds / 60);
  const secs = seconds % 60;
  return `${minutes}:${String(secs).padStart(2, '0')}`;
}

export function RecordingUI({ phase, transcript, duration, isLivePreview }: RecordingUIProps) {
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

      {transcript.length > 0 && (
        <Box paddingLeft={2} marginTop={1} flexDirection="column">
          {isLivePreview && (
            <Text dimColor italic>
              Live preview
            </Text>
          )}
          <Text dimColor italic={isLivePreview}>
            {transcript}
          </Text>
        </Box>
      )}

      <Box paddingLeft={2}>
        <Text dimColor>{'Esc to cancel | Enter to finish'}</Text>
      </Box>
    </Box>
  );
}
