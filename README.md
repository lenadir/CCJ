# CCJ

A small Windows desktop app that manages three scheduled Robocopy backup jobs: **Folder1**,
**Folder2** and **Folder3**. It edits the jobs, regenerates `Backup_FolderN.bat` from a fixed
template, creates or updates the matching Task Scheduler tasks, and shows status and logs in
one window.

C# WinForms, .NET Framework 4.8, single `CCJ.exe`, no installer.

---

## Safety guarantees

These are enforced in code, not just by convention:

| Guarantee | How it is enforced |
|---|---|
| Nothing is ever deleted on the destination | `ScriptGenerator.GuardAgainstDeletingOptions` throws if a rendered script contains `/MIR` or `/PURGE` outside a comment. It runs on every render **and** again inside `WriteScript`, so a bad script cannot reach the disk. Covered by unit tests. |
| No real copy without confirmation | `Run now` always opens `ConfirmRunForm`, which shows the source, the destination and the file count. The confirm button stays disabled until the count is in. |
| Scripts are shown before they are written | `Save` opens a full preview of all three scripts. Nothing is written if you cancel. |
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

**Packages** (restored by `tools\restore-packages.ps1`, which downloads the `.nupkg` files
directly because `nuget.exe` is not installed on the target machine):

- `TaskScheduler` 2.12.2 (`lib\net48`), the NuGet package that provides `Microsoft.Win32.TaskScheduler.dll`
- `Newtonsoft.Json` 13.0.4 (`lib\net45`)
- `NUnit` 3.14.0 and `NUnitLite` 3.14.0 (tests only)

If the machine has no internet access, copy the whole `packages\` folder from a machine that
does.

### Tests

```cmd
run-tests.cmd
```

Builds, runs the 276 unit tests, then runs the exe's own self test. Every test works in a
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
  next run, last run, last result (`0x0 = success`) and the last `master.log` line for that job.
- **Jobs** — the editable settings, plus the script and log folder.
- **Logs** — the last 200 lines of `master.log`, filterable by job.

### Buttons

| Button | What it does |
|---|---|
| **Save** | Validates, previews all three scripts, writes them, saves `jobs.json`, then offers to create or update the three tasks. |
| **Preview** | Shows the script for the selected job (or all three) without writing anything. |
| **Run now** | Starts the selected job's task after a confirmation showing source, destination and file count. |
| **Test (dry run)** | Generates a `/L` copy of the script in `%TEMP%\CCJ_DryRun`, runs it, and reports the result. Copies nothing. |
| **Enable/Disable** | Flips the selected job and matches the task's enabled state. |
| **Open logs** | Opens the Logs folder in Explorer. |
| **Check connectivity** | Each path must exist and each host must answer on TCP 445, with a 3 second timeout. |
| **Check credentials** | Reports whether Credential Manager holds an entry per host, and can open Credential Manager. Passwords are never read or shown. |

### Settings

Stored in `%APPDATA%\CCJ\jobs.json`. Errors go to `%APPDATA%\CCJ\app.log`.

**Script and log folder** (the "Vox root") defaults to the `Vox` folder beside the exe and is
editable on the Jobs tab. It holds `Backup_Folder1.bat`, `Backup_Folder2.bat`,
`Backup_Folder3.bat` and the `Logs` folder. To use the classic layout, set it to `C:\Vox`.

### Validation rules

- Source and NAS root must be UNC paths, `\\server\share`, optionally with more path segments.
  Spaces in a share name are fine (`\\192.168.100.100\3G eng`).
- The NAS folder name must be a single name with none of `\ / : * ? " < > |`, not a reserved
  device name (`CON`, `NUL`, `COM1`…), and not padded with spaces or dots.
- The first run date must be a **Sunday** at **00:00**. The **Next Sunday** button fills it in.

---

## Running as the task user

Each job becomes a task named `CCJ Backup <JobName>`, configured as:

- **Trigger:** weekly, `WeeksInterval = 3`, Sunday, starting on the chosen date at 00:00.
- **Action:** `<Vox root>\Backup_FolderN.bat`, starting in `<Vox root>`.
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
| `0x41302` | Task is disabled. | Use **Enable/Disable**. |
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
                              CopyProgress
  Services\                   ScriptGenerator, TaskSchedulerService, ConnectivityChecker,
                              CredentialChecker, LogReader, JobRepository, Validation,
                              AppLog, Elevation, SelfTest
                              progress: ProgressService, CopyProgressMonitor,
                              RobocopyPlanner, RobocopyLogParser, IncrementalLineReader
  Forms\                      MainForm, JobEditorControl, PreviewForm, ConfirmRunForm,
                              PasswordForm, TextReportForm, DataGridViewProgressColumn
  Resources\BackupTemplate.bat.txt    the embedded script template
CCJ.Tests\                    NUnit tests, run by NUnitLite
```

The forms hold no backup logic: they move values between controls and a `JobConfig` and call a
service. Network work is `async` so the window never freezes.

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

### The script template

`CCJ\Resources\BackupTemplate.bat.txt` is a copy of the reference script
`Vox\Backup_Folder1.bat` with placeholders in the SETTINGS block, the `SRC` line and the
`LOGDIR` line. Everything else — the `:TRY` / `:RETRY` / `:SUCCESS` / `:FAILED` labels, the
`set "RC=%ERRORLEVEL%"` and `if %RC% LSS 8 goto SUCCESS` checks, the six attempts 900 seconds
apart, the PowerShell timestamp and the robocopy options — is byte-for-byte the reference.

`CCJ.Tests\ReferenceFidelityTests.cs` renders a script using the reference's own values and
compares it with `Vox\Backup_Folder1.bat` line by line. That reference script holds site
specific paths, so it is **not part of the repository**: on a fresh clone these two tests are
reported as ignored until you put your own `Vox\Backup_Folder1.bat` in place. Only line 4, the comment naming the
destination of that particular job, is allowed to differ. **If anyone edits the template and
changes the retry loop, a label or an ERRORLEVEL check, that test fails.**

Output is always CRLF, with no byte order mark (a BOM would break `@echo off`), written in the
console code page that `cmd.exe` uses to read batch files.
