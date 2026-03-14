# Egroo Skills Guide

This file captures the practical skills and conventions needed to work effectively in this repository.

## UI And Design Skill

- Use MudBlazor components first.
- Favor deliberate contrast and readable hierarchy over subtle low-contrast effects.
- Keep the existing orange-accented dark visual language unless a feature explicitly introduces a scoped variant.
- Chat surfaces must remain readable on desktop and mobile widths.
- New CSS should support both regular message bubbles and current-user message bubbles.

## Chat Composition Skill

- Channel composition supports plain text, mentions, links, and file links.
- Mention autocomplete should support both channel agents and channel users when the UI permits it.
- Mention formatting is a rendering concern; backend message content should stay plain and portable.
- Raw URLs should render as hyperlinks without requiring users to manually write markdown.

## Realtime Skill

- Use `ChatMessageService` as the client event hub for message and typing events.
- Avoid wiring `HubConnection.On(...)` directly in view components when a service abstraction already exists.
- Clear typing state when a participant sends a message.

## Agent Skill

- Channel agents are configured server-side and can respond to mentions.
- Agent typing state should be broadcast separately from user typing state.
- Agent display names should remain stable from generation through rendering.

## File Sharing Skill

- Files should be uploaded over HTTP, not embedded directly into SignalR payloads.
- Shared file messages should use durable links that work as normal clickable anchors.
- Keep upload limits explicit and user-facing.

## Database Schema Skill

- When a persisted entity, owned type, relationship, index, or EF mapping changes, add a migration.
- After adding the migration, update the database using the repository scripts.
- Do not leave `src/Egroo.Server` schema changes without matching migration files and snapshot updates.
- Use `.github/skills/ef-core-migrations/SKILL.md` for the detailed workflow.

## Documentation Skill

- Update `.github/copilot-instructions.md` for durable workspace guidance.
- Use `AGENTS.md` for repository-wide agent workflow expectations.
- Use `SKILLS.md` for implementation patterns, design preferences, and feature-specific working rules.