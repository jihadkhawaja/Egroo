#!/bin/bash

# Run from the repository root to publish the wiki sources to GitHub Wiki.

set -e

WIKI_REPO_URL="https://github.com/jihadkhawaja/Egroo.wiki.git"
TEMP_DIR="/tmp/egroo-wiki-deploy"
WIKI_FILES_DIR="$(pwd)/wiki"

echo "🚀 Deploying Egroo Wiki Documentation"
echo "======================================="

# Check if wiki files exist
if [ ! -d "$WIKI_FILES_DIR" ]; then
    echo "❌ Error: Wiki files directory not found at $WIKI_FILES_DIR"
    echo "   Make sure you're running this script from the repository root."
    exit 1
fi

# Clone or update the wiki repository
if [ -d "$TEMP_DIR" ]; then
    echo "📁 Cleaning up existing temporary directory..."
    rm -rf "$TEMP_DIR"
fi

echo "📥 Cloning wiki repository..."
git clone "$WIKI_REPO_URL" "$TEMP_DIR"

# Copy wiki files
echo "📋 Copying wiki files..."
cp -r "$WIKI_FILES_DIR"/* "$TEMP_DIR/"

# Navigate to wiki directory
cd "$TEMP_DIR"

# Check if there are changes
if git diff --quiet && git diff --cached --quiet; then
    echo "ℹ️  No changes to deploy."
    exit 0
fi

# Stage all changes
git add .

# Commit changes
echo "💾 Committing changes..."
git commit -m "Update wiki documentation

- Update comprehensive documentation
- Add Getting Started guide
- Add Installation instructions
- Add Configuration guide
- Add Development Setup guide
- Add Deployment guide
- Add API documentation
- Add Architecture overview
- Add Troubleshooting guide

Auto-deployed on $(date)"

# Push changes
echo "📤 Pushing to GitHub Wiki..."
git push origin master

# Cleanup
cd - > /dev/null
rm -rf "$TEMP_DIR"

echo ""
echo "✅ Wiki documentation deployed successfully!"
echo "🌐 View at: https://github.com/jihadkhawaja/Egroo/wiki"
echo ""
echo "📖 Available pages:"
echo "   • Home (overview and quick links)"
echo "   • Getting Started (quick setup)"
echo "   • Installation (detailed setup)"
echo "   • Configuration (settings and options)"
echo "   • Development Setup (contributor guide)"
echo "   • Deployment (production scenarios)"
echo "   • API Documentation (REST API + SignalR)"
echo "   • Architecture (technical overview)"
echo "   • Troubleshooting (common issues)"
