import React, { useState, useEffect, useCallback } from 'react';
import { Box, Text, useInput } from 'ink';
import Spinner from 'ink-spinner';
import type { Entry } from 'ten-second-tom-core';
import { EntryDetail } from '../components/EntryDetail.js';
import { getSentimentColor, formatScore } from '../utils/sentiment.js';
import { formatShortDate, getExcerpt, toErrorMessage } from '../utils/format.js';
import { BORDER_CHAR } from '../constants.js';
import type { AppContext } from '../commands/registry.js';

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

const LIST_LIMIT = 20;

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

function renderOneShotEntries(label: string, loadedEntries: Entry[]): React.ReactNode {
  return (
    <Box flexDirection="column">
      <Text>
        Recent {label} ({loadedEntries.length})
      </Text>
      {loadedEntries.map((entry) => (
        <Text key={entry.id}>
          {formatShortDate(entry.createdAt)} {entry.id} {getExcerpt(entry.content)}
        </Text>
      ))}
    </Box>
  );
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
  const stdinSupportsInput = process.stdin.isTTY === true;

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
        if (context.oneShot) {
          context.pushHistory({
            id: `list-result-${Date.now()}`,
            content: renderOneShotEntries(filterLabel(filter), loaded),
          });
          onClose();
        }
      })
      .catch((err: unknown) => {
        setError(`Failed to load entries: ${toErrorMessage(err)}`);
        setEntries([]);
        setPhase('results');
        if (context.oneShot) {
          context.pushHistory({
            id: `list-err-${Date.now()}`,
            content: <Text color="red">Failed to load entries: {toErrorMessage(err)}</Text>,
          });
          onClose();
        }
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
    { isActive: stdinSupportsInput && !context.oneShot },
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
    return (
      <Box flexDirection="column" gap={1}>
        <EntryDetail entry={detailEntry} />
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
