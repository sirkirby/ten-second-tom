import React from 'react';
import { Box, Text } from 'ink';
import type { EntryAnalysis } from '@ten-second-tom/core';

interface SentimentDisplayProps {
  analysis: EntryAnalysis;
}

function getSentimentColor(score: number): string {
  if (score > 0.2) return 'green';
  if (score < -0.2) return 'red';
  return 'yellow';
}

function formatScore(score: number): string {
  const sign = score >= 0 ? '+' : '';
  return `${sign}${score.toFixed(2)}`;
}

function formatConfidence(confidence: number): string {
  return `${Math.round(confidence * 100)}%`;
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
          <Text color={sentimentColor}>
            {sentiment.label}
          </Text>
          {` (${formatScore(sentiment.score)})`}
        </Text>

        <Text>{`Confidence: ${formatConfidence(sentiment.confidence)}`}</Text>

        <Text>{`Summary: ${summary}`}</Text>
      </Box>
    </Box>
  );
}
