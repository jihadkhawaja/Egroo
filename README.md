# [Egroo](https://www.egroo.org/)

<img src="https://raw.githubusercontent.com/jihadkhawaja/Egroo/refs/heads/main/docs/icon.png" alt="Egroo Icon" width="128"/>

Egroo is a self-hosted real-time chat platform built with Blazor, ASP.NET Core, SignalR, and PostgreSQL. It is designed for teams that want modern chat, voice, and AI-assisted collaboration without giving up control of their infrastructure or data.

It combines a Blazor web experience, a SignalR-first real-time backend, ephemeral encrypted message delivery, channel voice calls over WebRTC, and optional AI agents that can participate in conversations when mentioned.

## Build Status

[![NuGets Push](https://github.com/jihadkhawaja/Egroo/actions/workflows/Nuget.yml/badge.svg)](https://github.com/jihadkhawaja/Egroo/actions/workflows/Nuget.yml)
[![MSTest](https://github.com/jihadkhawaja/Egroo/actions/workflows/MSTest.yml/badge.svg)](https://github.com/jihadkhawaja/Egroo/actions/workflows/MSTest.yml)
[![Docker](https://github.com/jihadkhawaja/Egroo/actions/workflows/Docker.yml/badge.svg)](https://github.com/jihadkhawaja/Egroo/actions/workflows/Docker.yml)
[![Chat Deploy](https://github.com/jihadkhawaja/Egroo/actions/workflows/Deploy-Chat.yml/badge.svg)](https://github.com/jihadkhawaja/Egroo/actions/workflows/Deploy-Chat.yml)
[![CodeQL](https://github.com/jihadkhawaja/Egroo/actions/workflows/github-code-scanning/codeql/badge.svg)](https://github.com/jihadkhawaja/Egroo/actions/workflows/github-code-scanning/codeql)

## Why Egroo

- Self-hosted architecture with PostgreSQL storage and no third-party message relay.
- Blazor Auto rendering for fast first load with WebAssembly after hydration.
- SignalR-based real-time messaging and presence.
- Ephemeral encrypted message delivery for stronger privacy.
- WebRTC channel voice calls.
- AI agents that can be created, published, added to channels, and triggered with mentions.

## Core Capabilities

| Capability | What it means |
|---|---|
| Real-time chat | Fast message delivery over SignalR WebSockets |
| Privacy-first delivery | Message content is encrypted and stored only until recipients receive it |
| Voice channels | Peer-to-peer channel audio using WebRTC signaling through the hub |
| Blazor UI | Shared Razor components across server and WebAssembly experiences |
| AI agents | User-owned agents powered by OpenAI, Azure OpenAI, Anthropic, or Ollama |
| Self-hosting | Full control over deployment, configuration, and data |

## System Overview

At a high level, Egroo separates the web host, the API server, shared UI libraries, chat libraries, and database storage.

```mermaid
flowchart TD
  Browser["Browser / PWA"]
  Host["Egroo Host - Blazor Auto host"]
  UI["Egroo.UI - Shared Razor components"]
  Client["Egroo.Client - WASM client project"]
  Api["Egroo.Server - Minimal API + SignalR"]
  ChatClient["jihadkhawaja.chat.client - Client chat services"]
  ChatServer["jihadkhawaja.chat.server - ChatHub + connection tracking"]
  Shared["jihadkhawaja.chat.shared - Shared models and interfaces"]
  Db[("PostgreSQL")]

  Browser --> Host
  Host --> UI
  Host --> Client
  Browser --> Api
  UI --> ChatClient
  ChatClient --> Shared
  Api --> ChatServer
  Api --> Shared
  ChatServer --> Shared
  Api --> Db
```

## Solution Structure

The solution in `src/` is split by responsibility so the UI, transport layer, shared contracts, and server implementation remain independent.

```mermaid
flowchart LR
  Egroo["Egroo/Egroo - Blazor host"] --> UI["Egroo.UI"]
  EgrooClient["Egroo.Client - WASM client"] --> UI
  UI --> ChatClient["jihadkhawaja.chat.client"]
  ChatClient --> Shared["jihadkhawaja.chat.shared"]
  EgrooServer["Egroo.Server - API + DB + repositories"] --> ChatServer["jihadkhawaja.chat.server"]
  ChatServer --> Shared
  EgrooServer --> Shared
  Tests["Egroo.Server.Test"] --> EgrooServer
```

| Project | Purpose |
|---|---|
| `src/Egroo/Egroo` | Blazor host application that serves the web app and coordinates SSR to WASM rendering |
| `src/Egroo/Egroo.Client` | Client-side WebAssembly project |
| `src/Egroo.UI` | Shared Razor component library used by both hosting modes |
| `src/Egroo.Server` | ASP.NET Core backend with Minimal APIs, SignalR, repositories, EF Core, and PostgreSQL |
| `src/jihadkhawaja.chat.client` | Client services that wrap auth, channel, message, and call interactions |
| `src/jihadkhawaja.chat.server` | SignalR hub implementation and connection tracking |
| `src/jihadkhawaja.chat.shared` | Shared DTOs, models, and interfaces |
| `src/Egroo.Server.Test` | MSTest coverage for server behavior |

## How Messaging Works

One of Egroo's core design choices is that message content is not kept permanently in the main message record. The server stores metadata, encrypts per-recipient content temporarily, and removes pending content after acknowledgment.

```mermaid
sequenceDiagram
  actor User
  participant UI as Egroo.UI
  participant Client as chat.client
  participant Hub as ChatHub
  participant Repo as Server Repository
  participant Recipient as Recipient Client

  User->>UI: Send message
  UI->>Client: SendMessage(message)
  Client->>Hub: SignalR invocation
  Hub->>Repo: Save metadata
  Hub->>Repo: Encrypt and store pending content
  Hub-->>Recipient: ReceiveMessage(message)
  Recipient-->>Hub: UpdatePendingMessage(messageId)
  Hub->>Repo: Delete pending content
```

## How Voice Channel Calls Work

Voice calls in channels use a WebRTC mesh network. SignalR manages membership and relays WebRTC signaling, while the audio stream stays peer-to-peer between participants.

```mermaid
sequenceDiagram
  actor A as Participant A
  participant Hub as SignalR Hub
  actor B as Participant B

  A->>Hub: JoinChannelCall(channelId)
  Hub-->>A: ChannelCallParticipantsChanged([A])

  B->>Hub: JoinChannelCall(channelId)
  Hub-->>A: ChannelCallParticipantsChanged([A, B])
  Hub-->>B: ChannelCallParticipantsChanged([A, B])

  Note over B: New participant creates WebRTC offers for existing members
  B->>Hub: SendOfferToUser(A, offerSdp)
  Hub-->>A: ReceiveOffer(B, offerSdp)

  A->>Hub: SendAnswerToUser(B, answerSdp)
  Hub-->>B: ReceiveAnswer(A, answerSdp)

  Note over A,B: ICE candidates are exchanged through the hub
  B->>Hub: SendIceCandidateToUser(A, candidate)
  Hub-->>A: ReceiveIceCandidate(B, candidate)

  Note over A,B: Peer-to-peer encrypted audio stream established
```

## AI Agents in Channels

Egroo supports personal AI agents that can be attached to channels and respond when mentioned. Agents can use supported LLM providers and participate as first-class actors in the chat experience.

```mermaid
sequenceDiagram
  actor User
  participant Hub as ChatHub
  participant AgentService as AgentChannelService
  participant LLM as LLM Provider
  participant Channel as Channel Members

  User->>Hub: Send @AgentName message
  Hub->>AgentService: Detect mention and load context
  AgentService->>LLM: Generate reply
  LLM-->>AgentService: Response text
  AgentService-->>Channel: Broadcast agent reply
```

## Where To Start

The README is the entry point. The wiki is where installation, deployment, and deeper technical details live.

| If you want to... | Go here |
|---|---|
| Get the app running quickly | [Getting Started](https://github.com/jihadkhawaja/Egroo/wiki/Getting-Started) |
| Install with more detail | [Installation Guide](https://github.com/jihadkhawaja/Egroo/wiki/Installation) |
| Configure database, JWT, encryption, and CORS | [Configuration](https://github.com/jihadkhawaja/Egroo/wiki/Configuration) |
| Set up a contributor workstation | [Development Setup](https://github.com/jihadkhawaja/Egroo/wiki/Development-Setup) |
| Deploy to production | [Deployment Guide](https://github.com/jihadkhawaja/Egroo/wiki/Deployment) |
| Understand the backend and runtime design | [Architecture Overview](https://github.com/jihadkhawaja/Egroo/wiki/Architecture) |
| Explore API and SignalR surface area | [API Documentation](https://github.com/jihadkhawaja/Egroo/wiki/API-Documentation) |
| Debug common setup issues | [Troubleshooting](https://github.com/jihadkhawaja/Egroo/wiki/Troubleshooting) |

## Local Development At A Glance

For contributors, the normal loop is:

1. Install .NET 10 and PostgreSQL.
2. Configure `src/Egroo.Server/appsettings.Development.json` with your database, JWT secret, and encryption values.
3. Start the API from `src/Egroo.Server`.
4. Start the web host from `src/Egroo/Egroo`.
5. Run tests from `src/Egroo.Server.Test` or the solution root.

Common commands:

```bash
dotnet build src/Egroo.slnx --configuration Debug
dotnet test src/Egroo.Server.Test/Egroo.Server.Test.csproj --verbosity normal
dotnet watch --project src/Egroo.Server/Egroo.Server.csproj
dotnet watch --project src/Egroo/Egroo/Egroo.csproj
```

For full environment setup, use the [Development Setup](https://github.com/jihadkhawaja/Egroo/wiki/Development-Setup) guide.

## Contributing

Contributions should be small, reviewable, and aligned with the existing architecture.

Before opening a pull request:

1. Read the [Contributing Guide](https://github.com/jihadkhawaja/Egroo/blob/main/docs/CONTRIBUTING.md).
2. Check the [Code of Conduct](https://github.com/jihadkhawaja/Egroo/blob/main/docs/CODE_OF_CONDUCT.md).
3. Discuss larger changes through an issue first.
4. Keep documentation updated when behavior or setup changes.
5. Run tests before submitting your branch.

## Community

- GitHub issues: [Report bugs or request features](https://github.com/jihadkhawaja/Egroo/issues)
- Discord: [Join the community server](https://discord.gg/9KMAM2RKVC)

## License

Egroo is licensed under the [Apache License 2.0](https://github.com/jihadkhawaja/Egroo/blob/main/LICENSE).
