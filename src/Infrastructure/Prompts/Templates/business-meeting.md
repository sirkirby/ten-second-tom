---
templateType: business-meeting
title: Business Meeting Summary
description: Template for multi-speaker meeting summarization with structured output
version: 1.0
author: Ten Second Tom
---

# Business Meeting Summary Template

You are an AI assistant helping to create a structured summary from a multi-speaker business meeting transcript.

## Transcript

{{TRANSCRIPT}}

## Task

Analyze the meeting transcript and extract key information. Focus on actionable items, decisions, and important discussion points.

### Instructions

- **Meeting Topics**: Identify the main subjects or agenda items discussed (brief phrases or sentences)
  - Include: primary agenda items, major discussion themes, key subjects covered
  - List in the order they were discussed when possible

- **Key Decisions**: Extract concrete decisions that were made during the meeting
  - Include: commitments made, courses of action agreed upon, resolutions reached
  - Note who made the decision if clearly identifiable
  - Format: Brief statement of the decision with context

- **Action Items**: List specific tasks assigned or volunteered for
  - Format: `[ ] Task description (Responsible party if identifiable)`
  - Include: commitments to complete work, follow-up items, deliverables
  - Note deadlines if mentioned

- **Discussion Points**: Summarize important points raised during discussions
  - Include: key arguments, concerns raised, questions asked, insights shared
  - Capture differing viewpoints if present
  - Focus on substantive content, not small talk

- **Participants**: Identify speakers and attendees mentioned in the transcript
  - List names or roles as they appear in the transcript
  - Include context about their participation if significant (e.g., "presented quarterly results")
  - Infer from context clues like speaker attribution, names mentioned, or role references

## Expected Output Format

Format your response using this structure:

## Meeting Topics

- [First topic or agenda item]
- [Second topic]
- [Additional topics as needed]

## Key Decisions

- [Decision with brief context and decision maker if known]
- [Another decision]

## Action Items

- [ ] [Task description] (Responsible party if identifiable)
- [ ] [Another task] (Due date if mentioned: YYYY-MM-DD)

## Discussion Points

- [Important point raised, concern, or insight]
- [Another discussion point]
- [Additional points as needed]

## Participants

- [Person name or role - context about their participation if significant]
- [Another participant]

### Style Guidelines

- Use bullet points for all sections except Action Items (which use checkboxes)
- Keep each item focused and concise (1-3 sentences maximum)
- Be specific about decisions and action items
- If a section has no relevant information, include the header but write "None noted"
- Use past tense for completed discussions and decisions
- Preserve speaker attribution when important for context
- Focus on business content; omit greetings, small talk, and meeting logistics unless significant
