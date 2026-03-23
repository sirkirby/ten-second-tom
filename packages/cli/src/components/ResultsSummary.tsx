import React from 'react';
import { Box, Text } from 'ink';
import type { EntryAnalysis } from '@ten-second-tom/core';
import { TranscriptBox } from './TranscriptBox.js';
import { WarningList } from './WarningList.js';
import { getSentimentColor, formatScore } from '../utils/sentiment.js';

// ---------------------------------------------------------------------------
// Props
// ---------------------------------------------------------------------------

export interface ResultsSummaryProps {
  duration?: number; // recording duration in seconds (omit for notes)
  transcript: string;
  analysis: EntryAnalysis | null;
  warnings: string[];
  entryType: 'recording' | 'note';
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function formatDuration(seconds: number): string {
  const minutes = Math.floor(seconds / 60);
  const secs = seconds % 60;
  return `${minutes}:${String(secs).padStart(2, '0')}`;
}

function formatConfidence(confidence: number): string {
  return `${Math.round(confidence * 100)}%`;
}

// ---------------------------------------------------------------------------
// ResultsSummary
// ---------------------------------------------------------------------------

export function ResultsSummary({
  duration,
  transcript,
  analysis,
  warnings,
  entryType,
}: ResultsSummaryProps) {
  const typeLabel = entryType === 'recording' ? 'Recording' : 'Note';
  const durationSuffix = duration !== undefined ? ` (${formatDuration(duration)})` : '';

  return (
    <Box flexDirection="column">
      {/* Header */}
      <Text color="green" bold>
        {'\u2713'} {typeLabel} saved{durationSuffix}
      </Text>

      {/* Transcript */}
      {transcript.length > 0 && (
        <Box paddingLeft={2}>
          <TranscriptBox text={transcript} />
        </Box>
      )}

      {/* Analysis: sentiment + confidence + topics + context type */}
      {analysis !== null && (
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

          {/* AI-generated summary */}
          {analysis.summary.length > 0 && (
            <Text>
              <Text dimColor>Summary: </Text>
              <Text>{analysis.summary}</Text>
            </Text>
          )}

          {/* Topics and context type on one line */}
          {(hasTopics(analysis) || hasContextType(analysis)) && (
            <Text dimColor>
              {getTopics(analysis)}
              {hasTopics(analysis) && hasContextType(analysis) ? ' \u00B7 ' : ''}
              {getContextType(analysis)}
            </Text>
          )}
        </Box>
      )}

      {/* Warnings */}
      <WarningList warnings={warnings} />
    </Box>
  );
}

// ---------------------------------------------------------------------------
// Analysis field helpers
// ---------------------------------------------------------------------------

function hasTopics(analysis: EntryAnalysis): boolean {
  return Array.isArray(analysis.raw['topics']) && (analysis.raw['topics'] as string[]).length > 0;
}

function getTopics(analysis: EntryAnalysis): string {
  if (!hasTopics(analysis)) return '';
  return (analysis.raw['topics'] as string[]).join(', ');
}

function hasContextType(analysis: EntryAnalysis): boolean {
  return typeof analysis.raw['contextType'] === 'string' && analysis.raw['contextType'].length > 0;
}

function getContextType(analysis: EntryAnalysis): string {
  if (!hasContextType(analysis)) return '';
  return String(analysis.raw['contextType']);
}
