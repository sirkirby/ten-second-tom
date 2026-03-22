import { useEffect } from 'react';
import { useApp } from 'ink';

/**
 * Auto-exit the Ink app after a delay when the given condition is true.
 * Replaces the duplicated 5-second auto-exit useEffect found in
 * record, note, analyze, and setup commands.
 */
export function useAutoExit(shouldExit: boolean, delayMs: number = 5000) {
  const { exit } = useApp();
  useEffect(() => {
    if (shouldExit) {
      const timer = setTimeout(() => exit(), delayMs);
      return () => clearTimeout(timer);
    }
    return undefined;
  }, [shouldExit, delayMs, exit]);
}
