---
title: Config and Hosting
status: active
owner: @codex
priority: high
complexity: 2
created: 2026-04-26
updated: 2026-05-22
tags: [feature, config, hosting]
---

# Config and Hosting

## Summary

The app is configured through the `NugsDownloader` settings section and the deployment environment.

## Current Behavior

- Storage and persistence settings are bound from the app configuration.
- CLI-style Go flags are no longer part of the runtime.
- The web host uses the configured URL and port from the deployment environment or launch settings.
- Runtime state and data-protection keys are stored beneath the configured state directory.

## Legacy JSON account migration

Hermes Kanban: `t_4e46427e`.

Before the web host starts queue workers or serves requests, SQLite storage is
initialized and attempts to import `credentials.json` plus `secrets.json` from
`NugsDownloader__StateDirectory` into `nugs-downloader.db`. Direct repository
users get the same migration on the first operation of each store instance.

- The source files are read-only inputs: no rewrites, renames, deletions, or
  migration markers. Their original bytes remain unchanged.
- Only complete account/secret pairs are imported. IDs, provider/label,
  username, secret reference, credential kind, authentication state and last
  verification timestamp are preserved. Migration performs **no live auth**
  and never promotes an account to authenticated.
- Inputs match the legacy C# serializer, not arbitrary exported JSON: account
  arrays use case-sensitive PascalCase properties and numeric enums; secrets
  are an object mapping exact references to strings. Missing `CredentialKind`
  defaults to Password; missing `AuthState` defaults to Unknown and missing
  `LastVerifiedAt` to null. ID, provider, label, username (may be empty for a
  token), and secret reference must be present and valid.
- Only nonempty `protected:` secret values are imported. The complete protected
  string is copied verbatim, without decrypting or re-encrypting. Unreferenced
  secrets and accounts lacking matching protected material are not imported.
  Pre-protection plaintext JSON is deliberately not imported; sign in again.
- SQLite `secret_material.StorageFormat` explicitly distinguishes `plaintext`
  from `protected`. Schema upgrades default existing rows to `plaintext` without
  inspecting their contents. Imported rows are marked `protected` in the same
  transaction as their account/secret inserts. Regular vault writes, including
  updates of migrated secrets, explicitly set `plaintext`; passwords beginning
  with `protected:` remain literal passwords. Only rows marked `protected` are
  decrypted, and unreadable protected material never falls back to ciphertext.
- Duplicate account IDs, provider/label pairs, or secret references cause all
  competing valid legacy accounts to be skipped. Duplicate object properties
  invalidate that account row; duplicate secret keys invalidate the secret
  document. Missing/unreadable files, malformed documents, wrong root shapes,
  and invalid/incomplete account rows are skipped without logging input values
  or parser exceptions. Other valid, unambiguous pairs can still import.
- Existing SQLite rows always win. Any account ID, provider/label, account
  secret-reference, or secret-material reference conflict skips the entire
  incoming pair, even if one existing side is missing or the values look equal.
  Migration does not repair pre-existing half pairs or change existing data.
- All eligible inserts share one immediate SQLite transaction. Database errors
  roll back the whole batch and stop startup with a generic, secret-free error;
  correct storage issues and restart to retry. No partial account/secret pairs
  are committed. Cancellation also rolls back uncommitted inserts.
- The migration checks again on restart; it does not overwrite newer SQLite
  values. There is no deletion tombstone: if both imported SQLite rows are later
  removed while legacy files remain, they can be imported again on restart.

### Operator checklist and limitations

- [ ] Keep the original `data-protection-keys` directory and the same application
  identity (`NugsDownloader`) with the state directory. Protected material can
  migrate without keys, but cannot be used until the original keys are restored;
  alternatively sign in again. The vault decrypts only when a credential is
  requested, using the legacy `NugsDownloader.SecretVault.v1` purpose.
- [ ] Stop legacy writers before migration. The two JSON files are separate
  snapshots, not a coordinated cross-file export; concurrent JSON edits are not
  supported. The importer does not validate protected payload authenticity.
- [ ] Check saved accounts in the UI. Skipped inputs are intentionally quiet;
  no secret-bearing diagnostic dump is produced. Resolve ambiguous input
  offline or sign in again rather than deleting existing SQLite rows to force
  an overwrite.
- [ ] After verifying a migration, an operator may separately archive the old
  JSON files securely to prevent later re-import. The application never does
  this automatically. Back up SQLite and protection keys together.
- [ ] Jobs and file-state JSON are not migrated by this account-only feature.
  New SQLite secret writes retain the existing SQLite vault behavior; this
  change is not an at-rest encryption redesign.

## Checklist

- [x] Document the storage/config section used by the web host
- [x] Keep the deployment stack and the app configuration aligned
- [x] Replace the old Go CLI expectations with the current C# host model
