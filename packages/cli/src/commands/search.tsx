import React, { useState, useEffect } from 'react';
import { Box, Text, useApp, useInput } from 'ink';
import TextInput from 'ink-text-input';
import { Command } from 'commander';
import { render } from 'ink';
import { SearchService } from '@ten-second-tom/core';
import type { Entry } from '@ten-second-tom/core';
import { buildServicesFromConfig } from './record.js';
import { SearchResultsWithDetail } from '../components/SearchResults.js';
import { ErrorDisplay } from '../components/ErrorDisplay.js';
import { useAutoExit } from '../hooks/useAutoExit.js';
import { checkSetupComplete } from '../hooks/useSetupGuard.js';

// ---------------------------------------------------------------------------
// Pipeline (extracted for testability)
// ---------------------------------------------------------------------------

export interface SearchPipelineResult {
  entries: Entry[];
  error: string | null;
}

/**
 * Run the search pipeline: validate, build services, search.
 * Exported for testing.
 */
export async function runSearchPipeline(query: string): Promise<SearchPipelineResult> {
  const empty: SearchPipelineResult = { entries: [], error: null };

  if (!query.trim()) {
    return { ...empty, error: 'Search query is empty — nothing to search.' };
  }

  const guard = checkSetupComplete();
  if (!guard.ok) {
    return { ...empty, error: guard.error };
  }

  const { config, configManager } = guard;
  const services = buildServicesFromConfig(config, configManager);

  const searchService = new SearchService(services.storage, services.embedding);

  try {
    const entries = await searchService.search(query.trim());
    return { entries, error: null };
  } catch (err) {
    const msg = err instanceof Error ? err.message : String(err);
    return { entries: [], error: `Search failed: ${msg}` };
  } finally {
    services.storage.close();
  }
}

// ---------------------------------------------------------------------------
// React component
// ---------------------------------------------------------------------------

type Phase = 'input' | 'searching' | 'results' | 'error';

function SearchCommand() {
  const { exit } = useApp();

  const [phase, setPhase] = useState<Phase>('input');
  const [query, setQuery] = useState('');
  const [results, setResults] = useState<Entry[]>([]);
  const [error, setError] = useState<string | null>(null);

  // -------------------------------------------------------------------------
  // On mount: check setup guard
  // -------------------------------------------------------------------------
  useEffect(() => {
    const guard = checkSetupComplete();
    if (!guard.ok) {
      setError(guard.error);
      setPhase('error');
    }
  }, []);

  // -------------------------------------------------------------------------
  // Auto-exit after error
  // -------------------------------------------------------------------------
  useAutoExit(phase === 'error');

  // -------------------------------------------------------------------------
  // Keyboard: allow 'q' or Escape to exit from results phase
  // -------------------------------------------------------------------------
  useInput((input, key) => {
    if ((input === 'q' || key.escape) && (phase === 'results' || phase === 'error')) {
      exit();
    }
  });

  // -------------------------------------------------------------------------
  // Submit handler
  // -------------------------------------------------------------------------
  async function handleSearch(searchQuery: string) {
    if (!searchQuery.trim()) return;

    setPhase('searching');

    const result = await runSearchPipeline(searchQuery);

    if (result.error) {
      setError(result.error);
      setPhase('error');
      return;
    }

    setResults(result.entries);
    setPhase('results');
  }

  // -------------------------------------------------------------------------
  // Render
  // -------------------------------------------------------------------------
  if (phase === 'error') {
    return <ErrorDisplay message={error ?? 'Unknown error'} />;
  }

  if (phase === 'input') {
    return (
      <Box flexDirection="column" paddingY={1}>
        <Text bold>{'🔍 Search Entries'}</Text>
        <Text>Search your entries:</Text>
        <TextInput
          value={query}
          onChange={setQuery}
          onSubmit={(value) => void handleSearch(value)}
        />
      </Box>
    );
  }

  if (phase === 'searching') {
    return (
      <Box paddingY={1}>
        <Text color="cyan">Searching...</Text>
      </Box>
    );
  }

  // phase === 'results'
  return (
    <Box flexDirection="column" paddingY={1}>
      <SearchResultsWithDetail results={results} />
      <Box marginTop={1}>
        <Text dimColor>Press q or Esc to exit.</Text>
      </Box>
    </Box>
  );
}

// ---------------------------------------------------------------------------
// Commander command registration
// ---------------------------------------------------------------------------

export const searchCommand = new Command('search')
  .description('Search entries by meaning or keyword')
  .action(() => {
    render(<SearchCommand />);
  });
