using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Security.Principal;
using System.Threading;

public static class Program
{
	private const string config = "Config.txt";

	private static readonly Dictionary<string, Target> data = new Dictionary<string, Target>();
	private static readonly (char, TargetType)[] separators = new (char, TargetType)[]
	{
		('>', TargetType.Trigger),
		('|', TargetType.Linked)
	};

	private static void Main()
	{
		if (TryInit())
		{
			WatchEvents();
			FinishThread();
		}
	}

	#region Init

	private static bool TryInit()
	{
		if (!IsAdministrator())
		{
			RunAsAdministrator();

			return false;
		}

		return TryParseData();
	}

	private static bool IsAdministrator()
	{
		using WindowsIdentity identity = WindowsIdentity.GetCurrent();
		WindowsPrincipal principal = new WindowsPrincipal(identity);

		return principal.IsInRole(WindowsBuiltInRole.Administrator);
	}

	private static void RunAsAdministrator()
	{
		string process = Process.GetCurrentProcess().ProcessName;

		ProcessStartInfo startInfo = new ProcessStartInfo(process)
		{
			UseShellExecute = true,
			Verb = "runas"
		};

		Process.Start(startInfo);
	}

	private static bool TryParseData()
	{
		if (File.Exists(config))
		{
			foreach (string line in File.ReadAllLines(config))
			{
				foreach ((char separator, TargetType type) in separators)
				{
					if (TryParseLine(line, separator, out string process, out string path))
					{
						data.Add(process, new Target(path, type));

						break;
					}
				}
			}
		}

		return data.Count > 0;
	}

	private static bool TryParseLine(string line, char separator, out string process, out string path)
	{
		process = null;
		path = null;

		string[] contents = line.Split(separator);

		if (contents.Length == 2)
		{
			for (int i = 0; i < contents.Length; i++)
			{
				string value = contents[i].Trim();

				if (value != "")
				{
					switch (i)
					{
						case 0: process = value; break;
						case 1: path = value; break;
					}
				}
				else return false;
			}
		}
		else return false;

		return true;
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

	public static void KillProcessTree(int id)
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

		if (data.TryGetValue(process, out Target target))
		{
			target.OnProcessStart();
		}
	}

	private static void OnProcessStop(object sender, EventArrivedEventArgs eventArgs)
	{
		string truncatedProcess = eventArgs.GetProcessName();

		foreach (string process in data.Keys)
		{
			if (process.StartsWith(truncatedProcess) && !Exists(process))
			{
				data[process].OnProcessStop();
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