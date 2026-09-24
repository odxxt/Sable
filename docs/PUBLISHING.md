# Publishing SableLatch and its module catalog

The app and module catalog use the same public repository: `odxxt/SableLatch`.

## Push source changes

From this repository in Git Bash or PowerShell:

```sh
git status
git push origin main
```

Use your normal Windows terminal if an automation session cannot access Git Credential Manager. Do not put GitHub tokens in source, scripts, command arguments, chat, or module packages.

## Publish an app download

A source push does not upload the standalone app to Releases. Build and verify the portable ZIP, source ZIP and SHA256SUMS file, commit the corresponding source, and create a matching version tag. Push main and the tag without force, then create a GitHub prerelease with those three files as assets. The prepared local Publish-SableLatch script automates those steps after local preparation and verifies remote asset hashes before publishing. Existing published assets are not overwritten.

## Make modules appear in the app

The app fetches this file only when the user clicks **Settings → Plugins & modules → Catalog → Check GitHub catalog**:

https://raw.githubusercontent.com/odxxt/SableLatch/main/modules/catalog.json

Each catalog entry points to a ZIP under the same repository and specifies its SHA-256. The catalog payload is signed using the SableLatch publisher key; the ZIP's manifest is independently signed. The client checks both and the archive hash. Uploading an unsigned JSON list or editing signed bytes invalidates the catalog.

The PuTTY catalog and ZIP were fetched from public GitHub on 24 September 2026; the catalog signature and ZIP hash matched. No separate hosting or server is needed. Pushing a compatible, correctly signed catalog/package update to main makes it available on the next explicit catalog check. The included PuTTY module also works offline.

For a new official module:

1. Implement and test any required new host capability in an app release first. API 1 ZIPs cannot contain arbitrary code. In particular, an RDP manifest alone cannot implement RDP.
2. Define the manifest; preserve the schema or use a new module ID when incompatible.
3. Sign the exact manifest bytes with `modules/Sign-Official.ps1`, then pack it using `modules/Pack-Module.ps1`.
4. Update the catalog payload with its name, description, allowed repository URL and archive SHA-256. Sign those exact payload bytes and put the payload and signature, each base64 encoded, in catalog.json.
5. Verify the signature, package and download hashes locally; commit only public artifacts and push them.

Keep the encrypted publisher private key and its unlock password outside Git and release ZIPs, backed up separately. The repository contains only the public verification key. See [the module author guide](../modules/README.md).
