# Wiki Source

This folder contains the markdown source for the Egroo GitHub wiki.

## Page Purpose

| File | Purpose |
|---|---|
| `Home.md` | Landing page and navigation for the wiki |
| `Getting-Started.md` | Fastest path to a working local environment |
| `Installation.md` | Setup options and Docker caveats |
| `Configuration.md` | Runtime settings and environment overrides |
| `Development-Setup.md` | Contributor-focused local workflow |
| `Deployment.md` | Production planning and deployment notes |
| `API-Documentation.md` | REST and SignalR reference |
| `Architecture.md` | Technical architecture overview |
| `Troubleshooting.md` | Common setup and runtime issues |

## Editing Rules

When you update wiki content:

1. Treat the repository files in `wiki/` as the source of truth.
2. Keep setup steps aligned with the actual codebase and shipped files.
3. Prefer practical, newcomer-friendly instructions over exhaustive theory.
4. Remove stale steps instead of layering new steps on top of them.

## Publishing

The repository includes `scripts/deploy-wiki.sh` for syncing these files to the GitHub wiki.

If you publish manually, copy the markdown files from this folder into the wiki repository rather than editing the wiki first.