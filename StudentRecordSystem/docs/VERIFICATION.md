# Verification status

Current revision: **1.0.1**, including the attendance loading and page-width corrections described in CHANGES.md.

## Completed in the build environment

- Compiled the complete Visual Studio solution in Release configuration: **0 warnings, 0 errors**.
- Ran **41 automated checks: 41 passed, 0 failed**. See CHECK_RESULTS.txt for the complete list.
- Published the WinForms application for **Windows x64**, with its .NET runtime included. The executable is a Windows PE32+ GUI executable.
- Exported and validated the fictional sample JSON. It includes three students, two terms, four subjects, sports/clubs, participation and achievements.
- Checked that the ZIP contains the solution, projects, source, documentation, sample data and the entire published runtime folder.

Environment: Linux x64, .NET SDK 10.0.402, Microsoft.NETCore.App and WindowsDesktop targeting/runtime packs 10.0.13. Windows targeting was enabled for cross-compilation. The code does not require third-party application packages.

## Automated check coverage

Checks cover correct and incorrect login, unique salted password hashes, opaque session registration and logout, restricted student snapshots, service-level teacher permissions, normalized assessment marks, equal subject weighting, zero versus unmarked results, historical cut-offs, chart gaps, attendance coverage and streaks, holiday handling, activity membership and coverage, invalid dates/marks, duplicate sessions, JSON round-trip, backup retention, corrupt files, invalid relationships, failed disk saves, export restrictions, change events and serialized concurrent updates. The two language experiments are also checked.

## Still required on Windows

The build environment cannot execute the native WinForms interface. The user ran version 1.0.0 on Windows and supplied the attendance error screenshot used for this fix. **The corrected version 1.0.1 has not yet been launched or visually verified on Windows.** Follow DEMONSTRATION.md to check the login page, grids, dialogs, charts, window resizing, display scaling and persistence on your computer. Capture actual application screenshots for the report.

Automated checks establish selected behaviours; they do not establish production security, exhaustive correctness, accessibility compliance or performance superiority. No performance benchmark was run.

No changes were pushed to the user's private GitHub repository. Add the supplied source folder to the connected local repository after reviewing it.
