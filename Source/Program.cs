using System.Collections.Generic;
using System.Diagnostics;
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
		if (TryInit(out string condition))
		{
			Watch("Win32_ProcessStartTrace", condition, OnProcessStart);
			Watch("Win32_ProcessStopTrace", condition, OnProcessStop);

			StTrinaSleep();
		}
	}

	#region Init

	private static bool TryInit(out string condition)
	{
		condition = null;

		if (IsAdministrator() && TryInitData())
		{
			List<string> conditions = new List<string>(data.Count);

			foreach (string key in data.Keys)
			{
				conditions.Add($"ProcessName like '{key.Truncate(14)}%'");
			}

			condition = string.Join(" or ", conditions);
		}

		return condition != null;
	}

	private static bool IsAdministrator()
	{
		using WindowsIdentity identity = WindowsIdentity.GetCurrent();
		WindowsPrincipal principal = new WindowsPrincipal(identity);

		return principal.IsInRole(WindowsBuiltInRole.Administrator);
	}

	private static bool TryInitData()
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

					data.Add(process, program);
					states.Add(process, false);
				}
			}
		}

		return data.Count > 0;
	}

	private static string Truncate(this string value, int length)
	{
		return value.Length > length ? value.Substring(0, length) : value;
	}

	#endregion

	// ----------------------------------------------------------------------------------------------------

	#region Watch

	private static void Watch(string className, string condition, EventArrivedEventHandler handler)
	{
		WqlEventQuery query = new WqlEventQuery(className, condition);
		ManagementEventWatcher watcher = new ManagementEventWatcher(query);

		watcher.EventArrived += handler;
		watcher.Start();
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
		string process = GetProcess(eventArgs);

		if (states.TryGetValue(process, out bool isRunning) && !isRunning)
		{
			Process.Start(data[process]);

			states[process] = true;
		}
	}

	private static void OnProcessStop(object sender, EventArrivedEventArgs eventArgs)
	{
		string truncatedProcess = GetProcess(eventArgs);

		foreach (string process in states.Keys)
		{
			if (process.StartsWith(truncatedProcess) && !Exists(process))
			{
				states[process] = false;
			}
		}
	}

	private static string GetProcess(EventArrivedEventArgs eventArgs)
	{
		return (string)eventArgs.NewEvent.Properties["ProcessName"].Value;
	}

	private static bool Exists(string process)
	{
		int extension = process.LastIndexOf('.');
		string name = process.Remove(extension);

		return Process.GetProcessesByName(name).Length > 0;
	}

	#endregion

}