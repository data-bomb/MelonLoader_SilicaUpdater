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
	"RemoteRelativePath": "bin",
	"UpdateNotes": "Minor bug fixes",
	"StoreBackup": true,
	"Dependencies":
	[
		{
			"Filename": "Si_AdminExtension.dll",
			"RemoteFullPath": "https://raw.githubusercontent.com/data-bomb/Silica_ListenServer/main/Si_AdminExtension/bin",
			"LocalPath": "MelonLoader\\net6",
			"ForceUpdate": false
		},
		{
			"Filename": "LICENSE",
			"RemoteFullPath": "https://raw.githubusercontent.com/data-bomb/Silica_ListenServer/main",
			"LocalPath": "Mods",
			"ForceUpdate": false
		}
	]
}
```

If the updater.json file is found then the SemVer versions from the assembly and the updater.json are compared to determine if an update is needed.

The DLL should be stored in `/{RemotePath}/{ModNamespace}.dll` relative to the updater.json downloadLink path

If the optional parameter `StoreBackup` is set to `true` then before any update occurs the previous DLL is copied into the Game\Mods\backup\ directory.

Dependencies are optional but are only downloaded if the file is missing on the client or if the ForceUpdate is set to true.
