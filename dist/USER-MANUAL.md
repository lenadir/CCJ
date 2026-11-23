# CCJ — User manual

CCJ manages Robocopy backup jobs. It starts with three (**Folder1**, **Folder2**, **Folder3**)
and you can add as many more as you need. Each job copies a source folder to a NAS folder,
**every 3 weeks on a Sunday, at the time you choose**, through Windows Task Scheduler.

CCJ only ever **copies**. It never deletes anything on the destination.

---

## 1. Starting the app

Double-click **`dist\CCJ.exe`**. No installation.

A yellow banner means you are not running as administrator. That is fine for looking at
everything — you only need administrator rights to create or change the scheduled tasks. When
you do, press **Restart as administrator** in the banner.

---

## 2. First-time setup (do this once)

1. Open the **Jobs** tab.
2. Check **Script and log folder** at the top. This is where the `Backup_<job>.bat` files and
   the `Logs` folder live. Use **Browse…** to change it.
3. Fill in the jobs you want. **You do not have to fill in all of them — one is enough.**
   For each job:
   - **Job name** — becomes the task name, e.g. `CCJ Backup Folder1`.
   - **Source (UNC)** — e.g. `\\192.168.100.205\vox\...`
   - **NAS root (UNC)** — e.g. `\\192.168.100.100\3G eng`
   - **NAS folder name** — the single folder under that root. The grey **Destination** line
     below shows the full result; check it reads what you expect.
   - **First run** — must be a **Sunday**, and next to it the **time** of day the backup should
     start. The weekly run repeats at that time.
   - **Enabled** — tick the jobs you want to run. *Everything starts switched off.* Ticking it
     takes effect at once: it is saved and the scheduled task is switched straight away.
4. Need more jobs? Press **Add another job** and fill the new box in.
   Don't need one? Press **Remove job** in its top right corner.
5. Press **Restart as administrator** if the banner is showing.
6. Press **Save**.

**What Save does, in order:** it checks your settings → shows you the scripts it is about to
write → writes them only if you confirm → saves your settings → offers to create the scheduled
tasks → asks for your Windows password once.

The password is used only to register the tasks with Windows. CCJ does not store it anywhere.

> Red text on the right of a job means something is wrong with it. Hover for the full list.
> A job you have left completely empty is **skipped**, not treated as a mistake: no script and
> no task are made for it, and it does not stop the others from being saved.

---

## 3. The three tabs

| Tab | What it shows |
|---|---|
| **Dashboard** | One row per job: source, destination, whether it is enabled, whether the task exists, a **progress bar** while a backup runs, next run, last run, last result, and the most recent log line for that job. Details of the selected job appear under the grid. |
| **Jobs** | The editable settings (section 2), one box per job, plus **Add another job** and the **Enabled** switch. |
| **Logs** | The last 200 lines of `master.log`, with a filter per job and a **Refresh** button. |

**Select a job by clicking its row on the Dashboard.** *Start copy*, *Pause*, *Stop* and
*Test (dry run)* all act on the selected row.

---

## 4. The buttons

| Button | What it does |
|---|---|
| **Save** | Validates, previews, writes the scripts, saves settings, offers to update the tasks. |
| **Start copy** | Runs the selected job **for real**, after a confirmation showing source, destination, file count and what will actually be copied. The progress bar follows it. |
| **Pause** / **Resume** | Freezes the copy that is running, and lets it carry on again. Available only while files are actually being copied; the button says *Resume* while it is paused. |
| **Stop** | Ends the run after a confirmation. What was already copied stays on the NAS — nothing is deleted. |
| **Test (dry run)** | Safe rehearsal. Lists what *would* be copied, copies nothing. The progress bar follows it too, marked "Dry run". |
| **Check connectivity** | Tests that each path exists and each host answers on port 445 (3-second timeout). |
| **Check credentials** | Says whether Credential Manager has an entry per host. Never shows passwords. Has a button to open Credential Manager. |
| **Dark mode** | Switches the window between light and dark. CCJ remembers your choice. |
| **↻** | Refreshes the dashboard and the log view. |

Switching a job on or off is not a button: it is the **Enabled** box next to that job on the
Jobs tab, and the Dashboard follows it immediately.

---

## 5. How do I…

**…test a job safely before trusting it?**
Select the row → **Test (dry run)** → review the script → confirm. Robocopy runs with `/L`, so
it only lists files. Nothing is copied, the NAS folder is not created, and logs go to
`%TEMP%\CCJ_DryRun`. You get the exit code and the last log lines when it finishes. **Do this
first, every time you change a job.**

**…run a backup right now instead of waiting for Sunday?**
Select the row → **Start copy** → check the source, destination, file count and the
**To copy now** line → confirm. The task must already exist (press **Save** first if the Task
column says `missing`). The Dashboard shows the progress bar as soon as it starts.

**…pause a copy that is slowing the network down?**
Select the row → **Pause**. The bar turns grey and says *Paused*; robocopy is frozen where it
is and writes nothing. Press **Resume** to carry on from the same place. Keep pauses short:
after many minutes the server can drop the connection, and the script then has to retry.

**…stop a copy altogether?**
Select the row → **Stop** → confirm. Everything copied so far stays on the NAS; nothing is
deleted. The file that was in mid-copy is finished off on the next run. The run is reported as
*Stopped*, not as a success.

**…add a fourth job, or get rid of one?**
Jobs tab → **Add another job**, fill it in, **Save**. To get rid of one, press **Remove job** on
it: its scheduled task is deleted, and its script file and logs are left alone.

**…see how far a backup has got?**
Look at the **Progress** column on the Dashboard, and click the row for the details underneath.
It also works for a backup you did not start: the scheduled Sunday run shows up by itself when
you open CCJ, even if it began hours earlier. Press **↻** to look again straight away.

**…change a path or a date?**
Jobs tab → edit → **Save**. The scripts are rewritten and the tasks updated.

**…switch a job off without deleting it?**
Jobs tab → clear its **Enabled** box. The task stays but will not fire. The Dashboard shows the
change at once.

**…check it will work before Sunday?**
**Check connectivity**, then **Check credentials**. Both must be clean for a job that runs while
you are logged off.

**…see what happened on the last run?**
Dashboard → *Last result* column, and the *Last log line* column. For detail, Logs tab, or
**Open logs** and read `<Folder>_<timestamp>.log` for a specific run.

**…update things after changing my Windows password?**
Press **Save** and re-enter the new password when prompted. The tasks will not run until you do.

---

## 6. Reading the Dashboard

**Task** column — `enabled`, `disabled`, or `missing` (orange/red). `missing` means no scheduled
task exists yet: press **Save**.

**Progress** column — what the bar means:

| Shown | Meaning |
|---|---|
| `-` | Nothing is running for this job. |
| `Starting...` / `Waiting · attempt 2/6` | The script has started but robocopy has not. It is checking that the source and the NAS can be reached, or waiting 15 minutes to retry. The details underneath give the reason. |
| `37% · 1,234/3,300 files` | Copying. The bar and the percentage count **bytes**, so one huge file weighs more than many small ones. |
| Striped bar, `412 files · counting...` | Copying, but the total is still being worked out. The percentage appears within a moment. |
| `Paused · 37%` (grey) | You pressed **Pause**. Nothing is being written until you press **Resume**. |
| `Done · 3,300 files` (green) | The script reported `SUCCESS`. |
| `Done · nothing to copy` | Everything was already on the NAS. |
| `Failed` (red) | All 6 attempts failed. The details underneath give the last problem. |
| `Stopped` (orange) | The run ended without reporting a result — usually because you pressed **Stop**. What had been copied is still on the NAS. |
| `Dry run · ...` | The same, for **Test (dry run)**. Nothing is copied. |

The details under the grid show the file being copied right now, how long it has been on that
file, the speed, and about how long is left. A **warning line** appears if robocopy is hitting
errors (a locked file, a dropped connection); it retries by itself, 10 times, 30 seconds apart.

**Good to know about the bar:**

- **It moves file by file, not smoothly.** robocopy does not say how much of the current file is
  written, so a very large file keeps the bar still until it finishes. The "now copying" line
  and its timer show that it is alive.
- **It never shows 100% until the run is really over.** A full bar means the script reported
  `SUCCESS`.
- **"Total unknown"** means this PC could not read the folders to work out the total (for
  example, no stored credentials for the NAS in your session). The files are still copied and
  still counted; there is just no percentage.
- **Closing CCJ does not stop a backup.** It runs under Task Scheduler. Open CCJ again and the
  bar picks it up where it is. A copy you paused stays paused, so do not close CCJ on a paused
  job — resume or stop it first.
- Jobs that share the same NAS folder name cannot be told apart in the logs, so their progress
  can be mixed up. Give each job its own NAS folder.

**Last result** column:

| Shown | Meaning |
|---|---|
| `0x0 = success` | The last run worked. |
| `no task` / `never run` | Nothing has run yet. Normal for a new job. |
| `0x41301 = currently running` | It is running now. A job can take up to ~1¼ hours because of retries. |
| `0x41303 = has never run` | Normal before the first Sunday. |
| anything else, in red | It failed. See the troubleshooting table in `README.md`. |

---

## 7. What the backup actually does

When a job runs it tries up to **6 times, 15 minutes apart**. Each attempt checks that the NAS
root and the source are reachable, then runs:

```
robocopy "<source>" "<destination>" /E /Z /XJ /R:10 /W:30 /COPY:DAT /DCOPY:T /NP /LOG:<file>
```

It writes `SUCCESS` or `FAILED` to `Logs\master.log`, plus a detailed log per run. A whole job
can therefore take up to about 1 hour 15 minutes before it gives up.

---

## 8. Rules the app enforces

- Source and NAS root must be UNC paths (`\\server\share`). Spaces are fine
  (`\\192.168.100.100\3G eng`). A drive letter like `C:\Vox` is rejected — a scheduled task
  running logged-off cannot rely on mapped drives.
- The NAS folder name must be one plain folder name: no `\ / : * ? " < > |`, no reserved names
  (`CON`, `NUL`, `COM1`…), no leading or trailing spaces or dots.
- The first run must be a **Sunday**; the time is yours to choose.
- Two jobs cannot share a name, because the name becomes the scheduled task name.
- At least one job has to be filled in.

If anything is invalid, **nothing** is written — not the scripts, not the settings. A job left
completely empty is skipped rather than reported as invalid.

---

## 9. Where things are

| What | Where |
|---|---|
| Generated scripts | `<Script and log folder>\Backup_<job>.bat`, one per job |
| Logs | `<Script and log folder>\Logs\master.log` plus one file per run |
| Your settings | `%APPDATA%\CCJ\jobs.json` |
| Light or dark mode | `%APPDATA%\CCJ\ui.json` |
| App error log | `%APPDATA%\CCJ\app.log` |
| Dry-run output | `%TEMP%\CCJ_DryRun` |
| Scheduled tasks | Task Scheduler Library → `CCJ Backup <job name>` |

---

## 10. Safety

- The generated scripts never use `/MIR` or `/PURGE`. CCJ refuses to write a script that
  contains either, so nothing on the NAS can be deleted.
- No real copy starts without a confirmation showing the source, the destination and the file
  count.
- Scripts are always shown to you before they are written.
- **Stop** never deletes anything: it ends the copy, and the files already on the NAS stay.
- Your Windows password is never written to disk.

**If a run fails:** check `Last result` on the Dashboard, then the Logs tab. The
troubleshooting tables in `README.md` explain every result code and every `master.log` message.
