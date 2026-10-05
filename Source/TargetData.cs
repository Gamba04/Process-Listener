using System.Diagnostics;
using System.Linq;

public enum TargetType
{
	Trigger,
	Linked
}

public class TargetData
{
	public readonly string target;
	public readonly TargetType type;

	private int id;

	public TargetData(string target, TargetType type)
	{
		this.target = target;
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
		return Process.Start(target).Id;
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