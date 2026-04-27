import React from 'react';
import { Box, Text } from 'ink';
import type { Entry } from 'ten-second-tom-core';
import { TranscriptBox } from './TranscriptBox.js';
import { getSentimentColor, formatScore } from '../utils/sentiment.js';
import { formatConfidence, formatFullDate } from '../utils/format.js';

interface EntryDetailProps {
  entry: Entry;
}

export function EntryDetail({ entry }: EntryDetailProps) {
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
    <Box flexDirection="column" gap={1}>
      <Text bold>Entry - {formatFullDate(entry.createdAt)}</Text>

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
