import React from 'react';
import { Box, Text } from 'ink';
import type { SearchResult } from '@ten-second-tom/core';
import { formatShortDate, getExcerpt } from '../utils/format.js';

// ---------------------------------------------------------------------------
// InlineSearchResults — rendered into Static history after a search
// ---------------------------------------------------------------------------

const FILLED = '\u2588'; // █
const EMPTY = '\u2591'; // ░
const BAR_WIDTH = 5;

/**
 * Render a relevance bar like "████░" (5 chars wide).
 */
function relevanceBar(relevance: number): string {
  const filled = Math.round(relevance * BAR_WIDTH);
  return FILLED.repeat(filled) + EMPTY.repeat(BAR_WIDTH - filled);
}

interface InlineSearchResultsProps {
  query: string;
  results: SearchResult[];
}

export function InlineSearchResults({ query, results }: InlineSearchResultsProps) {
  if (results.length === 0) {
    return <Text dimColor>No results for &quot;{query}&quot;</Text>;
  }
  return (
    <Box flexDirection="column">
      <Text dimColor>
        Search: &quot;{query}&quot; ({results.length} {results.length === 1 ? 'result' : 'results'}
        {' \u00B7 type a number to expand'})
      </Text>
      {results.map(({ entry, relevance }, i) => {
        return (
          <Text key={entry.id}>
            <Text color="green" bold>
              {'  '}
              {i + 1}.{' '}
            </Text>
            <Text dimColor>{formatShortDate(entry.createdAt).padEnd(7)}</Text>
            <Text color="cyan"> {relevanceBar(relevance)}</Text>
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
