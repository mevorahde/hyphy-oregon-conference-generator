# Hyphy Oregon Conference Generator

Hyphy Oregon Conference Generator is a small, personally meaningful
fantasy-football utility that divides league owners evenly between conferences.
It modernizes the original .NET Framework 4.5.2 console application while
preserving the project's identity and Git history.

The default workflow accepts ten owners and assigns five each to East and West.
Custom workflows support two or more conferences whenever the owner count is
exactly divisible by the conference count.

![Hyphy Oregon Conference Generator icon](src/HyphyOregon.ConferenceGenerator.Cli/Resources/hyphy-oregon-conference-generator.png)

## Architecture

- `Core` contains immutable models, validation, balanced Fisher-Yates
  assignment, and injected random sources.
- `Cli` contains parsing, prompting, presentation, composition, cancellation,
  and safe exit-code mapping.
- `Tests` exercise domain, CLI, architecture, metadata, assets, CI policy, and
  packaging policy through headless boundaries.

See [architecture](docs/architecture.md), the
[legacy migration notes](docs/migration-from-legacy.md), and
[asset provenance](ASSET_PROVENANCE.md) for details.

## Build and verify

The repository pins .NET SDK 10.0.302 in `global.json`.

```shell
dotnet restore HyphyOregon.ConferenceGenerator.slnx
dotnet build HyphyOregon.ConferenceGenerator.slnx --configuration Release --no-restore
dotnet test HyphyOregon.ConferenceGenerator.slnx --configuration Release --no-build
dotnet format HyphyOregon.ConferenceGenerator.slnx --verify-no-changes --no-restore
```

## Run interactively

```shell
dotnet run --project src/HyphyOregon.ConferenceGenerator.Cli --configuration Release
```

Press Enter at the setup question for the default ten-owner East/West draw, or
choose the custom workflow. A blank seed uses the system-backed random source.

## Run noninteractively

```shell
dotnet run --project src/HyphyOregon.ConferenceGenerator.Cli --configuration Release -- \
  --owner "Alex" --owner "Blake" --owner "Casey" --owner "Devon" \
  --owner "Emery" --owner "Finley" --owner "Gray" --owner "Harper" \
  --owner "Indigo" --owner "Jordan" --seed 20200830
```

The verified deterministic result is:

```text
East
  - Indigo
  - Devon
  - Casey
  - Finley
  - Blake

West
  - Emery
  - Jordan
  - Alex
  - Gray
  - Harper

Seed: 20200830
Generator: SplitMix64-v1
```

The portfolio capture below shows the deterministic seed `20200830` smoke-test
output from the functionally identical `1.0.0-rc.1` package. It does not
represent a separate test of the final stable version.

![Hyphy Oregon Conference Generator CLI deterministic draw](docs/images/hyphy-oregon-conference-generator-cli.png)

Seeded draws use the project-defined, versioned SplitMix64-v1 generator and
repeat for the same input and seed across supported runtimes. Unseeded draws
use the system-backed random source and are not intended to repeat. Neither
mode is presented as cryptographic randomness or an external fairness
certification.

Names are trimmed, must be nonempty, cannot contain control characters or line
breaks, and must be unique ignoring case. Unicode letters, internal spaces,
apostrophes, and hyphens are supported. Every valid draw assigns each owner
exactly once and gives every conference the same number of owners.

## Exit codes

| Code | Meaning |
| ---: | --- |
| 0 | Success, help, or version |
| 2 | Command-line usage or domain-validation error |
| 3 | Interactive input ended or the operation was cancelled |
| 70 | Unexpected internal failure |

## Release archives

### Standalone Windows executable

The upcoming `1.0.1` packaging script also builds `hyphy-oregon-conference-generator-1.0.1-win-x64.exe`
and its `.sha256` checksum. This is a self-contained, single-file Windows x64
download: no .NET installation or adjacent DLL/resource files are required.
The MIT license and application icon resources are included in the bundle.
It is a local build option pending release publication, not yet a published asset.
The original folder-based ZIP downloads remain available.

Double-click the executable, complete the prompts, and press Enter when finished
to close the results. Successful interactive console draws pause before exit;
command-line arguments and redirected input/output do not pause.
You can also run it from PowerShell:

```powershell
& '.\hyphy-oregon-conference-generator-1.0.1-win-x64.exe'
```

The bundle extracts its contents to the per-user .NET temporary extraction cache
on launch, so that location must be writable. It is not an installer and does not
require administrator access. The executable is unsigned; verify its origin and
checksum before running it. Do not disable Windows security protections.

Build all packages into a new, empty directory:

```powershell
./eng/Package-Release.ps1 -OutputDirectory artifacts/release
```

See Microsoft's [single-file deployment documentation](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview)
for the runtime extraction behavior.

### Existing portable archives

Version `1.0.0` is the first stable modern release. The manual packaging
workflow produces two portable ZIP archives:

- Framework-dependent, cross-platform: requires a compatible .NET 10 runtime
  and runs with
  `dotnet "Hyphy Oregon Conference Generator.dll"`.
- Self-contained Windows x64: includes the runtime and runs directly from its
  extracted folder without installation.

PowerShell users can safely invoke the Windows executable through a quoted path
variable:

```powershell
$exe = '.\Hyphy Oregon Conference Generator.exe'
& $exe --help
```

Each archive has a separate SHA-256 checksum file. The Windows executable is
unsigned, so Windows SmartScreen may warn. The Windows x64 self-contained
package was manually smoke-tested as `1.0.0-rc.1` before promotion. The final
stable promotion changes release metadata, documentation, the portfolio
screenshot, and regression expectations without changing application
behavior. No bit-for-bit reproducible-build claim is made. No release archive
is published automatically and the workflows do not create a GitHub Release.

## License and history

The modern project and icon are available under the [MIT License](LICENSE).
The original .NET Framework implementation and legacy icon remain recoverable
through Git history and the local `legacy-dotnet-framework-4.5.2` baseline tag.
This project is independent and does not claim endorsement by Microsoft,
Oregon, any sports league, or any other organization.
