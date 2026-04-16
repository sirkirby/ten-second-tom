import React from 'react';
import { Box, Text } from 'ink';
import type { EntryAnalysis } from 'ten-second-tom-core';
import { getSentimentColor, formatScore } from '../utils/sentiment.js';
import { formatConfidence } from '../utils/format.js';

interface SentimentDisplayProps {
  analysis: EntryAnalysis;
}

export function SentimentDisplay({ analysis }: SentimentDisplayProps) {
  const { sentiment, summary } = analysis;
  const sentimentColor = getSentimentColor(sentiment.score);

  return (
    <Box flexDirection="column" gap={1}>
      <Text bold>{'📊 Analysis'}</Text>

      <Box paddingLeft={2} flexDirection="column">
        <Text>
          {'Sentiment: '}
          <Text color={sentimentColor}>{sentiment.label}</Text>
          {` (${formatScore(sentiment.score)})`}
        </Text>

        <Text>{`Confidence: ${formatConfidence(sentiment.confidence)}`}</Text>

        <Text>{`Summary: ${summary}`}</Text>

        {analysis.raw['contextType'] && (
          <Text dimColor>{`  Context: ${String(analysis.raw['contextType'])}`}</Text>
        )}
        {Array.isArray(analysis.raw['topics']) &&
          (analysis.raw['topics'] as string[]).length > 0 && (
            <Text dimColor>{`  Topics: ${(analysis.raw['topics'] as string[]).join(', ')}`}</Text>
          )}
      </Box>
    </Box>
  );
}
