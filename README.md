# [Egroo](https://www.egroo.org/)

<img src="https://raw.githubusercontent.com/jihadkhawaja/Egroo/refs/heads/main/docs/icon.png" alt="Egroo Icon" width="128"/>

A **self-hosted**, **real-time** chat web application built using **Blazor** and **ASP.NET**.

## 🚀 Build Status

[![NuGets Push](https://github.com/jihadkhawaja/Egroo/actions/workflows/Nuget.yml/badge.svg)](https://github.com/jihadkhawaja/Egroo/actions/workflows/Nuget.yml)
[![MSTest](https://github.com/jihadkhawaja/Egroo/actions/workflows/MSTest.yml/badge.svg)](https://github.com/jihadkhawaja/Egroo/actions/workflows/MSTest.yml)
[![Docker](https://github.com/jihadkhawaja/Egroo/actions/workflows/Docker.yml/badge.svg)](https://github.com/jihadkhawaja/Egroo/actions/workflows/Docker.yml)
[![Chat Deploy](https://github.com/jihadkhawaja/Egroo/actions/workflows/Deploy-Chat.yml/badge.svg)](https://github.com/jihadkhawaja/Egroo/actions/workflows/Deploy-Chat.yml)
[![CodeQL](https://github.com/jihadkhawaja/Egroo/actions/workflows/github-code-scanning/codeql/badge.svg)](https://github.com/jihadkhawaja/Egroo/actions/workflows/github-code-scanning/codeql)

## ✨ Features

- **Blazor Auto Mode**: 
  - Loads server-side for faster initial page load, then seamlessly switches to WebAssembly (WASM) when cached.
- **Progressive Web App (PWA)**: 
  - Installable on devices for an app-like experience.
- **Real-time Communication**: 
  - Built with SignalR for fast, responsive messaging.
- **Channel Voice Calls**: 
  - Secure, peer-to-peer mesh network WebRTC voice calls within channels.
- **Message Privacy**: 
  - Messages are automatically deleted after delivery, ensuring confidentiality.
- **AI Agents in Channels**:
  - Create personal AI agents powered by your own LLM provider (OpenAI, Azure OpenAI, Anthropic, Ollama).
  - Publish agents so other users can discover and add them as friends.
  - Add agents to channels — they respond automatically when @mentioned.
  - Supports names with spaces via `@<Agent Name>` mention syntax.
  - Agents have access to built-in tools including the ability to search, friend, and add other agents to channels.
- **Self-hosted Infrastructure**: 
  - Full control over your data with a customizable backend.

## 💬 How Messaging Works

The three NuGet packages (`jihadkhawaja.chat.client`, `jihadkhawaja.chat.server`, `jihadkhawaja.chat.shared`) work together every time a message is sent:

```mermaid
sequenceDiagram
    actor User
    participant UI as Egroo.UI<br/>(Razor Component)
    participant Client as jihadkhawaja.chat.client<br/>(NuGet)
    participant Hub as jihadkhawaja.chat.server<br/>(NuGet · ChatHub)
    participant Repo as Egroo.Server<br/>(Repository / DB)
    participant RecipientClient as jihadkhawaja.chat.client<br/>(Recipient · NuGet)
    participant RecipientUI as Egroo.UI<br/>(Recipient)

    User->>UI: Types message and hits Send
    UI->>Client: SendMessage(message)
    Note over Client: ChatMessageService<br/>wraps HubConnection.InvokeAsync
    Client-->>Hub: SignalR · SendMessage(message)<br/>over WebSocket
    Hub->>Hub: Validate sender & channel membership
    Hub->>Repo: Save message metadata (no content)
    Hub->>Repo: Encrypt & store content in UserPendingMessages<br/>for each recipient
    Repo-->>Hub: Saved ✓

    loop For each online recipient
        Hub-->>RecipientClient: SignalR · ReceiveMessage(message)
        RecipientClient->>RecipientUI: Trigger OnMessageReceived event
        RecipientUI->>User: Message displayed in chat
        RecipientClient-->>Hub: UpdatePendingMessage(messageId)
        Hub->>Repo: Delete UserPendingMessage record
    end

    Note over Repo: Content is never stored<br/>permanently — only until delivered
```

## 🤖 How Agent Mentions Work

When an agent is added to a channel, it listens for `@mentions` and replies in real time. The response is delivered via the same SignalR pipeline as regular messages.

```mermaid
sequenceDiagram
    actor User
    participant UI as Egroo.UI
    participant Hub as ChatHub
    participant Responder as AgentChannelService
    participant LLM as LLM Provider
    participant Channel as Channel Members

    User->>UI: Types @AgentName hello!
    UI->>Hub: SignalR · SendMessage(message)
    Hub->>Hub: Save & broadcast to members
    Hub->>Responder: ProcessMentionsAsync(message)
    Note over Responder: Detects @AgentName or @<Agent Name>
    Responder->>Responder: Load last 20 messages as context
    Responder->>LLM: Run agent with instructions + context
    LLM-->>Responder: Agent response text
    Responder->>Hub: Broadcast agent reply via IHubContext
    Hub-->>Channel: SignalR · ReceiveMessage(agentMessage)
    Note over Channel: Message shows agent name + bot icon
```

**Key points:**
- Agent replies are fire-and-forget — `SendMessage` returns immediately; the agent response arrives as a follow-up `ReceiveMessage` event.
- Any agent in a channel can be mentioned using `@AgentName` (single word) or `@<Agent Name>` (names with spaces).
- Only agents that are **Active** and **assigned to the channel** will respond.
- The last 20 channel messages are included as conversation context.

---

## 📞 How Channel Voice Calls Work

Voice calls in channels are powered by a **WebRTC Mesh Network**, where every participant establishes a secure peer-to-peer connection with everyone else in the call.

```mermaid
sequenceDiagram
  actor A as Participant A
  participant H as SignalR Hub
  actor B as Participant B

  A->>H: JoinChannelCall(channelId)
  H-->>A: ChannelCallParticipantsChanged([A])
    
  B->>H: JoinChannelCall(channelId)
  H-->>A: ChannelCallParticipantsChanged([A, B])
  H-->>B: ChannelCallParticipantsChanged([A, B])

  Note over B: New joiner creates WebRTC offers<br/>for existing participants
  B->>H: SendOfferToUser(A, offerSdp)
  H-->>A: ReceiveOffer(B, offerSdp)

  A->>H: SendAnswerToUser(B, answerSdp)
  H-->>B: ReceiveAnswer(A, answerSdp)

  Note over A,B: Exchange ICE Candidates via Hub

  B->>H: SendIceCandidateToUser(A, candidate)
  H-->>A: ReceiveIceCandidate(B, candidate)

  Note over A,B: P2P Encrypted Audio Stream Established
```

## 📋 Prerequisites

- **.NET 10** (required) for the latest features and optimizations.
- **Browser**: Any modern browser with WebAssembly support.

## 📚 Documentation

Comprehensive guides and setup instructions are available in our [Wiki](https://github.com/jihadkhawaja/Egroo/wiki):

- **[Getting Started](https://github.com/jihadkhawaja/Egroo/wiki/Getting-Started)** - Quick setup guide
- **[Installation Guide](https://github.com/jihadkhawaja/Egroo/wiki/Installation)** - Detailed installation instructions
- **[Configuration](https://github.com/jihadkhawaja/Egroo/wiki/Configuration)** - Configuration options and settings
- **[Development Setup](https://github.com/jihadkhawaja/Egroo/wiki/Development-Setup)** - Setup for contributors
- **[Deployment Guide](https://github.com/jihadkhawaja/Egroo/wiki/Deployment)** - Production deployment scenarios
- **[API Documentation](https://github.com/jihadkhawaja/Egroo/wiki/API-Documentation)** - REST API and SignalR reference
- **[Architecture Overview](https://github.com/jihadkhawaja/Egroo/wiki/Architecture)** - Technical architecture details
- **[Troubleshooting](https://github.com/jihadkhawaja/Egroo/wiki/Troubleshooting)** - Common issues and solutions

## 📸 Screenshots

### Friends List
<img src="https://raw.githubusercontent.com/jihadkhawaja/Egroo/refs/heads/main/docs/egroo_docs_friends.jpg" alt="Friends" height="300" />

### Channels
<img src="https://raw.githubusercontent.com/jihadkhawaja/Egroo/refs/heads/main/docs/egroo_docs_channels.jpg" alt="Channels" height="300" />

### Conversations
<img src="https://raw.githubusercontent.com/jihadkhawaja/Egroo/refs/heads/main/docs/egroo_docs_channel.jpg" alt="Conversations" height="300" />

### Responsive
<div>
  <img src="https://raw.githubusercontent.com/jihadkhawaja/Egroo/refs/heads/main/docs/egroo_docs_small_screen_channels.jpg" alt="Small Screen Channels" height="300" />
  <img src="https://raw.githubusercontent.com/jihadkhawaja/Egroo/refs/heads/main/docs/egroo_docs_small_screen_channel.jpg" alt="Small Screen Channel" height="300" />
</div>

## 🤝 Contribution

Contributions are welcome! To get started:

- Fork the repository and submit pull requests.
- Report bugs or request features via the [Issues](https://github.com/jihadkhawaja/Egroo/issues) tab.

## 🌐 Community

Join the discussion on our **[Discord Server](https://discord.gg/9KMAM2RKVC)** to connect, share ideas, and get help.

## 📄 License

This project is licensed under the [**MIT License**](https://github.com/jihadkhawaja/Egroo/blob/main/LICENSE).
