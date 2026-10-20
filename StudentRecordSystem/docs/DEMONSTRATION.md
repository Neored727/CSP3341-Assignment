# Demonstration and Windows acceptance checks

Perform these steps on Windows before taking assessment screenshots. Tick an item only after observing it. The native interface has not been executed in the Linux build environment.

1. [ ] Open the solution and run StudentRecordSystem.App, or launch the full WindowsApp package. Confirm v1.0.1 appears in the title and the login window is readable at your screen scaling.
2. [ ] Sign in as `teacher` / `Demo123!`. Confirm the default date is 16 October 2026 and Term 3 is selected.
3. [ ] View Amaya's dashboard. Hover over academic, attendance and cricket points. Change subject and activity; confirm the labels and points change. Right-click a chart and save a PNG.
4. [ ] Choose Term 2, then Term 3. Inspect Term comparison and explain why coverage and assessment difficulty limit comparisons.
5. [ ] Select Nila. Confirm the missing attendance day and unmarked Science assessment appear separately from absence and zero.
6. [ ] Select an assessment. Set its mark to 0, save and confirm it is a marked zero. Clear it with Not marked, then enter a valid value again. Confirm the dashboard updates.
7. [ ] Create a Mathematics assessment with a maximum of 50. Enter 43 marks for a student. Confirm it is shown as 86%. A typed mark greater than the maximum is prevented by the numeric input; Core tests also check invalid values at the service boundary.
8. [ ] Open Attendance after signing in and after changing students/terms. Confirm the grid fills the available width without an exception. It lists dates through the as-of cut-off first, newest first, followed by later dates. Change a recorded day and inspect the percentage/streak. Revert the change if retaining the original sample story matters. Try Not recorded to see the coverage count.
9. [ ] In Sports & clubs, record participation for an enrolled student. Add an achievement. Confirm achievements have descriptions, not score weights. For a new membership, enrol the student first.
10. [ ] Export a backup from Data & backup. Check that the destination exists and that the local working file has a .bak after an edit.
11. [ ] Close and reopen the app. Confirm saved edits remain. The executable and Visual Studio build use the same data folder for this Windows user.
12. [ ] Sign in as `student1`. Confirm only Amaya's profile is available and editing/export buttons are absent. Automated checks independently verify service-level write restrictions.
13. [ ] Open C# trade-offs and run both examples. Explain why integer division yields 0% and why the deferred query sees 95 after the source changes. Show the relevant source methods.
14. [ ] Resize the window and inspect the dashboard scroll area, grids and dialogs. Confirm no controls overlap at your display scaling. Check both teacher and student views.

## Suggested report evidence

Capture the teacher overview, an examination result, attendance coverage/streak, activity participation and achievement, student read-only view, and the two trade-off examples. Explain what each screenshot proves. Screenshots from mockups or these instructions are not evidence of a running application.

Use your real run results in Part B. A useful demonstration sequence is: sign in → interpret dashboard → change a mark → show recalculated summary → reopen to demonstrate persistence → sign in as student → show trade-off experiments.

## Troubleshooting

- **Select Startup Item / no app starts:** open the `.sln` as a project/solution and set StudentRecordSystem.App as the startup project. The Core project is a library and Checks is a console test harness.
- **SDK not found:** use Visual Studio Installer's .NET desktop development workload and .NET 10 SDK. Do not change the project to .NET Framework.
- **Executable copied alone:** restore the entire WindowsApp folder; its runtime and supporting DLLs are required.
- **Data file invalid:** the error gives its location. Close the app and preserve the file before restoring the `.bak` or a known-good exported copy. There is no silent deletion/reset.
- **A later record is missing from the dashboard:** advance the as-of date to include it. Teacher entry lists show the complete term, while summaries use the cut-off.
- **Graphs show gaps:** inspect missing marks or records. A gap is intentional when a percentage cannot be calculated.
- **No activity data:** select an enrolled activity and check scheduled sessions and the as-of date.
