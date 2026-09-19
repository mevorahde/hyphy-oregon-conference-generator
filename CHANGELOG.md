# Changelog

All notable changes to this project are documented here.

## [Unreleased]

### Fixed

- Restored test discovery after the MSTest framework update by aligning the
  test adapter to the same `4.4.0` release.

### Documentation

- Updated release, download, changelog, and security-support wording after the
  `v1.0.1` GitHub Release was published.

## [1.0.1] - 2026-09-18

### Added

- Optional self-contained, single-file Windows x64 executable and SHA-256 checksum,
  alongside the existing portable archives. Conference assignment logic is unchanged.
- Packaging rejects single-file output containing any supporting files.
- Successful interactive console draws wait for Enter so double-click launches
  keep results visible; redirected and command-line runs do not pause.

## [1.0.0] - 2026-07-29

### Added

- Defined the first stable modern release metadata.
- Added the approved deterministic-draw screenshot for portfolio documentation.

### Changed

- Promoted the functionally identical, manually smoke-tested `1.0.0-rc.1`
  package to stable `1.0.0`.
- Limited the final promotion to release metadata, documentation, screenshot
  provenance, and regression expectations; application behavior is unchanged.

The Windows x64 self-contained release candidate passed manual smoke testing
before promotion. The executable remains unsigned, and no bit-for-bit
reproducible-build claim is made. Legacy .NET Framework history remains
preserved in Git and through the existing local
`legacy-dotnet-framework-4.5.2` tag.

## [1.0.0-rc.1] - 2026-07-28

### Added

- Cross-platform .NET 10 Core, CLI, and test architecture.
- Interactive and repeatable command-line conference draws.
- Stable seeded draws using the versioned SplitMix64-v1 generator.
- Modern original branding, release packaging, CI, and project documentation.

### Changed

- Replaced the .NET Framework 4.5.2 implementation with an independently
  testable, accessible console workflow.

### Removed

- Retired the superseded legacy solution, project, source, configuration, and
  icon from the current tree. Their history remains in Git and at the local
  `legacy-dotnet-framework-4.5.2` baseline tag.
