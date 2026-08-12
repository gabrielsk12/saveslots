using MSCLoader;

namespace MwcSaveSlots
{
internal sealed class SaveSlotsConsoleCommand : ConsoleCommand
{
	private readonly MwcSaveSlotsMod mod;

	internal SaveSlotsConsoleCommand(MwcSaveSlotsMod mod)
	{
		this.mod = mod;
	}

	public override string Name { get { return "saveslots"; } }
	public override string Alias { get { return "ss"; } }
	public override string Help { get { return "Save Slots diagnostics: status, show, open, close, refresh, backups, log, help"; } }
	public override bool ShowInHelp { get { return true; } }

	public override void Run(string[] args)
	{
		mod.RunConsoleCommand(args);
	}
}
}
