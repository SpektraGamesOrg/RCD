# Private Spektra Packages — Authentication Setup

The `com.spektragames.*` dependencies in `manifest.json` are pulled from the
**private** repo `SpektraGamesOrg/SpektraUnityPackagesProject` over HTTPS.

The URLs in `manifest.json` are intentionally **token-free**:

```
https://github.com/SpektraGamesOrg/SpektraUnityPackagesProject.git?path=/Packages/<PackageName>
```

Credentials are **never** committed. Each machine authenticates locally via a Git
credential helper using your own GitHub Personal Access Token (PAT). Do this once
per machine.

## 1. Create a PAT (once per developer)

GitHub → Settings → Developer settings → Personal access tokens.

- **Fine-grained** (preferred): scope it to `SpektraGamesOrg/SpektraUnityPackagesProject`,
  permission `Contents: Read-only`.
- **Classic**: `repo` scope.

Copy the token. You'll paste it as the **password** below.

## 2. Store the token in the OS credential helper

### Windows
Git for Windows ships with Git Credential Manager (GCM).

```powershell
git config --global credential.helper manager
# Triggers a one-time prompt — username = your GitHub login, password = the PAT:
git ls-remote https://github.com/SpektraGamesOrg/SpektraUnityPackagesProject.git
```

GCM caches it in **Windows Credential Manager**.

### macOS
Use the built-in keychain helper (or GCM if you have it installed).

```bash
git config --global credential.helper osxkeychain
# Triggers a one-time prompt — username = your GitHub login, password = the PAT:
git ls-remote https://github.com/SpektraGamesOrg/SpektraUnityPackagesProject.git
```

The PAT is stored in the **macOS Keychain**.

> Once `git ls-remote` succeeds without re-prompting, Unity's Package Manager will
> resolve the private packages automatically — UPM uses the same git + credential
> helper under the hood.

## 3. Resolve in Unity

If Unity is open, use **Window → Package Manager → ⟳** or restart the Editor so UPM
re-resolves with the stored credentials.

## Troubleshooting

- **`Password authentication is not supported`** — git found no cached credential (or
  a bad/expired one). Re-run the `git ls-remote` step and ensure you paste the **PAT**,
  not your GitHub account password.
- **`git` not found by Unity** — Unity needs `git` on `PATH`. Verify with `git --version`
  in a fresh terminal; on Windows, reinstall Git for Windows if needed.
- **Token expired** — PATs can have expiry dates. Generate a new one and re-run step 2;
  on Windows you may need to clear the old entry in Windows Credential Manager first.

## Do NOT

- Do **not** paste tokens back into `manifest.json` or `packages-lock.json`. GitHub
  Secret Scanning auto-revokes any PAT pushed to a repo, which is exactly what broke
  this before.
