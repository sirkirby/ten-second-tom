import React, { useState, useEffect, useCallback } from 'react';
import { Box, Text, useInput } from 'ink';
import Spinner from 'ink-spinner';
import type { Entry } from '@ten-second-tom/core';
import { TranscriptBox } from '../components/TranscriptBox.js';
import { getSentimentColor, formatScore } from '../utils/sentiment.js';
import type { AppContext } from '../commands/registry.js';

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

const LIST_LIMIT = 20;
const BORDER_CHAR = '\u258E'; // ▎ left one-quarter block

// ---------------------------------------------------------------------------
// Props
// ---------------------------------------------------------------------------

export interface ListScreenProps {
  context: AppContext;
  filter?: 'notes' | 'recordings';
  onClose: () => void;
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

type Phase = 'loading' | 'results' | 'detail';

function formatShortDate(isoString: string): string {
  return new Date(isoString).toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
  });
}

function formatFullDate(isoString: string): string {
  return new Date(isoString).toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  });
}

function getExcerpt(content: string, maxLength = 60): string {
  const oneLine = content.replace(/\n/g, ' ');
  if (oneLine.length <= maxLength) return oneLine;
  return oneLine.slice(0, maxLength) + '...';
}

function formatConfidence(confidence: number): string {
  return `${Math.round(confidence * 100)}%`;
}

function filterLabel(filter: ListScreenProps['filter']): string {
  if (filter === 'notes') return 'notes';
  if (filter === 'recordings') return 'recordings';
  return 'entries';
}

// ---------------------------------------------------------------------------
// ListScreen
// ---------------------------------------------------------------------------

export function ListScreen({ context, filter, onClose }: ListScreenProps) {
  const [phase, setPhase] = useState<Phase>('loading');
  const [entries, setEntries] = useState<Entry[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [selectedIndex, setSelectedIndex] = useState(0);
  const [detailEntry, setDetailEntry] = useState<Entry | null>(null);
  const [error, setError] = useState<string | null>(null);

  // -------------------------------------------------------------------------
  // Load entries on mount
  // -------------------------------------------------------------------------
  useEffect(() => {
    const svcs = context.services;
    if (!svcs) {
      setError('Services not available. Run `tom setup` first.');
      setPhase('results');
      return;
    }

    const entryType: 'recording' | 'note' | undefined =
      filter === 'notes' ? 'note' : filter === 'recordings' ? 'recording' : undefined;

    void svcs.storage
      .listEntries({ limit: LIST_LIMIT, type: entryType })
      .then((loaded) => {
        setEntries(loaded);
        setTotalCount(loaded.length);
        setSelectedIndex(0);
        setPhase('results');
      })
      .catch((err: unknown) => {
        const msg = err instanceof Error ? err.message : String(err);
        setError(`Failed to load entries: ${msg}`);
        setEntries([]);
        setPhase('results');
      });
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
        if (phase === 'results' && entries.length > 0) {
          if (key.upArrow) {
            setSelectedIndex((prev) => (prev - 1 + entries.length) % entries.length);
          }
          if (key.downArrow) {
            setSelectedIndex((prev) => (prev + 1) % entries.length);
          }
          if (key.return) {
            const entry = entries[selectedIndex];
            if (entry) {
              setDetailEntry(entry);
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
      [phase, entries, selectedIndex, onClose],
    ),
  );

  // -------------------------------------------------------------------------
  // Render — Loading
  // -------------------------------------------------------------------------
  if (phase === 'loading') {
    return (
      <Box paddingY={1}>
        <Text color="cyan">
          <Spinner type="dots" />
        </Text>
        <Text> Loading entries...</Text>
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
  const label = filterLabel(filter);

  return (
    <Box flexDirection="column" gap={1}>
      {/* Header */}
      <Text bold>
        Recent {label}
        {totalCount > 0 && (
          <Text dimColor>
            {'  '}({totalCount} {totalCount === 1 ? label.replace(/s$/, '') : label})
          </Text>
        )}
      </Text>

      {error && (
        <Box paddingLeft={2}>
          <Text color="red">{error}</Text>
        </Box>
      )}

      {/* Entries or empty state */}
      {entries.length === 0 ? (
        <Box paddingLeft={2}>
          <Text dimColor>
            {error ? 'Esc to close' : `No ${label} yet. Try \u2018record\u2019 to create one.`}
          </Text>
        </Box>
      ) : (
        <Box flexDirection="column">
          {entries.map((entry, index) => {
            const isSelected = index === selectedIndex;
            const score = entry.analysis?.sentiment.score ?? 0;
            const borderColor = getSentimentColor(score);
            const dateStr = formatShortDate(entry.createdAt);
            const scoreStr = entry.analysis ? formatScore(score) : '     ';
            const excerpt = getExcerpt(entry.content);

            return (
              <Box key={entry.id} paddingLeft={2}>
                <Text color={borderColor}>{BORDER_CHAR} </Text>
                <Text bold={isSelected} inverse={isSelected}>
                  {dateStr.padEnd(7)} {scoreStr.padEnd(6)} {excerpt}
                </Text>
              </Box>
            );
          })}
        </Box>
      )}

      {/* Footer hint */}
      <Box paddingLeft={2}>
        <Text dimColor>
          {entries.length > 0
            ? '\u2191\u2193 navigate \u00B7 Enter to expand \u00B7 Esc to close'
            : 'Esc to close'}
        </Text>
      </Box>
    </Box>
  );
}
