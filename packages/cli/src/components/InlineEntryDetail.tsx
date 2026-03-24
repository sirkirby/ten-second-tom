import React from 'react';
import { Box, Text } from 'ink';
import type { Entry } from '@ten-second-tom/core';
import { TranscriptBox } from './TranscriptBox.js';
import { getSentimentColor, formatScore } from '../utils/sentiment.js';
import { formatFullDate, formatConfidence } from '../utils/format.js';

// ---------------------------------------------------------------------------
// InlineEntryDetail — rendered into Static history when user types a number
// ---------------------------------------------------------------------------

interface InlineEntryDetailProps {
  entry: Entry;
}

export function InlineEntryDetail({ entry }: InlineEntryDetailProps) {
  const analysis = entry.analysis;
  const hasTopics =
    analysis &&
    Array.isArray(analysis.raw['topics']) &&
    (analysis.raw['topics'] as string[]).length > 0;
  const hasContextType =
    analysis &&
    typeof analysis.raw['contextType'] === 'string' &&
    (analysis.raw['contextType'] as string).length > 0;

  return (
    <Box flexDirection="column">
      <Text bold>Entry — {formatFullDate(entry.createdAt)}</Text>

      <Box paddingLeft={2}>
        <TranscriptBox text={entry.content} />
      </Box>

      {analysis && (
        <Box paddingLeft={2} flexDirection="column">
          <Box gap={2}>
            <Text>
              <Text color={getSentimentColor(analysis.sentiment.score)}>
                {analysis.sentiment.label}
              </Text>
              {` (${formatScore(analysis.sentiment.score)})`}
            </Text>
            <Text dimColor>{formatConfidence(analysis.sentiment.confidence)} confidence</Text>
          </Box>

          {analysis.summary.length > 0 && (
            <Text>
              <Text dimColor>Summary: </Text>
              <Text>{analysis.summary}</Text>
            </Text>
          )}

          {(hasTopics || hasContextType) && (
            <Text dimColor>
              {hasTopics ? (analysis.raw['topics'] as string[]).join(', ') : ''}
              {hasTopics && hasContextType ? ' \u00B7 ' : ''}
              {hasContextType ? String(analysis.raw['contextType']) : ''}
            </Text>
          )}
        </Box>
      )}
    </Box>
  );
}
