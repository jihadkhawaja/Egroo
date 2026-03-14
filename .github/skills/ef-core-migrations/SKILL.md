---
name: ef-core-migrations
description: 'Handle EF Core schema changes in Egroo. Use for: adding or changing entity properties, owned types, relationships, indexes, table mappings, naming conventions, or anything in DataContext/OnModelCreating that changes the PostgreSQL schema. Covers when to add a migration, how to update the database, and what to verify afterwards.'
argument-hint: 'Describe the schema change (e.g. "add user public key columns", "new channel index", "rename owned type column")'
---

# EF Core Migrations — Egroo Schema Workflow

## When to Use

- Adding, removing, or renaming entity properties that are persisted to the database
- Changing `User`, `Channel`, `Message`, agent, or other EF Core entity mappings
- Modifying `DataContext`, `OnModelCreating`, owned entities, indexes, constraints, or relationships
- Updating table or column naming behavior
- Any change where the runtime model and the database schema would otherwise drift

---

## Required Rule

If a change affects the EF Core schema, you must:

1. Add a migration for the change.
2. Update the database with that migration.
3. Verify the solution still builds and the relevant tests still pass.

Do not leave entity/model changes without a matching migration.

---

## Commands

Run from the repository root:

```powershell
.\scripts\add-migration.ps1 "<MigrationName>"
.\scripts\update-database.ps1
```

Migrations target `src/Egroo.Server`.

---

## What Counts As A Schema Change

- New persisted properties on shared/server entities
- Removed persisted properties
- Property type changes
- New `DbSet<>` entries
- Relationship changes (`HasOne`, `HasMany`, FKs, join tables)
- Index changes
- Owned type changes
- Table/column naming changes
- Required/optional changes that affect generated columns or constraints

These do not usually require a migration by themselves:

- `[NotMapped]` properties only
- Pure UI/view-model changes
- Comments, logging, or behavior-only service changes

---

## Verification Checklist

- `dotnet build src/Egroo.slnx --configuration Debug`
- `dotnet test src/Egroo.Server.Test/Egroo.Server.Test.csproj --verbosity normal` when server behavior changed
- Confirm the migration files are present under `src/Egroo.Server/Migrations/`
- Confirm `DataContextModelSnapshot` reflects the new schema

---

## Egroo-Specific Notes

- The repo uses lowercase naming conventions. Expect generated database names to end up lowercase.
- Prefer the provided PowerShell scripts instead of ad hoc EF commands so the right project/startup wiring is used.
- If a change touches shared models but should not persist, keep it `[NotMapped]` or otherwise excluded from the EF model.