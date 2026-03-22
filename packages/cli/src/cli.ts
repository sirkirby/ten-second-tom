#!/usr/bin/env node
import { Command } from 'commander';
import { setupCommand } from './commands/setup.js';
import { recordCommand } from './commands/record.js';
import { noteCommand } from './commands/note.js';

const program = new Command();

program
  .name('tom')
  .description('Ten-Second Tom — intelligence-first voice capture and analysis')
  .version('2.0.0');

program.addCommand(setupCommand);
program.addCommand(recordCommand);
program.addCommand(noteCommand);

program.parse();
