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
        Search: &quot;{query}&quot; ({results.length} {results.length === 1 ? 'result' : 'results'}
        {' · type a number to expand'})
      </Text>
      {results.map((entry, i) => {
        const sentiment = entry.analysis?.sentiment;
        return (
          <Text key={entry.id}>
            <Text color="green" bold>
              {'  '}
              {i + 1}.{' '}
            </Text>
            <Text dimColor>{formatShortDate(entry.createdAt).padEnd(7)}</Text>
            {sentiment ? (
              <Text color={getSentimentColor(sentiment.score)}>
                {' '}
                {formatScore(sentiment.score)}
              </Text>
            ) : (
              <Text dimColor> {'     '}</Text>
            )}
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
