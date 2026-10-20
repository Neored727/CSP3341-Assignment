# Version 1.0.1

The Windows run supplied by the user showed an attendance-tab exception: "No room is available to display rows." The tab attempted to set CurrentCell and FirstDisplayedScrollingRowIndex during DataBindingComplete, before the grid had received its final layout size.

Removed that early selection/scroll operation. Attendance rows are now sorted so dates at or before the reporting cut-off appear first, newest first, with later dates below. No automatic scroll positioning is required.

Added explicit 100% column widths to single-column TableLayoutPanel containers in the shared page layout, overview, activity content, workspace, login and editor dialogs. This addresses the clipped/narrow page content visible in the screenshot.

The application title and assembly version identify this build as v1.0.1. Record format, credentials, formulas and storage path are compatible with the previous build. Extracting the new package does not replace the user's working records in LocalApplicationData.

The fix is build-checked and the existing Core checks are rerun. Native WinForms layout execution still requires a Windows run; the screenshot is evidence of the original failure, not evidence that the fix has passed a Windows acceptance check.
