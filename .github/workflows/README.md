# Wiki Deployment

This directory contains the automated deployment setup for the Egroo wiki documentation.

## Automated Deployment

The wiki documentation is automatically deployed via GitHub Actions whenever changes are made to files in the `wiki/` directory.

### Workflow: `.github/workflows/deploy-wiki.yml`

**Triggers:**
- Push to `main` branch with changes in `wiki/**` files
- Pull request merged to `main` branch with changes in `wiki/**` files
- Manual workflow dispatch

**Process:**
1. Checks out the repository
2. Validates wiki files exist
3. Clones the wiki repository
4. Copies updated files
5. Commits and pushes changes to the wiki

### Manual Deployment

If you need to deploy manually, you can:

1. **Use GitHub Actions UI:**
   - Go to Actions tab in GitHub
   - Select "Deploy Wiki Documentation" workflow
   - Click "Run workflow"

2. **Use the shell script (legacy):**
   ```bash
   ./scripts/deploy-wiki.sh
   ```

## Wiki Structure

The wiki files are located in the `wiki/` directory:

```
wiki/
├── Home.md                    # Wiki homepage
├── Getting-Started.md         # Quick setup guide
├── Installation.md           # Detailed installation
├── Configuration.md          # Configuration options
├── Development-Setup.md      # Development environment
├── Deployment.md            # Production deployment
├── API-Documentation.md     # API reference
├── Architecture.md          # Technical architecture
└── Troubleshooting.md       # Common issues
```

## Contributing to Documentation

1. Edit files in the `wiki/` directory
2. Commit and push to `main` branch
3. The deployment will happen automatically
4. Check the Actions tab for deployment status
5. View the updated wiki at: https://github.com/jihadkhawaja/Egroo/wiki

## Troubleshooting

If the deployment fails:

1. Check the GitHub Actions logs
2. Ensure the wiki repository exists (visit the wiki page once to create it)
3. Verify repository permissions allow wiki access
4. Make sure wiki files are valid Markdown

For manual intervention, use the shell script in `scripts/deploy-wiki.sh`.
