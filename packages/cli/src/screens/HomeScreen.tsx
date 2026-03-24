import React, { useState, useCallback } from 'react';
import { Box, Text } from 'ink';
import Spinner from 'ink-spinner';
import type { AppConfig, Entry, ServiceContainer } from '@ten-second-tom/core';
import { Prompt } from '../components/Prompt.js';
import { InlineSearchResults } from '../components/InlineSearchResults.js';
import { InlineEntryDetail } from '../components/InlineEntryDetail.js';
import { toErrorMessage } from '../utils/format.js';
import { APP_VERSION } from '../constants.js';
import { COMMANDS, findCommand } from '../commands/registry.js';
import type { AppContext, HistoryEntry } from '../commands/registry.js';

const DIVIDER = '───────────────────────────────────────';

interface HomeScreenProps {
  context: AppContext;
  config: AppConfig | null;
  entryCount: number;
}

/**
 * Derive a human-readable label for the LLM provider.
 */
function llmLabel(config: AppConfig): string {
  if (config.llm.provider === 'cloud') return 'Claude';
  return config.llm.modelId;
}

/**
 * Build the config summary line, e.g. "Claude · whisper · bge-m3 · 12 entries"
 */
function configSummary(config: AppConfig, entryCount: number): string {
  const parts: string[] = [llmLabel(config), 'whisper'];

  if (config.embedding.provider !== 'none') {
    parts.push(config.embedding.model);
  }

  const entryLabel = entryCount === 1 ? 'entry' : 'entries';
  parts.push(`${entryCount} ${entryLabel}`);

  return parts.join(' \u00B7 ');
}

// ---------------------------------------------------------------------------
// Reindex logic
// ---------------------------------------------------------------------------

async function reindexEntries(
  services: ServiceContainer,
  pushHistory: (entry: HistoryEntry) => void,
): Promise<void> {
  const entries = await services.storage.listEntries({ limit: 100_000 });

  if (entries.length === 0) {
    pushHistory({
      id: `reindex-empty-${Date.now()}`,
      content: <Text dimColor>No entries to re-index.</Text>,
    });
    return;
  }

  const available = await services.embedding.isAvailable();
  if (!available) {
    pushHistory({
      id: `reindex-unavailable-${Date.now()}`,
      content: <Text color="yellow">Embedding service unavailable. Check Ollama is running.</Text>,
    });
    return;
  }

  pushHistory({
    id: `reindex-start-${Date.now()}`,
    content: <Text dimColor>Re-indexing {entries.length} entries...</Text>,
  });

  let updated = 0;
  let failed = 0;
  for (const entry of entries) {
    try {
      const embedding = await services.embedding.embed(entry.content);
      await services.storage.updateEntryEmbedding(entry.id, embedding);
      updated++;
    } catch {
      failed++;
    }
  }

  pushHistory({
    id: `reindex-done-${Date.now()}`,
    content: (
      <Text color="green">
        ✓ Re-indexed {entries.length} entries ({updated} updated
        {failed > 0 ? `, ${failed} failed` : ''})
      </Text>
    ),
  });
}

export function HomeScreen({ context, config, entryCount }: HomeScreenProps) {
  const [searching, setSearching] = useState(false);
  const [lastSearchResults, setLastSearchResults] = useState<Entry[]>([]);

  // ---- /command handler ----
  const handleCommand = useCallback(
    (name: string, args: string) => {
      // Handle /reindex asynchronously — the registry stub exists only for discovery.
      if (name === 'reindex') {
        const svcs = context.services;
        if (!svcs) {
          context.pushHistory({
            id: `reindex-err-${Date.now()}`,
            content: (
              <Text color="yellow">
                Not configured. Run <Text color="green">/setup</Text> first.
              </Text>
            ),
          });
          return;
        }
        void reindexEntries(svcs, context.pushHistory);
        return;
      }

      const cmd = findCommand(name);
      if (cmd) {
        cmd.execute(args, context);
      } else {
        context.pushHistory({
          id: `unknown-${Date.now()}`,
          content: (
            <Text>
              Unknown command: /{name}. Type <Text color="green">/help</Text> for available
              commands.
            </Text>
          ),
        });
      }
    },
    [context],
  );

  // ---- inline search handler ----
  const handleSearch = useCallback(
    async (query: string) => {
      const svcs = context.services;
      if (!svcs) {
        context.pushHistory({
          id: `search-err-${Date.now()}`,
          content: (
            <Text color="yellow">
              Not configured. Run <Text color="green">/setup</Text> first.
            </Text>
          ),
        });
        return;
      }

      setSearching(true);

      try {
        const results = await svcs.search.search(query, 5);
        setLastSearchResults(results);
        context.pushHistory({
          id: `search-${Date.now()}`,
          content: <InlineSearchResults query={query} results={results} />,
        });
      } catch (err) {
        const msg = toErrorMessage(err);
        context.pushHistory({
          id: `search-err-${Date.now()}`,
          content: <Text color="red">Search failed: {msg}</Text>,
        });
      } finally {
        setSearching(false);
      }
    },
    [context],
  );

  // ---- expand result handler ----
  const handleExpandResult = useCallback(
    (index: number) => {
      if (lastSearchResults.length === 0) {
        context.pushHistory({
          id: `expand-err-${Date.now()}`,
          content: <Text dimColor>No search results. Type a query first.</Text>,
        });
        return;
      }

      const entry = lastSearchResults[index - 1]; // 1-indexed display
      if (!entry) {
        context.pushHistory({
          id: `expand-err-${Date.now()}`,
          content: (
            <Text dimColor>
              No result #{index}. Pick 1–{lastSearchResults.length}.
            </Text>
          ),
        });
        return;
      }

      context.pushHistory({
        id: `detail-${Date.now()}`,
        content: <InlineEntryDetail entry={entry} />,
      });
    },
    [context, lastSearchResults],
  );

  return (
    <Box flexDirection="column">
      <Text bold>Ten-Second Tom v{APP_VERSION}</Text>
      {config ? (
        <Text dimColor>{configSummary(config, entryCount)}</Text>
      ) : (
        <Text dimColor color="yellow">
          Not configured — type <Text color="green">/setup</Text> to get started
        </Text>
      )}
      <Text dimColor>{DIVIDER}</Text>
      {searching ? (
        <Box>
          <Text color="cyan">
            <Spinner type="dots" />
          </Text>
          <Text> Searching...</Text>
        </Box>
      ) : (
        <Prompt
          commands={COMMANDS}
          onCommand={handleCommand}
          onSearch={(query) => void handleSearch(query)}
          onExpandResult={handleExpandResult}
          hasSearchResults={lastSearchResults.length > 0}
        />
      )}
    </Box>
  );
}
