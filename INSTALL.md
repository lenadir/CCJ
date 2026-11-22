# Installing CCJ on another PC

## The short version

Copy **one file** — `dist\CCJ-Setup.exe` (1.2 MB) — to the other PC and **double-click it**.
Approve the Windows prompt, press **Install**. That is the whole installation.

Everything CCJ needs is inside that one file. Nothing is downloaded, and no other file has to
travel with it.

---

## What the installer does

| Step | Detail |
|---|---|
| Checks .NET Framework 4.8 | Stops with a clear message if it is missing, instead of letting CCJ fail later. |
| Installs the program | `C:\Program Files\CCJ` by default. |
| Creates the working folder | `C:\Vox` and `C:\Vox\Logs`, for the generated scripts and the logs. |
| Adds shortcuts | Start menu and desktop. |
| Registers an uninstaller | CCJ appears in **Settings → Apps → Installed apps**. |
| Copies the documentation | `USER-MANUAL.md`, `README.md` and this file land next to the program. |

Both folders are shown in the window before you press Install, and both can be changed with
**Browse…**.

It does **not** create or change any backup job, and it never touches the NAS. You do that
afterwards, inside CCJ.

### The one Windows prompt

The installer asks for administrator rights once, up front, because it writes to Program Files
and registers the uninstaller. You may also see SmartScreen ("Windows protected your PC")
because the file is not code-signed — choose **More info → Run anyway**.

---

## After installing: three things to finish

The installer puts CCJ on the PC. These three steps make the backups actually run. Full detail
is in `USER-MANUAL.md`, which is installed alongside the program.

**1. Set up the jobs.** Start CCJ **as administrator** (right-click → Run as administrator — it
needs this to create scheduled tasks). On the **Jobs** tab fill in the source UNC, NAS root UNC,
NAS folder and a Sunday date for each of the three jobs, tick **Enabled**, then press **Save**
and enter the Windows password of the account the backups run as.

**2. Store the NAS credentials.** Signed in as *that same account*, open Credential Manager
(CCJ's **Check credentials** button opens it) and add a Windows credential for each of the two
hosts. A scheduled task running while logged off inherits nothing from your interactive session.
The account also needs a **non-blank password**.

**3. ⚠ Disable the old scheduled tasks.** If this PC already runs the backups from hand-made
tasks, disable or delete them in `taskschd.msc`. CCJ creates its own tasks named
`CCJ Backup …`; leaving the old ones enabled means every job runs twice. Look for any task whose
action points at `Backup_Folder1.bat`, `Backup_Folder2.bat` or `Backup_Folder3.bat`.

Then verify, in this order: **Check connectivity** → **Check credentials** → **Test (dry run)**
on each job. The dry run copies nothing.

---

## Unattended install (for several PCs)

```cmd
CCJ-Setup.exe /S
```

Silent, with the default folders. Options:

| Option | Meaning |
|---|---|
| `/S` | Silent, no window. |
| `/dir=<path>` | Install folder instead of `C:\Program Files\CCJ`. |
| `/vox=<path>` | Script and log folder instead of `C:\Vox`. |
| `/noshortcut` | Skip the Start menu and desktop shortcuts. |

```cmd
CCJ-Setup.exe /S /dir="D:\Tools\CCJ" /vox="D:\Vox"
```

Run it from an elevated prompt; it returns exit code `0` on success. Jobs still have to be
configured per PC, in the app or by copying `jobs.json` (below).

---

## Copying your settings from the old PC (optional)

Settings are per-user and do not travel with the installer. To carry them over, copy

```
%APPDATA%\CCJ\jobs.json
```

to the same path on the new PC, then open the **Jobs** tab and correct the **Script and log
folder** — the copied file still points at the old PC's path. Press **Save** so the scripts and
tasks are regenerated locally.

Otherwise just retype the three jobs. It takes a minute.

---

## Updating to a newer build

Run the newer `CCJ-Setup.exe` over the top. Close CCJ first, or the installer will tell you the
program is in use. Your `jobs.json` and your logs are left alone. Press **Save** once afterwards
so the `.bat` files are regenerated from the new template.

---

## Uninstalling

**Settings → Apps → Installed apps → CCJ → Uninstall**, or run `Uninstall-CCJ.exe` from the
install folder. Silent: `Uninstall-CCJ.exe /uninstall /S`.

It removes the program, the shortcuts, the Apps entry and the three `CCJ Backup` scheduled
tasks. It deliberately **keeps** your logs and generated scripts in `C:\Vox`, and your settings
in `%APPDATA%\CCJ`. **Nothing on the NAS is ever touched.**

---

## Manual install, without the installer

If you would rather not run an installer, copy these four files from `dist\` into any folder,
keeping them together, and run `CCJ.exe`:

```
CCJ.exe   CCJ.exe.config   Microsoft.Win32.TaskScheduler.dll   Newtonsoft.Json.dll
```

Then create `C:\Vox\Logs` yourself. If the files arrived by share, USB, email or zip, clear the
"downloaded from another computer" mark first or the DLLs may refuse to load:

```powershell
Get-ChildItem 'C:\path\to\CCJ' -Recurse | Unblock-File
```

---

## Prerequisite detail

**.NET Framework 4.8 or later.** Windows 10 version 21H2 and later — including **Windows 10 IoT
Enterprise LTSC 2021** — already include it, so normally there is nothing to install. To check:

```powershell
(Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full').Release
```

`528040` or higher means 4.8 is present. If it is lower or missing, install the **.NET Framework
4.8 Runtime** from Microsoft and reboot. The installer performs this same check and refuses to
continue without it.

---

## Building the installer yourself

```cmd
build-installer.cmd
```

Builds the app, then compiles `dist\CCJ-Setup.exe` with the program, its two DLLs and the
documentation embedded as resources. Needs only .NET Framework 4.8; Visual Studio is not
required. Source is in `installer\`.

---

## Quick reference

| Item | Location |
|---|---|
| Program | `C:\Program Files\CCJ` (configurable) |
| Scripts and logs | `C:\Vox` and `C:\Vox\Logs` (configurable) |
| Settings | `%APPDATA%\CCJ\jobs.json` (per user) |
| Error log | `%APPDATA%\CCJ\app.log` |
| Scheduled tasks | `taskschd.msc` → `CCJ Backup <job name>` (per machine) |
| Uninstaller | `<install folder>\Uninstall-CCJ.exe` |
