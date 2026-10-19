# GitHub Releases distribution

Windows x64 builds now support updates from the public repository `murdockpeter/seaofuncertainty`. The updater uses GitHub's latest stable release endpoint and exact uploaded Windows asset, as described by the [GitHub Releases API](https://docs.github.com/en/rest/releases/releases?apiVersion=2022-11-28). Drafts and prereleases are excluded. Tags use `vMAJOR.MINOR.PATCH`; comparisons are numeric. This is game binary distribution, not automatic code generation or changes to gameplay rules.

## Playing and updating

Extract the entire `SeaOfUncertainty-windows-x64.zip` into a writable folder and double-click **Start Sea of Uncertainty.cmd**. Windows PowerShell 5.1, included in supported Windows desktop installations, runs the launcher without an administrator prompt. Local security policy may block scripts; it is not changed by the launcher.

The launcher checks on each launch and asks before downloading a newer stable release. Declining, going offline, a missing release, GitHub rate limiting, or a failed download leaves the installed game playable. There is no embedded GitHub token. The check has a 15-second network timeout; the download has a 10-minute timeout. The download currently has no percentage progress display; allow it to finish before trying again.

**Settings > Check for Updates** invokes the same launcher without starting a second game. The current operation continues. Exit normally (save first) and use the launcher to play an installed update. Editor and unpackaged builds open the Releases page instead. Starting a version's EXE directly bypasses the startup check.

The archive is downloaded to a unique temporary file. Its byte count and SHA-256 must match GitHub's asset metadata; missing digests fail closed. This checks integrity against the repository's release asset, and does not provide a separate publisher signature. Before installation, all ZIP paths are checked for traversal, duplicates, links, and size limits. The release manifest must match the tag and include the executable, Unity runtime, and Unity data. Schema 1 supports archives up to 4 GiB and expanded files up to 8 GiB.

Use a reasonably short installation path. Windows PowerShell's legacy path-length limits apply to staged extraction; an overlong path reports an error before extraction and leaves the selected version untouched. Move/extract the full package closer to the drive root if needed. Release archives exclude Unity's `DoNotShip` debug/backup directories.

Installation stages a fresh version directory and then atomically replaces `current.json` on the same volume. A failure before the pointer change preserves the selected version. Version directories are never overwritten or automatically deleted, so an open game keeps its files. Saves, settings, and telemetry remain under Unity's existing persistent storage; company/product names do not change. Update compatibility still depends on the game's existing save migrations. Retain a separate copy of important saves before loading them with a new build.

## Rollback and recovery

From the extracted package folder:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\SeaLauncher.ps1 -Rollback
```

This explicitly swaps the selected and previous versions and launches the previous build without checking for an update on that invocation. It does not revert save files. Automatic crash detection/rollback is not implemented. Use a save compatible with the older game.

If an installation was interrupted after moving files but before switching the pointer, the old version still launches. An unused `versions/vX.Y.Z` may remain; remove only that unused directory before retrying the same update. `current.json.bak` retains the preceding pointer. A missing/corrupt pointer reports a recovery error: restore the backup after checking that its version directory exists, or extract a fresh package. Old unused version directories may be removed manually once rollback is no longer needed. Do not remove the running, current, or previous version.

The root launcher is a stable schema-1 bootstrap and is deliberately preserved across updates. Downloads supply the new player directory, rather than replacing a script that may still be running. A future bootstrap/schema change requires extracting a new full launcher package. App signing, installers, macOS/Linux distribution, delta updates, and prerelease channels are not part of this first implementation.

## Preparing a release

Close Unity and run from the repository root:

```powershell
.\tools\test-updater.ps1
.\tools\package-release.ps1 -Version 0.1.0
```

The packager builds a non-development Windows player, temporarily sets the embedded version, and restores the project version afterward. It does not rebuild/change the scene. It creates:

```text
SeaOfUncertainty/Builds/Releases/v0.1.0/
  SeaOfUncertainty-windows-x64.zip
  SHA256SUMS.txt
  Build.log
  package/
    Start Sea of Uncertainty.cmd
    SeaLauncher.ps1
    current.json
    release.json
    versions/v0.1.0/<Unity player files>
```

Output is ignored by Git. An existing output directory causes an error rather than overwriting a reviewed artifact. `-UnityPath` selects another editor executable. `-PlayerDirectory` packages an existing Windows build; the caller is responsible for ensuring that build's embedded version and production settings match the release.

Review and commit the intended source, run the required game checks, and build the final artifact from that exact state. Then create a **draft** GitHub Release targeting that tested commit with the matching tag. Upload the ZIP under the exact name above and `SHA256SUMS.txt`. Do not upload GitHub's generated source ZIP as the player. Check the uploaded asset digest, release notes, save compatibility, and extracted launcher before publishing the stable release. The packager performs no push, tagging, release creation, or publication. Enable immutable releases in repository settings if desired.

Smoke-test a published candidate using an older extracted package: accept the update, launch the new version, load a compatible save, then exercise explicit rollback with an older compatible save. Repeat offline and with a declined update. This live two-release path requires actual published releases; local fixture tests alone cannot validate it.

## Automated verification

`tools/test-updater.ps1` uses disposable local fixtures and the production launcher functions. It covers numeric versions, malformed tags, SHA mismatch, traversal/absolute/duplicate ZIP paths, manifest mismatch, missing Unity files, atomic pointer replacement, preservation of the previous player, rollback selection, staging cleanup, concurrent file locks, and release-asset filtering. It does not access GitHub or execute fixture EXEs.

Local verification on 2026-10-19, against the existing dirty working tree plus this implementation:

- All 43 updater checks passed, including offline/declined startup, in-game checks without launching another player, explicit rollback orchestration, and overlong path rejection.
- Unity's Presentation suite passed (`SeaOfUncertainty/TestResults/Presentation.log`).
- A non-development Windows `0.1.0` player built successfully. The corrected release ZIP passed production archive validation and extraction. SHA-256: `2669f1e64be2653abf11882a1dc5bd4029e7994912247e6d235cf5ca5250df63`.
- The extracted player initialized and exited with code 0 through the existing capture hook, with no matching exception/load errors (`TestResults/ReleasePlayerSmoke.log`). A first headless probe timed out because the player did not exit on `-quit`; it was terminated and replaced by the self-exiting capture probe. The hidden-window capture was black, so it supplies no visible UI acceptance evidence.
- No tag, draft, or public release was created. The live two-release update/rollback path and human UI acceptance remain unverified.
