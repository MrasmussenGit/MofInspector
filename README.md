# MofInspector

A lightweight .NET 8 WPF utility to inspect and compare MOF files. It lets you browse rule details, compare two MOF files side-by-side, quickly identify differences, and drill into rule-level property and raw-text diffs.

- Target framework: `net8.0-windows`
- UI technology: WPF
- App version: 1.0.0.9

## Features

- Inspect a single MOF:
  - Tree of rules with details and instance properties.
- Compare two MOFs:
  - Color-coded results: Match, Different, Version Only, Missing, Parsing Error, Info.
  - Filters to show/hide statuses.
  - Grouping by `Category`.
  - Copy selected rows to clipboard.
  - Open rule-level detail view.
- Rule Details:
  - Per-property diff with status per row.
  - Raw text diff with optional “Only Changed Lines”.
  - Tooltips explaining status labels and colors.

## Color legend

Status backgrounds used in compare views:
- Match / Same: `#DFF5D8` (green)
- Version Only: `#FFF3B3` (pale yellow)
- Different: `#FFC8C8` (soft red)
- Missing (File 1 / File 2): `#E9D9FF` (lavender)
- Parsing Error: `#FFB266` (orange)
- Info: `#E6E9EF` (blue-gray)
- Selection (Rule Details): subtle border highlight (status color remains visible)

Notes:
- “Version Only” means values are identical after normalizing PowerStig version tokens in paths/names.

## Screenshots

Place screenshots in a `docs/` folder and link them here:
- Main menu
- MOF Compare
- Rule Details (properties and raw text)

## Getting started

Prerequisites:
- Windows 10/11
- .NET SDK 8.0+
- Visual Studio 2022 (recommended) with WPF workload

Build and run:
- Visual Studio: open the solution, set `MofInspector` as startup project, press F5.
- CLI:
  - `dotnet build`
  - `dotnet run --project MofInspector`

## Usage

- Launch the app (Main menu):
  - Inspect MOF: open a single file, browse rules and properties.
  - Compare: open the Compare window.

Compare window:
- Click `Load File 1` and `Load File 2`.
- Use the “Show” filters to include/exclude statuses.
- Double-click or press Enter on a row to open Rule Details.
- Press Ctrl+C to copy selected rows (tab-delimited: RuleId, Status, Details).
- Bottom bar shows total and displayed counts; `Close` exits the window.

Rule Details window:
- Property Differences: sortable columns; press Ctrl+C to copy selected rows.
- Raw Text Diff: enable “Show Raw Text Diff”; optionally check “Only Changed Lines”.
- Tooltips explain each legend entry and control.
- `Close` exits the window.

## Project structure

- `MofInspector\App.xaml` – Application definition.
- `MofInspector\MainWindow.xaml` – Main menu.
- `MofInspector\InspectWindow.xaml` – Single-file inspection view.
- `MofInspector\CompareWindow.xaml` / `.cs` – File comparison UI and logic.
- `MofInspector\CompareDetailsWindow.xaml` / `.cs` – Rule-level detail and raw-text diff.
- `MofInspector\Mof.cs`, `MofRule.cs`, `MofInstance.cs` – Parsing and data models.

## How version-only detection works

During comparisons, occurrences of PowerStig version segments in paths/identifiers are normalized to a placeholder. If everything matches post-normalization (and raw text differs only in version tokens), the status is “Version Only”.

## Keyboard shortcuts

- Compare list:
  - Enter: open Rule Details.
  - Ctrl+C: copy selected rows.
- Rule Details:
  - Property list Ctrl+C: copy selected rows.
  - Raw text lists Ctrl+C: copy selected lines.

## Contributing

Issues and pull requests are welcome. Please open an issue to discuss substantial changes before submitting a PR.

## License

TBD. Add a license (e.g., MIT) that suits your needs and link it here.

## Acknowledgments

- Built with .NET 8 and WPF.
- Uses simple regex-based normalization for PowerStig version markers.

