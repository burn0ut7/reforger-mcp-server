#ifdef WORKBENCH
//! The stock dedicated plugin forwards an unquoted startup directory and omits
//! local projects mounted later. Resolve current project roots at launch time.
class RST_MultiplayerPaths
{
	//------------------------------------------------------------------------------------------------
	static bool TryGetArgument(out string argument)
	{
		argument = "";
		string configured;
		System.GetCLIParam("addonsDir", configured);
		array<string> paths = {};
		configured.Split(",", paths, true);
		array<string> addons = {};
		GameProject.GetLoadedAddons(addons);
		foreach (string addon : addons)
		{
			if (GameProject.IsVanillaAddon(addon))
				continue;
			string path;
			if (!Workbench.GetAbsolutePath("$" + GameProject.GetAddonID(addon) + ":", path) || path.IsEmpty())
				return false;
			if (!paths.Contains(path))
				paths.Insert(path);
		}
		string joined;
		foreach (string directory : paths)
		{
			directory.TrimInPlace();
			if (directory.IsEmpty())
				continue;
			// Quotes cannot occur in a Windows directory name. Reject rather than
			// letting a configured value terminate the quoted command-line argument.
			if (directory.Contains("\"") || directory.Contains("\n") || directory.Contains("\r"))
				return false;
			if (!joined.IsEmpty())
				joined += ",";
			joined += directory;
		}
		if (!joined.IsEmpty())
			argument = "\"" + joined + "\"";
		return true;
	}
}

[WorkbenchPluginAttribute(name: "Dedicated Server Tool", shortcut: "Ctrl+Shift+D", wbModules: { "WorldEditor" }, awesomeFontCode: 0xF233)]
modded class SCR_DedicatedServerPlugin
{
	//------------------------------------------------------------------------------------------------
	override protected string GetAddonsDirCLI()
	{
		string argument;
		if (!RST_MultiplayerPaths.TryGetArgument(argument))
			Print("RST | Dedicated launch paths unavailable: inspect mounted projects and addonsDir.", LogLevel.ERROR);
		return argument;
	}
}
#endif
