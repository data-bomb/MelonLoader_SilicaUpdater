# MelonLoader_UniversalUpdater
Universal updater plugin for MelonLoader
Checks each DLL file in the Mods\ directory for the assemblyInfo 
optional downloadLink URL. If a downloadLink URL is found then it 
will try and check for an updater.json file at the URL

https://yourdownloadlink.com/<mod namespace>/updater.json

updater.json is of the format:
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

if updater.json is found and the json Version is higher than the 
assemblyInfo version then it will download the copy from 

https://yourdownloadlink.com/<mod namespace>/bin/<mod namespace>.dll

and place the updated version in the Mods\ directory
