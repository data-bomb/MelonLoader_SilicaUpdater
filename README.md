# MelonLoader Updater Plugin
An updater plugin for MelonLoader on both Il2Cpp and Mono. While built to support Silica, this repo could be used to quickly generate a mod loader for any game.

### Mod Prequisites
- Each Mod using the updater needs to have the optional downloadLink parameter specified in the assembly info (https://melonwiki.xyz/#/modders/attributes)
(e.g., `[assembly: MelonInfo(typeof(SurrenderCommand), "[Si] Surrender Command", "1.1.8", "databomb", "https://github.com/data-bomb/Silica")]`)
- The latest release of the GitHub repo should either have a DLL or a Zip file included

> [!TIP]
> If a GitHub address is used as the downloadLink it should be of the format `https://github.com/<username>/<repo>`

### DLL Direct Updater Method
- The plugin will look for a DLL filename in the latest releases that matches the mod filename

### Zip Updater Method
- The plugin will look for Zip files that start with either `Listen` or `Dedicated` and download the Zip that matches the current run-time environment
- The plugin will scan all the files in the Zip to see if there are matches to the associated mod filenames

### Version Comparison
- Once downloaded, the assembly MelonLoader information is scanned and compared with what is currently in the `Mods\` directory
- If the downloaded version is newer then it replaces the DLL in the `Mods\` directory
- If not then, then nothing happens
- Files temporarily stored in the `Mods\temp\` directory are removed after all version comparisons have completed
