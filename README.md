# Process Listener
Process Listener is an [extremely lightweight](#performance) utility for Windows that allows you to open files, programs and more whenever a specific process is created.

For example, you may configure a background utility to silently launch every time you open a program. That way, you don't need to always have it running in the background or manually open and close it yourself. Instead, it will be automatically launched alongside that program.

A scripting tool such as [AutoHotkey](https://www.autohotkey.com/) can pair really well with this utility, since you can use it to perform automated tasks like maximizing a window (some programs don't remember the maximized state on launch) or moving it to a specific part of the screen, or to a secondary monitor, or even much more complex stuff. This tool merely acts as an entry point to automate behaviours without wasting background resources by having them running all the time.

# How it works
In the same directory as the executable, you must add a file named `Config.txt`, in which you will specify for each case the name of the process to listen to, a separator and the target path to execute.

Target paths can be a lot of different things, so here's a few examples:
| Type				| Example									|
| -----------------	| -----------------------------------------	|
| Relative paths	| `Relative\Path\Program.exe`				|
| Absolute paths	| `C:\Program Files\Program\Program.exe`	|
| Files				| `File.txt`								|
| Shortcuts			| `Shortcut` or `Shortcut.lnk`				|
| PATH variables	| `notepad` or `notepad.exe`				|
| Websites			| `https://github.com`						|
| [Shell Commands](https://windowsloop.com/windows-shell-commands/#shell-commands-list) | `shell:SendTo` |
| [App URI Commands](https://windowsloop.com/list-of-app-uri-commands-windows-10/#first-party) | `calculator:` |

<br>The exact logic is that when a process with the specified name is created for the first time after Process Listener is running, the target path will be executed. This will happen regardless of whether there was already an existing process with that name. After this, as long as there are one or more existing processes with that same name, the target won't be executed on new process creations. When all the processes are fully closed[^1], the cycle resets, and when the process appears again, the target path will be triggered once more.

[^1]: Due to WMI, this might take a few seconds to register in Process Listener (as well as process creation). So if you close and reopen a program very quickly, Process Listener won't recognize that (I'll see if I can fix this in the future).

There are two separators that you can use, and depending on the one you choose, the behavior changes slightly:
| Separator	| Type		| Description																	|
| ---------	| ---------	| -----------------------------------------------------------------------------	|
| >			| Trigger	| Just triggers the target 														|
| \|		| Linked	| Triggers the target, but also kills it[^2] when the process is fully closed	|

[^2]: How this works is that the PID of whatever process was created as a result of the target path is stored, and when the listened process is fully closed, the process tree of that PID is what is going to be killed (if it still exists). This might be useful if the target is a script or program that creates other processes (if they are created as children, which should be the case), so they all will be killed.

# Config.txt
The contents of the file must match the following pattern for each line:<br>
$\textsf{\color{Gray}OptionalWhitespace}\ \textsf{\color[rgb]{0.8, 1.0, 0.3}ProcessName}\ \textsf{\color{Gray}OptionalWhitespace}\ \textsf{\color[rgb]{0.4, 1.0, 0.6}Separator}\ \textsf{\color{Gray}OptionalWhitespace}\ \textsf{\color[rgb]{0.3, 1.0, 1.0}TargetPath}\ \textsf{\color{Gray}OptionalWhitespace}$

Example:
```
Process.exe         > Programs\Program.exe
OtherProcess.exe    > Shortcuts\Shortcut
VeryLongProcess.exe | Programs\Scripts\Script.ahk
```

> [!Note]
> Whitespace can either be spaces or tabs.

# Performance
As I mentioned earlier, this program is extremely lightweight to run in the background, but how much exactly? well, this is what the main thread does after initialization:
```C#
private static void StTrinaSleep()
{
	Thread.Sleep(Timeout.Infinite);
}
```
<details>
	<summary> This silly name is of course a reference to... <b>(Elden Ring spoiler)</b> </summary>
	 St. Trina's Eternal Sleep
</details>

So basically it does nothing[^3] in the background except for the specific moments in which an event for a matching process is received.

[^3]: This is for the process itself, but WMI might technically be executing additional instructions in the background. However, I couldn't measure any increase in CPU usage at all from the WMI processes.

> [!Warning]
> This program does not have any form of GUI or tray icon, it is purely a background process. You can verify whether it's running properly by looking for it in the Task Manager (or any similar tool). If the process does not immediately appear after launching it, it means that something has failed. You can restart the program by launching it again (which should automatically replace the previous instance by killing it's process tree), but you must manually kill it yourself if you want to just close the current instance.

# Mechanism
This project leverages the [Windows Management Instrumentation](https://learn.microsoft.com/en-us/windows/win32/wmisdk/wmi-start-page) [Operating System Events](https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/operating-system-classes#operating-system-events) to receive asynchronous events for process creation and termination.

This program will attempt to self-elevate at launch, since it is required for interacting with WMI. However, since the initial instance does not start elevated, you can launch Process Listener at startup by just placing a shortcut in the Startup folder, located at either of these paths:
- `%ProgramData%\Microsoft\Windows\Start Menu\Programs\Startup`
- `%AppData%\Microsoft\Windows\Start Menu\Programs\Startup`

For the best experience, consider disabling UAC prompts so that Process Listener can run silently at startup (at your own risk).
