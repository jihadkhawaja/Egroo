# One-time agent tasks

A one-time task is a private work item for an active agent that you own. The owner can create, view, list, or cancel their tasks. An agent in a channel or a friends list is governed by the separate conversation rules. Task state and results are stored by the server outside channel timelines and agent conversations.

The current implementation accepts and persists tasks in the queue. A hosted worker and Avalonia task controls are the next increments.

## API

All requests use the normal Egroo bearer token.

| Method and path | Behavior |
| --- | --- |
| `POST /api/v1/AgentTasks` | Create a queued task or return the task from an earlier identical request. |
| `GET /api/v1/AgentTasks?skip=0&take=20` | List the owner's tasks with the newest first. The maximum page size is 50. |
| `GET /api/v1/AgentTasks/{taskId}` | Read task status, timestamps, and any result or failure code. |
| `POST /api/v1/AgentTasks/{taskId}/cancel` | Cancel a queued or running task. Repeating a successful cancellation returns the same cancelled task. A completed or failed task returns HTTP 409 with its current state. |

Send `agentId`, a client-generated UUID called `requestId`, and a `prompt` to create a task. Prompts accept up to 8,192 characters after trimming surrounding whitespace. The first submission returns HTTP 201 and the task location. Reusing the same request ID with the same agent and trimmed prompt returns HTTP 200 and the original task. The server returns HTTP 409 if an owner reuses a request ID for different work. A task or agent outside the caller's permissions returns HTTP 404.

## Lifecycle and restart

The task status is `Queued`, `Running`, `Completed`, `Failed`, or `Cancelled`. The worker-facing queue can claim a task once and records an execution deadline. An inactive or removed agent leaves its queued task in the `Failed` state when the queue reaches it. A cancelled task keeps that state when a worker reports a late result.

Queued tasks remain available after server restart. The worker-facing recovery step marks expired running tasks `Failed` with the failure code `Interrupted`. The original request ID continues to refer to that task. An intentional rerun uses a new request ID.
