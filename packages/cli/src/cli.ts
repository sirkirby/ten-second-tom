#!/usr/bin/env node
import { Command } from 'commander';

const program = new Command();

program
  .name('tom')
  .description('Ten-Second Tom — intelligence-first voice capture and analysis')
  .version('2.0.0');

// Commands will be registered here as they are built

program.parse();
