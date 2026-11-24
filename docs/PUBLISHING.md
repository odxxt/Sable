# Publishing Sable and its module catalog

The app source and signed module catalog share the public repository [odxxt/Sable](https://github.com/odxxt/Sable). A source push updates code and documentation. **It does not create a GitHub Release or update an existing downloadable ZIP.**

## Push source changes

Work from a checkout of this repository. Review the diff and run checks appropriate to the code changes; a documentation-only edit needs link/content checks rather than another complete remote-session test.

```sh
git status
git diff --check
git diff
git fetch origin
```

Confirm what will be included. Keep vault data, recovery keys, publisher private keys, credentials, build output and scratch fixtures out of Git. Stage the intended paths, inspect the staged diff, commit and push without force:

```sh
git add <reviewed-paths>
git diff --cached --check
git diff --cached --stat
git commit -m "Describe the update"
git push origin main
```

If the remote has advanced, integrate and verify its changes before pushing. Use normal Git Credential Manager authentication; never put tokens in scripts, command arguments, chat or package files. The repository does not currently have a GitHub Actions release workflow, so pushing main alone does not build or publish assets.

## Build and publish an app download

1. Set the executable version and release notes consistently. The current source identifies as **0.9.7 Preview**. Review README, user/security/architecture/module guides and verification evidence for the same behavior.
2. On Windows with .NET 10 SDK, run `.\build.ps1 -Portable`. This runs core and native module checks and publishes to `artifacts/Sable-portable`. Record environment-dependent failures accurately; do not substitute historical passing counts for the current build.
3. Exercise the relevant Debug UI fixture with fictional data in a **fresh disposable directory**, never a real vault. For current starter/window-mode checks use `--preview --preview-starters --data-dir <fresh-directory>`. Test hooks are excluded from Release. Record manual Windows/DPI/remote-session gaps in VERIFICATION.md.
4. Build the portable ZIP with the **entire** self-contained publish output, licenses/notices, current documentation and public module artifacts. Verify it launches without an external .NET installation. Do not distribute just `Sable.exe` or a framework-dependent build.
5. Prepare a source ZIP from the exact committed tree, excluding Git metadata, bin/obj, data directories and private material. Name assets consistently: `Sable-v<version>-win-x64.zip`, `Sable-v<version>-source.zip`, `SHA256SUMS-v<version>.txt`. For this release, substitute `0.9.7` throughout.
6. Compute SHA-256 checksums for both ZIPs and inspect their entries for missing files and unwanted private data. Ensure source, executable metadata, release tag and documentation describe the same build.
7. Create a version tag on that source commit, push main and that tag without force, and create a GitHub **prerelease** with those three assets and concise change/upgrade notes. Verify the uploaded assets and checksums. Never silently replace a published version with different bytes; use a new version for a new release.

Keep earlier releases available for rollback. Current platform packaging is standalone Windows x64; installer and ARM64 packages are not shipped. A source-only update can precede a release, but README must not imply that unuploaded assets are already downloadable.

The maintainer explicitly requested the 0.9.7 spacing refresh under the same version. Its release notes disclose the replacement, and its source commit and SHA-256 checksums identify the refreshed build. Existing 0.9.7 users must download the ZIP again. This exception does not change the normal policy of assigning a new version to future releases.

## Make modules appear in the app

**Settings → Plugins & modules → GitHub catalog** fetches the signed catalog on demand. **Install from ZIP…** supports previously downloaded packages. Keep module ZIPs out of the standalone app download. The signed packages stay in the source repository for download:

[Official catalog](https://raw.githubusercontent.com/odxxt/Sable/main/modules/catalog-v2.json)

Each entry specifies an allowed repository ZIP URL and SHA-256. The catalog payload and each official manifest are independently ECDSA-signed against the embedded publisher key. The ZIP hash must also match. Editing signed bytes without re-signing invalidates the catalog/package.

Existing signed payloads can contain `odxxt/SableLatch` URLs. The host verifies those exact bytes first, then maps only that old official prefix to `odxxt/Sable`. Do not edit signed URLs solely for branding. Stable `sablelatch.*` module IDs, embedded resource names and crypto purpose strings likewise remain intentional compatibility identifiers.

For a new official module:

1. Implement/test any required host capability in an app version first; API 1 ZIPs cannot supply arbitrary code or invent a new protocol handler.
2. Define a supported manifest. Preserve its schema or use a new ID for an incompatible change.
3. Sign the exact manifest with `modules/Sign-Official.ps1`, then pack with `modules/Pack-Module.ps1`.
4. Update the catalog payload with name, description, allowed URL and ZIP SHA-256. Sign the exact payload bytes and place base64 payload/signature in `catalog-v2.json`. Keep `catalog.json` compatible with older releases that cannot read newer host capabilities.
5. Verify signatures, archives and downloads; commit only public artifacts and push. The next explicit catalog check sees the change. No separate server is needed.

Installed enabled `putty`/`rdp` modules automatically appear under **New entry → Connection**; field modules get individual starters. UI/docs-only host changes do not require re-signing unchanged packages. Editing the source sample README does not modify an already packed ZIP; repacking is a separate package update because it changes its archive hash.

Keep publisher private keys and unlock passwords outside Git and release ZIPs, backed up separately. The repository contains public verification material only. See [module authoring](../modules/README.md), [security boundaries](../SECURITY.md) and [verification](../VERIFICATION.md).
