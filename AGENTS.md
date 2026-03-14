# Egroo Agents Guide

This file defines project-wide guidance for AI agents and contributors working in Egroo.

## Purpose

Egroo is a real-time chat system. Work should preserve fast message flow, readable chat UI, and clear separation between user actions, agent actions, and transport concerns.

## Architecture Expectations

- Keep UI behavior in `src/Egroo.UI` and transport/service logic in `src/jihadkhawaja.chat.client`.
- Keep server-side persistence and authorization in `src/Egroo.Server`.
- Keep shared models/contracts in `src/jihadkhawaja.chat.shared`.
- Keep SignalR hub logic split by concern in `src/jihadkhawaja.chat.server/Hubs` partial classes.

## Communication Model

- Human users and AI agents both appear in channel timelines.
- User-authored messages must remain visually distinct from agent-authored messages.
- Agent mentions trigger agent workflows only when the target is a configured channel agent.
- User mentions are a presentation and composition feature unless a backend feature explicitly depends on them.
- Typing indicators should stay lightweight, optimistic, and easy to clear when a real message arrives.

## Chat UX Rules

- Prioritize legibility over decorative styling.
- Sender bubbles must maintain high contrast for text, links, mentions, timestamps, and status icons.
- Mention pills should read as semantic tokens, not plain text.
- Links in messages must be clickable and readable in both sender and receiver bubbles.
- File sharing should produce durable links and avoid pushing large payloads through SignalR messages.

## Agent Behavior Rules

- Agents should feel like channel participants, not system logs.
- Agent output should stream or appear with typing feedback when practical.
- Preserve the distinction between agent identity and sender identity in shared message models.
- Do not couple mention rendering to agent execution logic.

## Editing Rules

- Prefer focused changes over broad rewrites.
- Preserve existing MudBlazor patterns unless there is a clear UX issue.
- When changing chat rendering, verify sender bubble, receiver bubble, and agent bubble contrast separately.
- When adding new HTTP endpoints, follow the minimal API grouping pattern under `/api/v1/{Feature}`.
- When changing persisted EF Core entities or schema mappings, add a migration and update the database before considering the work complete.

## Verification

- Build the full solution after cross-project changes.
- Run `src/Egroo.Server.Test` when server behavior changes.
- If UI rendering changes, verify both sender and receiver states for the affected component.
- If a schema change was made, ensure new migration files exist under `src/Egroo.Server/Migrations/` and run the database update script.