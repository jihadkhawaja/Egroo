# Contributing

Thanks for contributing to Egroo. This guide is the shortest path from idea to pull request.

## Before you start

1. For anything larger than a small fix, open an issue first so the approach can be aligned early.
2. Read the [Code of Conduct](CODE_OF_CONDUCT.md).
3. Use the [Development Setup](../wiki/Development-Setup.md) guide if this is your first time running the project locally.

## Local workflow

1. Fork the repository and create a branch from `main`.
2. Build the solution:

	```bash
	dotnet build src/Egroo.slnx --configuration Debug
	```

3. Make focused changes that match the existing architecture and style.
4. Run the relevant tests before opening a pull request:

	```bash
	dotnet test src/Egroo.Server.Test/Egroo.Server.Test.csproj --verbosity normal
	```

5. Update docs when behavior, setup, or public APIs change.

If your change affects one of these areas, update the related docs in the same branch:

- architecture or runtime behavior: `README.md` and `wiki/Architecture.md`
- setup, configuration, or deployment: the relevant page in `wiki/`
- public API or SignalR behavior: `wiki/API-Documentation.md`
- contributor expectations: `docs/CONTRIBUTING.md` or `docs/pull_request_template.md`

If your change updates persisted entities, mappings, or migrations:

- add the EF Core migration
- make sure `DataContextModelSnapshot` is updated
- mention the migration explicitly in your pull request

## Pull requests

Keep pull requests small and reviewable.

Before submitting, check that:

- the branch is based on `main`
- the change solves one clear problem
- tests pass for the area you changed
- docs are updated if needed
- migrations are included if the schema changed
- the PR description explains the why, not just the diff

## Good Documentation Changes

Keep documentation changes practical:

- prefer instructions that match the files and commands in the repo today
- remove stale guidance instead of layering exceptions on top of it
- keep wording consistent across the README, wiki, and contributor docs
- when changing encryption behavior, be explicit about what happens on the server and what stays on the client device

## Need help?

- Use GitHub issues for bugs and feature requests.
- Refer to the [Getting Started](../wiki/Getting-Started.md) and [Development Setup](../wiki/Development-Setup.md) guides for environment setup.
