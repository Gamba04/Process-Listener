using System.Diagnostics;
using System.Linq;

public enum TargetType
{
	Trigger,
	Linked
}

public class Target
{
	private readonly string path;
	private readonly TargetType type;

	private int id;

	public Target(string path, TargetType type)
	{
		this.path = path;
		this.type = type;
	}

	#region Events

	public void OnProcessStart()
	{
		if (id == 0)
		{
			id = Start();
		}
	}

	public void OnProcessStop()
	{
		if (type == TargetType.Linked && id > 0)
		{
			Stop();
		}

		id = 0;
	}

	#endregion

	// ----------------------------------------------------------------------------------------------------

	#region Other

	private int Start()
	{
		return Process.Start(path).Id;
	}

	private void Stop()
	{
		if (Process.GetProcesses().Any(process => process.Id == id))
		{
			Program.KillProcessTree(id);
		}
	}

	#endregion

}