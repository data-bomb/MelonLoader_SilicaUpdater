/*
Universal Mod Updater Plugin
Copyright (C) 2023-2024 by databomb

* Description *
Checks each DLL file in the Mods\ directory for the assemblyInfo 
optional downloadLink URL. If a downloadLink URL is found then it 
will try and check for an updater.json file and download the latest
version, if needed.

* License *
This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

#if NET6_0
using Il2CppSteamworks;
#else
using Steamworks;
#endif

using MelonLoader;
using Newtonsoft.Json;
using MelonLoader.Utils;
using UniversalUpdater;
using Mono.Cecil;
using System.Net.Http.Headers;
using System.Net.Http;
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;

[assembly: MelonInfo(typeof(Updater), "Universal Mod Updater", "2.0.6", "databomb", "https://github.com/data-bomb/MelonLoader_UniversalUpdater")]
[assembly: MelonGame(null, null)]

namespace UniversalUpdater
{
    public class Updater : MelonPlugin
    {
        public static readonly string modsPath = MelonEnvironment.ModsDirectory;
        public static readonly string modsBackupDirectory = Path.Combine(modsPath, @"backup\");
        public static readonly string modsTemporaryDirectory = Path.Combine(modsPath, @"temp\");
        public static HttpClient updaterClient = null!;

        // initialize steam
        public override void OnPreInitialization()
        {
            try
            {
                SteamAPI.Init();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning(ex.ToString());
            }
        }

        public override void OnApplicationEarlyStart()
        {
            try
            {
                Methods.InitializeWebClient();
                Dictionary<string, List<ModInfo>> modDownloadList = Methods.GenerateDownloadList();
                Methods.InitializeTemporaryDirectory();

                // loop through all unique download links
                foreach (var thisDownloadTable in modDownloadList)
                {
                    List<ModInfo> modList = thisDownloadTable.Value;

                    // if it's github then process it
                    if (!Methods.IsGitHubLink(thisDownloadTable.Key))
                    {
                        MelonLogger.Msg("Skipping checking updates for non-GitHub download URL [" + thisDownloadTable.Key + "]");
                        continue;
                    }

                    JObject releaseJsonObject = Methods.GetGitHubReleaseJson(thisDownloadTable.Key);

                    // search all files available for download at each GitHub repo's latest releases page
                    foreach (JObject asset in releaseJsonObject["assets"]!)
                    {
                        asset.TryGetValue("name", out var releaseAssetName);
                        asset.TryGetValue("browser_download_url", out var releaseDownloadURL);
                        if (releaseAssetName == null || releaseDownloadURL == null)
                        {
                            MelonLogger.Warning("Could not find valid release data for: " + asset.ToString());
                            continue;
                        }

                        // is this a zip file or a DLL?
                        if (Methods.IsZipAsset(releaseAssetName.ToString()))
                        {
                            MelonLogger.Msg("Found zip file. Skipping for now.");
                            continue;
                        }

                        // see if we have any direct matches from the mod files
                        foreach (var modInfo in modList)
                        {
                            if (string.Equals(releaseAssetName.ToString(), modInfo.FileInfo.Name, StringComparison.OrdinalIgnoreCase))
                            {
                                string temporaryFilePath = Path.Combine(modsTemporaryDirectory, modInfo.FileInfo.Name);

                                MelonLogger.Msg("Downloading temporary files for " + modInfo.FileInfo.Name + "...");
                                Methods.DownloadFile(updaterClient, releaseDownloadURL.ToString(), temporaryFilePath);

                                MelonInfoAttribute? tempModAttributes = Methods.GetMelonModAttributes(temporaryFilePath);
                                if (tempModAttributes == null)
                                {
                                    MelonLogger.Warning("Could not find MelonMod attributes for " + temporaryFilePath);
                                    continue;
                                }

                                // do we already have the latest version?
                                if (!Methods.IsNewerVersion(modInfo.Version, tempModAttributes.Version))
                                {
                                    MelonLogger.Msg("Skipping update for " + modInfo.FileInfo.Name + ". Version " + tempModAttributes.Version + " is the latest.");
                                    continue;
                                }

                                MelonLogger.Msg("Updating " + modInfo.FileInfo.Name + " to version " + tempModAttributes.Version + "...");

                                Methods.MakeModBackup(modInfo.FileInfo, modInfo.Version);
                                System.IO.File.Copy(temporaryFilePath, modInfo.FileInfo.FullName, true);
                            }
                        }
                    }
                }

                updaterClient.Dispose();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning(ex.ToString());
            }
        }
    }
}