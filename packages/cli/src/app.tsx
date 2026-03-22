import React from 'react';
import { Text } from 'ink';

interface AppProps {
  command: string;
}

export function App({ command }: AppProps) {
  return <Text>Ten-Second Tom v2.0 — {command}</Text>;
}
