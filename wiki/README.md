# Egroo Wiki Documentation

This directory contains the complete wiki documentation for Egroo. These files are designed to be copied to the GitHub Wiki for the project.

## 📁 File Structure

- **`Home.md`** - Main wiki homepage with overview and navigation
- **`Getting-Started.md`** - Quick start guide for new users
- **`Installation.md`** - Comprehensive installation instructions
- **`Configuration.md`** - Configuration options and settings
- **`Development-Setup.md`** - Setup guide for developers and contributors
- **`Deployment.md`** - Production deployment scenarios and guides
- **`API-Documentation.md`** - Complete API reference including SignalR
- **`Architecture.md`** - Technical architecture and design patterns
- **`Troubleshooting.md`** - Common issues and their solutions

## 🚀 Deploying to GitHub Wiki

### Option 1: Using the Deploy Script (Recommended)

Run the provided deployment script from the repository root:

```bash
./scripts/deploy-wiki.sh
```

This script will:
- Clone the GitHub Wiki repository
- Copy all wiki files
- Commit and push the changes
- Clean up temporary files

### Option 2: Manual Deployment

1. **Clone the wiki repository:**
   ```bash
   git clone https://github.com/jihadkhawaja/Egroo.wiki.git
   cd Egroo.wiki
   ```

2. **Copy the wiki files:**
   ```bash
   cp /path/to/Egroo/wiki/* .
   ```

3. **Commit and push:**
   ```bash
   git add .
   git commit -m "Update wiki documentation"
   git push origin master
   ```

### Option 3: Manual Copy via GitHub Web Interface

1. Navigate to the [GitHub Wiki](https://github.com/jihadkhawaja/Egroo/wiki)
2. Click "Edit" on existing pages or "New Page" for new ones
3. Copy the content from each `.md` file
4. Save the changes

## 📖 Wiki Navigation Structure

The wiki is organized with the following navigation flow:

```
Home (Landing Page)
├── Getting Started (Quick setup)
│   ├── Prerequisites
│   ├── Quick Start with Docker
│   └── Manual Setup
├── Installation (Detailed setup)
│   ├── Docker Compose Method
│   ├── Manual Installation
│   └── Pre-built Images
├── Configuration (Settings)
│   ├── Server Configuration
│   ├── Client Configuration
│   ├── Docker Configuration
│   └── Security Settings
├── Development Setup (Contributors)
│   ├── Environment Setup
│   ├── IDE Configuration
│   ├── Testing
│   └── Contributing Workflow
├── Deployment (Production)
│   ├── Docker Deployment
│   ├── Kubernetes
│   ├── Cloud Platforms
│   └── Traditional Hosting
├── API Documentation (Reference)
│   ├── Authentication
│   ├── REST Endpoints
│   ├── SignalR Hubs
│   └── WebSocket Events
├── Architecture (Technical Details)
│   ├── System Overview
│   ├── Component Design
│   ├── Data Flow
│   └── Security Architecture
└── Troubleshooting (Problem Solving)
    ├── Installation Issues
    ├── Runtime Problems
    ├── Performance Issues
    └── Getting Help
```

## 🔄 Updating Documentation

When updating the wiki documentation:

1. **Edit the markdown files** in this directory
2. **Test the changes** locally if possible
3. **Run the deployment script** or manually copy files
4. **Verify the changes** on the GitHub Wiki

## 📝 Content Guidelines

### Writing Style
- Use clear, concise language
- Include practical examples and code snippets
- Provide step-by-step instructions
- Add troubleshooting tips where relevant

### Formatting
- Use proper markdown headers (H1, H2, H3, etc.)
- Include code blocks with syntax highlighting
- Use emojis sparingly for visual organization
- Add links between related wiki pages

### Code Examples
- Provide complete, working examples
- Include error handling where appropriate
- Show both successful and error scenarios
- Use realistic configuration values

## 🤝 Contributing to Documentation

To contribute to the wiki documentation:

1. **Fork the main repository**
2. **Edit files in the `wiki/` directory**
3. **Test your changes thoroughly**
4. **Submit a pull request**
5. **Wiki will be updated after PR merge**

## 📧 Support

If you need help with the documentation:

- **Issues**: [GitHub Issues](https://github.com/jihadkhawaja/Egroo/issues)
- **Discord**: [Community Discord](https://discord.gg/9KMAM2RKVC)
- **Email**: Contact the maintainers

---

**Note**: The GitHub Wiki is automatically generated from these files. Always edit the source files in this directory rather than editing the wiki directly to ensure changes are preserved.