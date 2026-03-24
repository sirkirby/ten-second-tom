import React, { useState, useEffect, useCallback } from 'react';
import { Box, Text, useInput } from 'ink';
import TextInput from 'ink-text-input';
import Spinner from 'ink-spinner';
import type { Entry, SearchResult } from '@ten-second-tom/core';
import { TranscriptBox } from '../components/TranscriptBox.js';
import { getSentimentColor, formatScore } from '../utils/sentiment.js';
import {
  formatShortDate,
  formatFullDate,
  getExcerpt,
  formatConfidence,
  toErrorMessage,
  relevanceBar,
} from '../utils/format.js';
import { BORDER_CHAR } from '../constants.js';
import type { AppContext } from '../commands/registry.js';

// ---------------------------------------------------------------------------
// Props
// ---------------------------------------------------------------------------

export interface SearchScreenProps {
  context: AppContext;
  initialQuery?: string;
  onClose: () => void;
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

type Phase = 'input' | 'searching' | 'results' | 'detail';

// ---------------------------------------------------------------------------
// SearchScreen
// ---------------------------------------------------------------------------

export function SearchScreen({ context, initialQuery, onClose }: SearchScreenProps) {
  const [phase, setPhase] = useState<Phase>(initialQuery ? 'searching' : 'input');
  const [query, setQuery] = useState(initialQuery ?? '');
  const [results, setResults] = useState<SearchResult[]>([]);
  const [selectedIndex, setSelectedIndex] = useState(0);
  const [detailEntry, setDetailEntry] = useState<Entry | null>(null);
  const [error, setError] = useState<string | null>(null);

  // -------------------------------------------------------------------------
  // Search execution
  // -------------------------------------------------------------------------
  const executeSearch = useCallback(
    async (searchQuery: string) => {
      if (!searchQuery.trim()) return;

      setPhase('searching');

      const svcs = context.services;
      if (!svcs) {
        setError('Services not available. Run `tom setup` first.');
        setPhase('input');
        return;
      }

      try {
        const searchResults = await svcs.search.search(searchQuery.trim());
        setResults(searchResults);
        setSelectedIndex(0);
        setPhase('results');
      } catch (err) {
        setError(`Search failed: ${toErrorMessage(err)}`);
        setPhase('results');
        setResults([]);
      }
    },
    [context.services],
  );

  // -------------------------------------------------------------------------
  // Run initial query on mount
  // -------------------------------------------------------------------------
  useEffect(() => {
    if (initialQuery) {
      void executeSearch(initialQuery);
    }
    // Run only on mount — initialQuery is a static prop
  }, []);

  // -------------------------------------------------------------------------
  // Keyboard handling
  // -------------------------------------------------------------------------
  useInput(
    useCallback(
      (
        _input: string,
        key: { upArrow?: boolean; downArrow?: boolean; return?: boolean; escape?: boolean },
      ) => {
        if (phase === 'results' && results.length > 0) {
          if (key.upArrow) {
            setSelectedIndex((prev) => (prev - 1 + results.length) % results.length);
          }
          if (key.downArrow) {
            setSelectedIndex((prev) => (prev + 1) % results.length);
          }
          if (key.return) {
            const result = results[selectedIndex];
            if (result) {
              setDetailEntry(result.entry);
              setPhase('detail');
            }
          }
        }

        if (key.escape) {
          if (phase === 'detail') {
            setDetailEntry(null);
            setPhase('results');
          } else {
            onClose();
          }
        }
      },
      [phase, results, selectedIndex, onClose],
    ),
  );

  // -------------------------------------------------------------------------
  // Submit handler for text input
  // -------------------------------------------------------------------------
  const handleSubmit = useCallback(
    (value: string) => {
      if (value.trim()) {
        void executeSearch(value);
      }
    },
    [executeSearch],
  );

  // -------------------------------------------------------------------------
  // Render — Query input
  // -------------------------------------------------------------------------
  if (phase === 'input') {
    return (
      <Box flexDirection="column" gap={1}>
        <Box>
          <Text bold>Search: </Text>
          <TextInput value={query} onChange={setQuery} onSubmit={handleSubmit} />
        </Box>
        {error && (
          <Box paddingLeft={2}>
            <Text color="red">{error}</Text>
          </Box>
        )}
        <Box paddingLeft={2}>
          <Text dimColor>Type a query and press Enter</Text>
        </Box>
      </Box>
    );
  }

  // -------------------------------------------------------------------------
  // Render — Searching
  // -------------------------------------------------------------------------
  if (phase === 'searching') {
    return (
      <Box paddingY={1}>
        <Text color="cyan">
          <Spinner type="dots" />
        </Text>
        <Text> Searching...</Text>
      </Box>
    );
  }

  // -------------------------------------------------------------------------
  // Render — Detail view
  // -------------------------------------------------------------------------
  if (phase === 'detail' && detailEntry) {
    const analysis = detailEntry.analysis;
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
        <Text bold>Entry — {formatFullDate(detailEntry.createdAt)}</Text>

        <Box paddingLeft={2}>
          <TranscriptBox text={detailEntry.content} />
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

        <Box paddingLeft={2}>
          <Text dimColor>Esc to go back</Text>
        </Box>
      </Box>
    );
  }

  // -------------------------------------------------------------------------
  // Render — Results list
  // -------------------------------------------------------------------------
  const resultCount = results.length;

  return (
    <Box flexDirection="column" gap={1}>
      {/* Header */}
      <Text>
        <Text bold>Search: </Text>
        <Text>&quot;{query}&quot;</Text>
        <Text dimColor>
          {'  '}({resultCount} {resultCount === 1 ? 'result' : 'results'})
        </Text>
      </Text>

      {error && (
        <Box paddingLeft={2}>
          <Text color="yellow">{error}</Text>
        </Box>
      )}

      {/* Results or empty state */}
      {resultCount === 0 ? (
        <Box paddingLeft={2}>
          <Text dimColor>No entries found</Text>
        </Box>
      ) : (
        <Box flexDirection="column">
          {results.map(({ entry, relevance }, index) => {
            const isSelected = index === selectedIndex;
            const score = entry.analysis?.sentiment?.score ?? 0;
            const borderColor = getSentimentColor(score);
            const dateStr = formatShortDate(entry.createdAt);
            const relBar = relevanceBar(relevance);
            const excerpt = getExcerpt(entry.content);

            return (
              <Box key={entry.id} paddingLeft={2}>
                <Text color={borderColor}>{BORDER_CHAR} </Text>
                <Text bold={isSelected} inverse={isSelected}>
                  {dateStr.padEnd(7)}
                </Text>
                <Text color="cyan"> {relBar} </Text>
                <Text bold={isSelected} inverse={isSelected}>
                  {' '}
                  {excerpt}
                </Text>
              </Box>
            );
          })}
        </Box>
      )}

      {/* Footer hint */}
      <Box paddingLeft={2}>
        <Text dimColor>
          {resultCount > 0
            ? '\u2191\u2193 navigate \u00B7 Enter to expand \u00B7 Esc to close'
            : 'Esc to close'}
        </Text>
      </Box>
    </Box>
  );
}
