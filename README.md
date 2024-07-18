# MelonLoader Updater Plugin
An updater plugin for MelonLoader on both Il2Cpp and Mono. While built to support Silica, this repo could be used to quickly generate a mod loader for any game.

### Mod Prequisites
- Each Mod using the updater needs to have the optional downloadLink parameter specified in the assembly info
(e.g., `[assembly: MelonInfo(typeof(SurrenderCommand), "[Si] Surrender Command", "1.1.8", "databomb", "https://github.com/data-bomb/Silica")]`)
- The latest release of the GitHub repo should either have a DLL or a Zip file included

> [!TIP]
> If a GitHub address is used as the downloadLink it should be of the format `https://github.com/<username>/<repo>`

