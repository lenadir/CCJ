# CCJ

A small Windows desktop app that manages scheduled Robocopy backup jobs. It starts with three
(**Folder1**, **Folder2** and **Folder3**), and you can add as many more as you need. It edits
the jobs, regenerates `Backup_<job>.bat` from a fixed template, creates or updates the matching
Task Scheduler tasks, shows status, progress and logs in one window, and can pause, resume or
stop a copy while it runs.

C# WinForms, .NET Framework 4.8, single `CCJ.exe`, no installer.

---

## Safety guarantees

These are enforced in code, not just by convention:

| Guarantee | How it is enforced |
|---|---|
| Nothing is ever deleted on the destination | `ScriptGenerator.GuardAgainstDeletingOptions` throws if a rendered script contains `/MIR` or `/PURGE` outside a comment. It runs on every render **and** again inside `WriteScript`, so a bad script cannot reach the disk. Covered by unit tests. |
| No real copy without confirmation | `Start copy` always opens `ConfirmRunForm`, which shows the source, the destination and the file count. The confirm button stays disabled until the count is in. |
| Scripts are shown before they are written | `Save` opens a full preview of every script it is about to write. Nothing is written if you cancel. |
| Stopping a copy deletes nothing | `Stop` ends the task and the robocopy process. Files already copied stay on the destination; robocopy is never asked to delete anything, and the confirmation says so before you agree. |
| The dry run cannot copy | It adds `/L` to robocopy, drops the destination-folder creation, and redirects logs to `%TEMP%\CCJ_DryRun`. |
| The progress bar cannot change anything | It only reads log files. Its total comes from a separate robocopy pass that always runs with `/L` (list only) and is checked against `/MIR` and `/PURGE` like every other command. Tests assert that neither the source nor the destination is touched, and that the destination folder is not even created. |
| Your password is never stored | It is a method parameter passed straight to the Task Scheduler API. It is never written to `jobs.json`, to `app.log`, or to any other file, and the password box is cleared when the dialog closes. A unit test asserts no `password` field appears in the saved JSON. |
| Invalid settings write nothing | Everything is validated before any file is touched; a failed save leaves the previous `jobs.json` byte-for-byte intact. |

---

## Build

No Visual Studio needed. .NET Framework 4.8 supplies the MSBuild used by the build script.

```cmd
build.cmd
```

This restores the NuGet packages, builds Release and copies the output to `dist\`:

```
dist\CCJ.exe
dist\CCJ.exe.config
dist\Microsoft.Win32.TaskScheduler.dll
dist\Newtonsoft.Json.dll
```

`build.cmd Debug` builds Debug instead.

### The installer

```cmd
build-installer.cmd
```

Builds the app, then compiles **`dist\CCJ-Setup.exe`** — one self-contained file (~1.2 MB) with
the program, both DLLs and the documentation embedded. That single file is what you give to
another PC: double-click, approve the Windows prompt, press Install. Source in `installer\`,
deployment notes in `INSTALL.md`.

**In Visual Studio:** open `CCJ.sln` and build. The projects use `packages.config`, and the
package folders the build script creates are the same ones VS restores into.

**Packages.** The extracted packages are committed in `packages\`, so a fresh clone builds with
no download at all. `tools\restore-packages.ps1` (run by `build.cmd`) only fetches a package that
is missing, straight from nuget.org, because `nuget.exe` is not installed on the target machine:

- `TaskScheduler` 2.12.2 (`lib\net48`), the NuGet package that provides `Microsoft.Win32.TaskScheduler.dll`
- `Newtonsoft.Json` 13.0.4 (`lib\net45`)
- `NUnit` 3.14.0 and `NUnitLite` 3.14.0 (tests only)

Because they are committed, a machine with no internet access can build as well. `.gitattributes`
keeps `packages\` byte-exact (no line-ending conversion).

### Tests

```cmd
run-tests.cmd
```

Builds, runs the 322 unit tests, then runs the exe's own self test. Every test works in a
folder under `%TEMP%`; no network path is ever touched. The tests also run with
`CCJ_DATA_DIR` pointed at a throwaway folder, so they can never read or change your real
`jobs.json` or `app.log`. The tests are a self-running NUnitLite
console app (`CCJ.Tests\bin\Release\CCJ.Tests.exe`) because there is no `vstest.console.exe`
without Visual Studio. Pass NUnitLite options through, for example:

```cmd
run-tests.cmd --where "class =~ ScriptGenerator"
```

`CCJ.exe --selftest` runs the self test on its own. It generates a script, executes a real dry
run against temporary folders and checks that nothing was copied, then runs a real copy of a
small temporary tree while the progress monitor follows it. Run it on a new PC to prove that
robocopy's log is read correctly in that machine's Windows language.

**Settings location.** `jobs.json` and `app.log` live in `%APPDATA%\CCJ`. Setting the
environment variable `CCJ_DATA_DIR` to a folder moves both there, which is how the tests stay
away from the real ones, and allows a portable setup.

---

## Run

Double-click `dist\CCJ.exe`.

The app starts **without** administrator rights on purpose, so the dashboard, the log viewer,
the connectivity and credential checks and the script preview always work. Creating or
changing a scheduled task does need administrator rights; when you are not elevated a yellow
banner appears with a **Restart as administrator** button.

### The three tabs

- **Dashboard** — one row per job: name, source, destination, enabled, whether the task exists,
  progress, next run, last run, last result (`0x0 = success`) and the last `master.log` line for
  that job.
- **Jobs** — the editable settings, one box per job, plus the script and log folder, **Add
  another job**, a **Remove job** button per job, and the **Enabled** switch.
- **Logs** — the last 200 lines of `master.log`, filterable by job.

### Buttons

| Button | What it does |
|---|---|
| **Save** | Validates, previews every script it will write, writes them, saves `jobs.json`, then offers to create or update the tasks. |
| **Start copy** | Starts the selected job's task after a confirmation showing source, destination and file count. |
| **Pause / Resume** | Suspends the running robocopy of the selected job, and lets it carry on again. The button is only available while a copy is actually under way. |
| **Stop** | Ends the run: the scheduled task first, then robocopy. Nothing is deleted. |
| **Test (dry run)** | Generates a `/L` copy of the script in `%TEMP%\CCJ_DryRun`, runs it, and reports the result. Copies nothing. |
| **Check connectivity** | Each path must exist and each host must answer on TCP 445, with a 3 second timeout. |
| **Check credentials** | Reports whether Credential Manager holds an entry per host, and can open Credential Manager. Passwords are never read or shown. |
| **Dark mode** | Switches the window between light and dark. Remembered in `%APPDATA%\CCJ\ui.json`. |

Enabling and disabling a job is **not** a button: it is the **Enabled** box on the Jobs tab. It
takes effect the moment you tick it — `jobs.json` is written and the scheduled task is switched
at the same time — so the Jobs tab and the Dashboard can never disagree.

### Adding, removing and skipping jobs

- **Add another job** adds an empty job with the next free key (`Folder4`, `Folder5`, …), which
  is also the name of its script, `Backup_Folder4.bat`.
- A job you have not filled in is **skipped**, not refused: it is left out of `jobs.json`, no
  script is written for it and no task is made. One filled-in job is enough to save and to copy.
  A job counts as filled in once it has a source or a NAS folder name.
- **Remove job** deletes that job's scheduled task and takes it out of the settings. Its script
  file and its logs are left on disk, and nothing already copied is touched.

### Settings

Stored in `%APPDATA%\CCJ\jobs.json`. Errors go to `%APPDATA%\CCJ\app.log`. Dark mode is kept
separately in `%APPDATA%\CCJ\ui.json`, so the look of the window never depends on the backup
settings being valid.

**Script and log folder** (the "Vox root") defaults to the `Vox` folder beside the exe and is
editable on the Jobs tab. It holds one `Backup_<job>.bat` per job and the `Logs` folder. To use
the classic layout, set it to `C:\Vox`.

### Validation rules

- Source and NAS root must be UNC paths, `\\server\share`, optionally with more path segments.
  Spaces in a share name are fine (`\\192.168.100.100\3G eng`).
- The NAS folder name must be a single name with none of `\ / : * ? " < > |`, not a reserved
  device name (`CON`, `NUL`, `COM1`…), and not padded with spaces or dots.
- The first run date must be a **Sunday**; the time of day is yours to choose, and the weekly
  trigger repeats at that time.
- Two jobs may not share a key (they would write the same script) or a job name (they would
  fight over one scheduled task).
- At least one job must be filled in.

---

## Running as the task user

Each job becomes a task named `CCJ Backup <JobName>`, configured as:

- **Trigger:** weekly, `WeeksInterval = 3`, Sunday, starting on the chosen date **and time**.
- **Action:** `<Vox root>\Backup_<job>.bat`, starting in `<Vox root>`.
- **Security:** *Run whether user is logged on or not* (`TaskLogonType.Password`).
- **Run after a missed start** (`StartWhenAvailable`) is on.
- **Do not start a new instance if one is running** (`MultipleInstances = IgnoreNew`) is on.
- Battery conditions are switched off, so a backup is not silently skipped on a laptop.

What the account needs:

1. **A non-blank password.** Windows will not run a task while logged off under an account with
   a blank password.
2. **Access to both hosts.** The task runs in its own session, so it does not inherit drive
   letters or credentials from your interactive session. Use full UNC paths (CCJ enforces this)
   and store credentials for each host in Credential Manager **as the task account**. Use
   *Check credentials* to see whether entries exist.
3. **The "Log on as a batch job" right**, if your domain policy restricts it
   (`secpol.msc` → Local Policies → User Rights Assignment).

CCJ asks for the password once, when it registers the tasks, and does not keep it. If you change
the Windows password later, press **Save** again to re-register the tasks with the new one.

---

## Troubleshooting

> Authored from the standard Task Scheduler and robocopy result codes. No Task Scheduler guide
> document was supplied with the brief, so this table is not a quotation from one.

### Last run result, as shown on the dashboard

| Code | Meaning | What to do |
|---|---|---|
| `0x0` | Success. | Nothing. |
| `0x1` | The action failed to launch. | Check the `.bat` path in the task's action. Press **Save** to rewrite it. |
| `0x2` | File not found. | `Backup_FolderN.bat` is missing from the Vox root. Press **Save**. |
| `0xA` | Environment is incorrect. | Usually a bad "Start in" folder. Press **Save** to reset it to the Vox root. |
| `0x41300` | Task is ready, never run. | Normal before the first Sunday. |
| `0x41301` | Task is currently running. | Wait; the retry loop can last up to 6 × 15 minutes. |
| `0x41302` | Task is disabled. | Tick **Enabled** for that job on the Jobs tab. |
| `0x41303` | Task has never run. | Normal for a new task. |
| `0x41304` | No more runs scheduled. | The start date is in the past with no future occurrence. Set a new Sunday and **Save**. |
| `0x41306` | Terminated by the user. | Someone pressed End in Task Scheduler. |
| `0x8004131F` | An instance is already running. | Expected: `IgnoreNew` refused a second copy. |
| `0x80070005` | Access denied. | Restart CCJ as administrator and save again. |
| `0x8007010B` | Directory name is invalid. | The Vox root does not exist on the task account. |
| `0x80070569` | Logon failure. | The account lacks "Log on as a batch job", or the password changed. Press **Save** to re-register. |
| `0x800704DD` | Not logged on / no session. | The task was registered for an interactive logon type. Press **Save** to re-register with *run whether logged on or not*. |

### What the script writes to `master.log`

| Line | Meaning |
|---|---|
| `===== <Folder> job started` | The script started. |
| `Attempt N of 6` | Attempt N of the retry loop. |
| `Problem: NAS share not reachable` | `%DST_ROOT%` did not exist. Waits 900 s and retries. |
| `Problem: Source folder not reachable` | `%SRC%` did not exist. Waits 900 s and retries. |
| `Problem: robocopy failed with exit code N` | robocopy returned 8 or more. Waits 900 s and retries. |
| `SUCCESS: <Folder> copied` | robocopy returned 0–7. The script exits 0. |
| `FAILED: <Folder> not copied after N attempts` | All six attempts failed. The script exits 1. |

### Robocopy exit codes

0–7 are success (`0` nothing new, `1` files copied, `2` extra files present, `3` both, and so
on). **8 or higher means at least one file failed**, which is what the script tests with
`if %RC% LSS 8 goto SUCCESS`.

### Other symptoms

| Symptom | Cause and fix |
|---|---|
| "Check connectivity" fails on TCP 445 but the path check passes | Something answers the path from cache. Check the firewall and that File and Printer Sharing is on. |
| "Check credentials" finds no entry | Add one in Credential Manager (Windows Credentials → Add a Windows credential) using the host as the target, **signed in as the task account**. |
| The task runs but nothing is copied | Open the per-run log named `<Folder>_<timestamp>.log` in the Logs folder. It names the file that failed. |
| The task works when logged on and fails when logged off | Credentials are stored for your interactive account only, or a mapped drive letter is in use. Store them for the task account and use UNC paths. |
| Scripts are not regenerated | They are only written by **Save**, and only after you confirm the preview. |
| **Pause** says it cannot open the process | The task is running as another account. Use **Restart as administrator** and pause again. |
| **Pause** says there is nothing to pause | The script is between attempts, or still checking the shares, so no robocopy exists yet. Wait for the bar to show "copying", or use **Stop** to end the whole run. |
| A paused copy ends up failing | A pause of many minutes can drop the SMB session to the server. The script notices the error and retries by itself (6 attempts, 15 minutes apart), so nothing is lost, but keep pauses short. |
| A job disappeared after **Remove job** | That is what it does. Its script file is still in the Vox root and can be deleted by hand; its logs are untouched. |

---

## Project layout

```
CCJ.sln                       build.cmd   run-tests.cmd   README.md
tools\restore-packages.ps1    downloads the NuGet packages without nuget.exe
packages\                     the extracted packages
dist\                         the release output
Vox\                          runtime root: Backup_FolderN.bat and Logs\master.log
CCJ\
  Program.cs                  entry point, --selftest, global error handling
  Models\                     JobConfig, JobsFile, JobStatus, CheckResult, AppPaths,
                              CopyProgress, UiSettings
  Services\                   ScriptGenerator, TaskSchedulerService, ConnectivityChecker,
                              CredentialChecker, LogReader, JobRepository, Validation,
                              AppLog, Elevation, SelfTest
                              progress: ProgressService, CopyProgressMonitor,
                              RobocopyPlanner, RobocopyLogParser, IncrementalLineReader
                              control: CopyControl (pause, resume, stop)
  Forms\                      MainForm, JobEditorControl, PreviewForm, ConfirmRunForm,
                              PasswordForm, TextReportForm, DataGridViewProgressColumn,
                              Theme (light and dark colours)
  Resources\BackupTemplate.bat.txt    the embedded script template
CCJ.Tests\                    NUnit tests, run by NUnitLite
```

The forms hold no backup logic: they move values between controls and a `JobConfig` and call a
service. Network work is `async` so the window never freezes.

**Dialog layout.** A button is positioned by the panel that holds it (a right-to-left
`FlowLayoutPanel`), never by a position worked out from the form's width plus a right anchor.
That older pattern looked right and was not: anchoring a button to a panel that has not been
docked yet makes the panel's later stretch push the button off the right edge, so the dialog
opens with nothing to click. `CCJ.Tests\DialogLayoutTests.cs` asserts that every button of every
dialog lies inside the window, as opened and after a resize.

### The progress bar

The Dashboard shows a progress bar for any backup that is running, whether CCJ started it
(**Run now**, **Test (dry run)**) or Task Scheduler did (the Sunday 00:00 run, found
automatically when CCJ is open). It is built **without changing the script**, so the robocopy
options stay exactly as specified and the fidelity test above still holds. Three things are
read:

| Source | Gives |
|---|---|
| `Logs\master.log` | The script's own lines: `job started`, `Attempt N of M`, `Problem: ...`, and finally `SUCCESS:` or `FAILED:`. These are fixed English text from the template, so they do not depend on the Windows language. |
| The per run robocopy log, `<Folder>_<stamp>.log` | robocopy writes a line **when a file starts to copy**, and flushes it at once. The newest file line is the file being copied now; every earlier one is finished. |
| A **list-only** robocopy pass (`/L /BYTES`) | The total: how many files and bytes robocopy will copy. It is read only. Asking robocopy itself means the answer follows exactly the rules the real run will use. |

Design decisions worth knowing, each found by measuring robocopy rather than assuming:

- **robocopy does not log files it skips.** An identical file never appears in the log, so
  counting log lines against "all files in the source" would be wrong for every incremental
  run. The list-only pass is what makes the percentage correct.
- **The destination file's size cannot be used to show progress.** robocopy pre-allocates it:
  a 6 GB file read 5,722 MB the instant its copy began. So the bar moves per file, and the file
  in flight is shown by name with a timer instead of an invented percentage.
- **Result lines are recognised by their shape, not their words.** robocopy translates its
  labels (this machine's log says `Nouveau fichier`), so the parser relies on the structure: tab
  separated fields, a trailing `\` on directories, a `*` on files that exist only on the
  destination, a timestamp on errors. The `--selftest` check proves it on each machine.
- **An old run is never mistaken for the new one.** When CCJ starts a run it records how long
  `master.log` is, and only reads what is added afterwards. A log file only counts if it was
  created after the run began.
- **A retry does not send the bar backwards.** The script reuses one log name for every attempt
  and robocopy overwrites it. That is detected, and what earlier attempts finished stays counted.
- **A failed count is never shown as zero.** If this PC cannot read the folders, the total is
  "unknown" and the bar is striped; it is not treated as "nothing to copy".
- **The bar only reaches 100% when the script has logged `SUCCESS`.**

How it runs: `ProgressService` polls on a background timer every 1.5 seconds and raises an
event; the form hands each update to the UI thread. Nothing slow runs on the UI thread. It looks
for running tasks every ~8 seconds, asking Task Scheduler for the state and start time.

Known limits: two jobs that share one NAS folder name write indistinguishable log lines, so
their progress can be mixed up; and the speed and time left are estimates that count only
finished files.

### Pause, resume and stop

robocopy has no pause of its own and the script is not changed to add one, so a pause is the
operating system's: every thread of that run's robocopy process is suspended, and resumed
later (`NtSuspendProcess` / `NtResumeProcess` on a handle opened with `PROCESS_SUSPEND_RESUME`).
robocopy itself notices nothing.

Finding the right process matters more than suspending it. `CopyControl` asks WMI for every
`robocopy.exe` with its command line, and keeps the one whose `/LOG:` is **this run's log
file** — a path that contains the job's folder name and the second the run started, so it is
unique to one run of one job. Before the log is known, the logs folder plus the folder name is
used instead, which is still enough to tell a dry run (logging under `%TEMP%`) from the real run
of the same job.

**Stop** ends the scheduled task first and robocopy second. The order matters: killing only
robocopy would leave the script in its retry loop, and it would start the copy again 15 minutes
later. Files already copied are left in place; the file that was in flight is part written and
is copied again on the next run, because robocopy compares size and timestamp. The run is then
reported as *stopped*, never as a success.

Two things worth knowing:

- Pausing needs the right to open that process. When the task runs as another account, CCJ has
  to be elevated; it says so rather than failing silently.
- A long pause can drop the SMB session to the server. That shows up as a robocopy error, the
  script retries on its own, and nothing is lost — but a pause is meant to be short.

### Dark mode

WinForms has no dark mode, so `Forms\Theme.cs` colours the controls itself. Each control says
what it is through its `Tag` (`hint`, `error`, `surface`, `banner`, `readonly`) and the rest
follows from its type, so a new control only has to be added to a form. The tab headers are
owner drawn, because Windows will not colour them.

Two things stay light on purpose: the progress bars, whose light track is what keeps one colour
of text readable both on the track and on the pastel fill, and the drop-down calendars, which
Windows draws itself.

### The script template

`CCJ\Resources\BackupTemplate.bat.txt` is a copy of the reference script
`Vox\Backup_Folder1.bat` with placeholders in the SETTINGS block, the `SRC` line and the
`LOGDIR` line. Everything else — the `:TRY` / `:RETRY` / `:SUCCESS` / `:FAILED` labels, the
`set "RC=%ERRORLEVEL%"` and `if %RC% LSS 8 goto SUCCESS` checks, the six attempts 900 seconds
apart, the PowerShell timestamp and the robocopy options — is byte-for-byte the reference.

`CCJ.Tests\ReferenceFidelityTests.cs` renders a script using the reference's own values and
compares it with `Vox\Backup_Folder1.bat` line by line. `Vox\` is committed and kept byte-exact
by `.gitattributes`, so the comparison runs on a fresh clone. If you ever remove that file, the
two fidelity tests are reported as ignored instead of failing. Only line 4, the comment naming the
destination of that particular job, is allowed to differ. **If anyone edits the template and
changes the retry loop, a label or an ERRORLEVEL check, that test fails.**

Output is always CRLF, with no byte order mark (a BOM would break `@echo off`), written in the
console code page that `cmd.exe` uses to read batch files.
