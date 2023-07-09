# MelonLoader Universal Updater Plugin
A universal updater plugin for MelonLoader

Checks each DLL in the Game\Mods\ directory to see if it's outdated and automatically downloads new mod files, if needed.

### Prequisites
- Each plugin using the updater needs to have the optional downloadLink parameter specified in the assembly info
(e.g., `[assembly: MelonInfo(typeof(SurrenderCommand), "[Si] Surrender Command", "1.1.8", "databomb", "https://github.com/data-bomb/Silica_ListenServer")]`)
- If a GitHub address is used as the downloadLink it should be of the format `https://github.com/<username>/<repo>`

For GitHub URLs, the mod will check for the updater.json at `https://raw.githubusercontent.com/<username>/<repo>/main/<namespace>/updater.json`

For non-GitHub URLs, the mod will check for the updater.json at `https://yourdownloadlink.com/yoursubpath/<namespace>/updater.json`

One `updater.json` file is needed for each mod and the format looks like:
```JSON
{
	"Version": "1.1.8",
	"RemotePath": "bin",
	"UpdateNotes": "Minor bug fixes",
	"StoreBackup": true,
	"Dependencies":
	{
		"Si_AdminExtension.dll":
		{
			"RemoteURL": "https://raw.githubusercontent.com/data-bomb/Silica_ListenServer/main/Si_AdminExtension/bin/Si_AdminExtension.dll",
			"LocalPath": "MelonLoader\net6",
			"ForceUpdate": false
		},
		"LICENSE":
		{
			"RemoteURL": "https://raw.githubusercontent.com/data-bomb/Silica_ListenServer/main/LICENSE",
			"LocalPath": "MelonLoader\net6",
			"ForceUpdate": false
		}
	}
}
```

If the updater.json file is found then the SemVer versions from the assembly and the updater.json are compared to determine if an update is needed.

The DLL should be stored in `/bin/<mod namespace>.dll` relative to the updater.json location

If the optional parameter `StoreBackup` is set to `true` then before any update occurs the previous DLL is copied into the Game\Mods\backup\ directory.

TODO: Dependencies are planned but not currently supported in the code.
