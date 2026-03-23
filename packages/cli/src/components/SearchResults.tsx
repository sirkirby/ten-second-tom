import React, { useState, useMemo } from 'react';
import { Box, Text, useInput } from 'ink';
import SelectInput from 'ink-select-input';
import type { Entry } from '@ten-second-tom/core';
import { getSentimentColor, getSentimentEmoji, formatScore } from '../utils/sentiment.js';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function formatDate(isoString: string): string {
  return new Date(isoString).toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  });
}

function formatDateTime(isoString: string): string {
  return new Date(isoString).toLocaleString('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
    hour12: true,
  });
}

function getTypeEmoji(type: Entry['type']): string {
  return type === 'recording' ? '\u{1F4F9}' : '\u{1F4DD}';
}

function getExcerpt(content: string, maxLength = 100): string {
  if (content.length <= maxLength) return content;
  return content.slice(0, maxLength) + '...';
}

// ---------------------------------------------------------------------------
// SelectInput item type
// ---------------------------------------------------------------------------

interface SelectItem {
  label: string;
  value: string;
}

// ---------------------------------------------------------------------------
// Detail view
// ---------------------------------------------------------------------------

interface EntryDetailProps {
  entry: Entry;
  onBack: () => void;
}

function EntryDetail({ entry, onBack }: EntryDetailProps) {
  useInput((_input, key) => {
    if (key.return) {
      onBack();
    }
  });

  return (
    <Box flexDirection="column" paddingY={1}>
      <Text bold>{`\u{1F4C4} Full Entry \u2014 ${formatDate(entry.createdAt)}`}</Text>

      <Box paddingLeft={2} marginTop={1} flexDirection="column">
        <Text>{entry.content}</Text>
      </Box>

      {entry.analysis && (
        <Box marginTop={1} paddingLeft={2} flexDirection="column">
          <Text bold>
            {'📊 Sentiment: '}
            <Text color={getSentimentColor(entry.analysis.sentiment.score)}>
              {entry.analysis.sentiment.label}
            </Text>
            {` \u2014 ${entry.analysis.summary} (${formatScore(entry.analysis.sentiment.score)})`}
          </Text>
        </Box>
      )}

      <Box marginTop={1}>
        <Text dimColor>{'[Enter] Back to results'}</Text>
      </Box>
    </Box>
  );
}

// ---------------------------------------------------------------------------
// SearchResults
// ---------------------------------------------------------------------------

export interface SearchResultsProps {
  results: Entry[];
  onSelect: (entry: Entry) => void;
}

export function SearchResults({ results, onSelect }: SearchResultsProps) {
  if (results.length === 0) {
    return (
      <Box paddingY={1}>
        <Text dimColor>No results found.</Text>
      </Box>
    );
  }

  const items = useMemo<SelectItem[]>(
    () =>
      results.map((entry) => {
        const typeEmoji = getTypeEmoji(entry.type);
        const dateStr = formatDateTime(entry.createdAt);
        const sentimentPart = entry.analysis
          ? `  ${getSentimentEmoji(entry.analysis.sentiment.score)}  `
          : '  ';
        const excerpt = getExcerpt(entry.content);
        return {
          label: `${typeEmoji} ${dateStr}${sentimentPart}${excerpt}`,
          value: entry.id,
        };
      }),
    [results],
  );

  function handleSelect(item: SelectItem) {
    const entry = results.find((e) => e.id === item.value);
    if (entry) onSelect(entry);
  }

  return (
    <Box flexDirection="column" paddingY={1}>
      <Text bold>{`\u{1F50D} Search Results (${results.length} found)`}</Text>
      <Box marginTop={1}>
        <SelectInput items={items} onSelect={handleSelect} />
      </Box>
    </Box>
  );
}

// ---------------------------------------------------------------------------
// SearchResultsWithDetail — composite component managing results + detail view
// ---------------------------------------------------------------------------

interface SearchResultsWithDetailProps {
  results: Entry[];
}

export function SearchResultsWithDetail({ results }: SearchResultsWithDetailProps) {
  const [selectedEntry, setSelectedEntry] = useState<Entry | null>(null);

  if (selectedEntry) {
    return <EntryDetail entry={selectedEntry} onBack={() => setSelectedEntry(null)} />;
  }

  return <SearchResults results={results} onSelect={setSelectedEntry} />;
}
