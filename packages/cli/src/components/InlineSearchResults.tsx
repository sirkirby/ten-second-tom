import React from 'react';
import { Box, Text } from 'ink';
import type { Entry } from '@ten-second-tom/core';
import { getSentimentColor, formatScore } from '../utils/sentiment.js';
import { formatShortDate, getExcerpt } from '../utils/format.js';

// ---------------------------------------------------------------------------
// InlineSearchResults — rendered into Static history after a search
// ---------------------------------------------------------------------------

interface InlineSearchResultsProps {
  query: string;
  results: Entry[];
}

export function InlineSearchResults({ query, results }: InlineSearchResultsProps) {
  if (results.length === 0) {
    return <Text dimColor>No results for &quot;{query}&quot;</Text>;
  }
  return (
    <Box flexDirection="column">
      <Text dimColor>
        Search: &quot;{query}&quot; ({results.length} {results.length === 1 ? 'result' : 'results'})
      </Text>
      {results.map((entry, i) => {
        const score = entry.analysis?.sentiment?.score ?? 0;
        return (
          <Text key={entry.id}>
            <Text dimColor>
              {'  '}
              {i + 1}.{' '}
            </Text>
            <Text dimColor>{formatShortDate(entry.createdAt).padEnd(7)}</Text>
            <Text color={getSentimentColor(score)}> {formatScore(score)}</Text>
            <Text>
              {'  '}
              {getExcerpt(entry.content)}
            </Text>
          </Text>
        );
      })}
    </Box>
  );
}
