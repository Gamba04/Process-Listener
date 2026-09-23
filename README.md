# Process Listener
Process Listener is an [extremely lightweight](#performance) utility that allows you to open files or programs whenever a specific process is created.

For example, you may configure a background utility to silently launch every time you open a program. That way, you don't need to always have it running in the background, but instead you can just launch it only when it's necessary.

A scripting tool such as [AutoHotkey](https://www.autohotkey.com/) can pair really well with this utility, since you can use it to perform automated tasks like maximizing a window (some programs don't remember the maximized state on launch) or moving it to a specific part of the screen, or to a secondary monitor, or even much more complex stuff. This tool merely acts as an entry point to automate behaviours without wasting background resources by having them running all the time.

# How it works
In the same directory as the executable, you must add a file named `Config.txt`, in which you will specify the names of the processes to listen to and the path of the program or file to execute.

> [!NOTE]
> File paths are relative to the current directory, but shortcuts can be used to point to external locations (e.g. `Relative\Path\Shortcut` or `Relative\Path\Shortcut.lnk`)

# Config.txt
The contents of the file must match the following pattern for each line:<br>
$\textcolor{Gray}{\textsf{[OptionalWhitespace]}}\textsf{\color[rgb]{1.0, 0.8, 0.3}[ProcessName]}\textcolor{Gray}{\textsf{[OptionalWhitespace]}}:\textcolor{Gray}{\textsf{[OptionalWhitespace]}}\textsf{\color[rgb]{0.4, 1.0, 0.6}[RelativePath]}\textcolor{Gray}{\textsf{[OptionalWhitespace]}}$

Example:
```
Process.exe         : Programs\Program.exe
OtherProcess.exe    : Shortcuts\Shortcut
VeryLongProcess.exe : Programs\Scripts\Script.ahk
```

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

So basically it does nothing in the background except for the specific moments in which an event for a matching process is received.

> [!Warning]
> This program does not have any form of GUI or tray icon, it is purely a background process. You can verify whether it's running properly by looking for it in the Task Manager (or any similar tool). If the process does not immediately appear after launching it, it means that something has failed. Avoid having duplicate instances and make sure to terminate the process tree if you decide to close it (since it might have child processes running)

# Mechanism
This project leverages the [Windows Management Instrumentation](https://learn.microsoft.com/en-us/windows/win32/wmisdk/wmi-start-page) [Operating System Events](https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/operating-system-classes#operating-system-events) to receive events for process creation and termination.

It is also designed to treat all processes of the same name as one single trigger, following this logic:
- When the first matching process creation is detected, the corresponding file is executed
- All successive creations with that process name are ignored
- When all instances are terminated, the system resets back to the first condition

> [!Warning]
> It is required that this program runs as an administrator, since it is needed for WMI to work properly
