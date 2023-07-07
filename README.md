# MelonLoader Universal Updater Plugin
A universal updater plugin for MelonLoader

Checks each DLL in the Game\Mods\ directory to see if it's outdated and automatically downloads new mod files, if needed.

### Prequisites
- Each plugin using the updater needs to have the assembly info populated first
(e.g., `[assembly: MelonInfo(typeof(SurrenderCommand), "[Si] Surrender Command", "1.1.8", "databomb", "https://github.com/data-bomb/Silica_ListenServer")]`)
- The optional downloadLink parameter must be specified with the URL of where the updater.json file and the DLL will reside
- If a GitHub address is used as the downloadLink it should be of the format `https://github.com/<username>/<repo>`

For GitHub URLs, the mod will check for the updater.json at `https://raw.githubusercontent.com/<username>/<repo>/main/<mod namespace>/updater.json`
For non-GitHub URLs, the mod will check for the updater.json at `https://yourdownloadlink.com/yoursubpath/<mod namespace>/updater.json`

The `updater.json` files are of the format:
```JSON
{
	"Version": "1.1.8",
	"UpdateNotes": "Minor bug fixes",
	"StoreBackup": true,
	"Dependencies": {
		"AdminExtension": {
			"WebLocation": "https://raw.githubusercontent.com/data-bomb/Silica_ListenServer/main/Si_AdminExtension/bin/Si_AdminExtension.dll",
			"LocalLocation": "MelonLoader\net6",
			"ForceUpdate": false
		}
	}
}
```

If the updater.json file is found then the SemVer versions from the assembly and the updater.json are compared to determine if an update is needed.

If the optional parameter `StoreBackup` is set to `true` then before any update occurs the previous DLL is copied into the Game\Mods\backup\ directory.

TODO: Dependencies are planned but not currently supported in the code.
