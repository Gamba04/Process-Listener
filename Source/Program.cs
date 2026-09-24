using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Management;
using System.Security.Principal;
using System.Threading;

public static class Program
{
	private const string config = "Config.txt";

	private readonly static Dictionary<string, string> data = new Dictionary<string, string>();
	private readonly static Dictionary<string, bool> states = new Dictionary<string, bool>();

	private static void Main()
	{
		if (TryInit())
		{
			WatchEvents();
			FinishThread();
		}
	}

	#region Init

	private static bool TryInit() => IsAdministrator() && TryParseData();

	private static bool IsAdministrator()
	{
		using WindowsIdentity identity = WindowsIdentity.GetCurrent();
		WindowsPrincipal principal = new WindowsPrincipal(identity);

		return principal.IsInRole(WindowsBuiltInRole.Administrator);
	}

	private static bool TryParseData()
	{
		if (File.Exists(config))
		{
			foreach (string line in File.ReadAllLines(config))
			{
				string[] contents = line.Split(':');

				if (contents.Length == 2)
				{
					string process = contents[0].Trim();
					string program = contents[1].Trim();

					if (ValidateContents(process, program))
					{
						data.Add(process, program);
						states.Add(process, false);
					}
				}
			}
		}

		return data.Count > 0;
	}

	private static bool ValidateContents(params string[] contents)
	{
		return !contents.Any(content => content == "");
	}

	#endregion

	// ----------------------------------------------------------------------------------------------------

	#region Watch

	private static void WatchEvents()
	{
		string condition = GetCondition();

		Watch("Win32_ProcessStartTrace", condition, OnProcessStart);
		Watch("Win32_ProcessStopTrace", condition, OnProcessStop);
	}

	private static string GetCondition()
	{
		List<string> conditions = new List<string>(data.Count);

		foreach (string key in data.Keys)
		{
			conditions.Add($"ProcessName like '{key.Truncate(14)}%'");
		}

		return string.Join(" or ", conditions);
	}

	private static string Truncate(this string value, int length)
	{
		return value.Length > length ? value.Substring(0, length) : value;
	}

	private static void Watch(string className, string condition, EventArrivedEventHandler handler)
	{
		WqlEventQuery query = new WqlEventQuery(className, condition);
		ManagementEventWatcher watcher = new ManagementEventWatcher(query);

		watcher.EventArrived += handler;
		watcher.Start();
	}

	#endregion

	// ----------------------------------------------------------------------------------------------------

	#region Finish

	private static void FinishThread()
	{
		KillInstances();
		StTrinaSleep();
	}

	private static void KillInstances()
	{
		Process process = Process.GetCurrentProcess();
		Process[] instances = Process.GetProcessesByName(process.ProcessName);

		foreach (Process instance in instances)
		{
			if (instance.Id != process.Id)
			{
				KillProcessTree(instance.Id);
			}
		}
	}

	private static void KillProcessTree(int id)
	{
		SelectQuery query = new SelectQuery("Win32_Process", $"ParentProcessID = {id}");
		ManagementObjectSearcher searcher = new ManagementObjectSearcher(query);

		foreach (ManagementBaseObject obj in searcher.Get())
		{
			KillProcessTree(obj.GetProcessID());
		}

		Process.GetProcessById(id).Kill();
	}

	private static int GetProcessID(this ManagementBaseObject obj)
	{
		return Convert.ToInt32(obj["ProcessID"]);
	}

	private static void StTrinaSleep()
	{
		Thread.Sleep(Timeout.Infinite);
	}

	#endregion

	// ----------------------------------------------------------------------------------------------------

	#region Events

	private static void OnProcessStart(object sender, EventArrivedEventArgs eventArgs)
	{
		string process = eventArgs.GetProcessName();

		if (states.TryGetValue(process, out bool isRunning) && !isRunning)
		{
			Process.Start(data[process]);

			states[process] = true;
		}
	}

	private static void OnProcessStop(object sender, EventArrivedEventArgs eventArgs)
	{
		string truncatedProcess = GetProcessName(eventArgs);

		foreach (string process in states.Keys)
		{
			if (process.StartsWith(truncatedProcess) && !Exists(process))
			{
				states[process] = false;
			}
		}
	}

	private static string GetProcessName(this EventArrivedEventArgs eventArgs)
	{
		return (string)eventArgs.NewEvent["ProcessName"];
	}

	private static bool Exists(string process)
	{
		int extension = process.LastIndexOf('.');
		string name = process.Remove(extension);

		return Process.GetProcessesByName(name).Length > 0;
	}

	#endregion

}