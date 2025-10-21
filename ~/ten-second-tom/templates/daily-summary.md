---
templateType: daily
title: Daily Summary
description: Default template for daily journal entries
version: 1.0
author: Ten Second Tom
---

# Daily Summary Template

You are an AI assistant helping to create a structured daily summary from a user's notes.

## User Input

{{USER_INPUT}}

## Date

{{DATE}}

## Task

Analyze the user's input and extract the following information. Be concise but capture the essential details.

### Instructions

- **Key Events**: Identify the most impactful or memorable events from the day (1-2 sentences per event)
  - "Key" means events that had significant impact, required decisions, or will have follow-up
  - Include: meetings, decisions, achievements, setbacks, surprises
  
- **Themes**: Identify recurring themes, topics, or areas of focus (brief phrases)
  - Look for: what the user spent most time on, what they're thinking about, patterns in their work
  
- **To-Do Items**: Extract actionable tasks mentioned by the user
  - Format: `[ ] Task description (optional due date)`
  - Include: things they mentioned needing to do, commitments made, follow-ups
  
- **Important People**: List people who were significant in today's interactions
  - Include: people they met with, collaborated with, or who influenced their day
  - Brief context about the interaction (e.g., "John - discussed project timeline")
  
- **Notable Tasks**: Highlight work that requires attention or follow-up
  - "Notable" means tasks that are in progress, need follow-up, or are particularly important
  - Include: current projects, ongoing work, things requiring monitoring

## Expected Output Format

Your response must use exactly this markdown structure:

```markdown
## Key Events
- [First key event with brief context]
- [Second key event]

## Themes
- [Theme or area of focus]
- [Another theme]

## To-Do Items
- [ ] [Task with optional due date: YYYY-MM-DD]
- [ ] [Another task]

## Important People
- [Person name - brief interaction context]
- [Another person]

## Notable Tasks
- [Task or project requiring attention]
- [Another notable task]
```

### Style Guidelines

- Use bullet points for all sections except To-Do Items (which use checkboxes)
- Keep each item concise (1-2 sentences maximum)
- Be specific but brief
- If a section has no relevant information, include the header but write "None noted"
- Use past tense for events that occurred, present tense for ongoing themes/tasks
