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

[assembly: MelonInfo(typeof(Updater), "Universal Mod Updater", "2.0.5", "databomb", "https://github.com/data-bomb/MelonLoader_UniversalUpdater")]
[assembly: MelonGame(null, null)]

namespace UniversalUpdater
{
    public class Updater : MelonPlugin
    {
        public static readonly string modsBackupDirectory = Path.Combine(MelonEnvironment.ModsDirectory, @"backup\");
        public static readonly string modsTemporaryDirectory = Path.Combine(MelonEnvironment.ModsDirectory, @"temp\");

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
                string modsPath = MelonEnvironment.ModsDirectory;
                DirectoryInfo modsDirectory = new DirectoryInfo(modsPath);
                FileInfo[] modFiles = modsDirectory.GetFiles("*.dll");

                HttpClient updaterClient = new HttpClient();
                updaterClient.DefaultRequestHeaders.Add("User-Agent", "MelonUpdater");
                updaterClient.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue
                {
                    NoCache = true,
                    Private = true,
                    NoStore = true
                };

                Dictionary<string, List<ModInfo>> modDownloadList = new Dictionary<string, List<ModInfo>>();

                for (int i = 0; i < modFiles.Length; i++)
                {
                    FileInfo thisMod = modFiles[i];

                    // read assembly info without loading the mod
                    MelonInfoAttribute? modAttributes = Methods.GetMelonModAttributes(thisMod.FullName);
                    if (modAttributes == null)
                    {
                        MelonLogger.Warning("Could not find MelonMod attributes for " + thisMod.Name);
                        continue;
                    }

                    MelonLogger.Msg(modAttributes.Name + " " + modAttributes.Version + " " + modAttributes.Author + " " + modAttributes.DownloadLink);

                    if (modAttributes.DownloadLink == null)
                    {
                        MelonLogger.Msg("No download URL found for " + thisMod.Name);
                        continue;
                    }

                    if (!modAttributes.DownloadLink.StartsWith("http"))
                    {
                        MelonLogger.Warning("Invalid URL found for " + thisMod.Name);
                        continue;
                    }

                    // should we track a new (unique) download link?
                    if (!modDownloadList.ContainsKey(modAttributes.DownloadLink))
                    {
                        List<ModInfo> modList = new List<ModInfo>();
                        ModInfo modInfoEntry = new ModInfo()
                        {
                            FileInfo = thisMod,
                            Version = modAttributes.Version
                        };

                        modList.Add(modInfoEntry);
                        modDownloadList.Add(modAttributes.DownloadLink, modList);
                    }
                    else
                    {
                        ModInfo modInfoEntry = new ModInfo()
                        {
                            FileInfo = thisMod,
                            Version = modAttributes.Version
                        };

                        modDownloadList[modAttributes.DownloadLink].Add(modInfoEntry);
                    }

                }

                // loop through all unique download links
                foreach (var thisDownloadTable in modDownloadList)
                {
                    MelonLogger.Warning("Found download link: " + thisDownloadTable.Key);
                    List<ModInfo> modList = thisDownloadTable.Value;

                    // if it's github then download the releases
                    string repoOwner = thisDownloadTable.Key.Split('/')[3];
                    string repoName = thisDownloadTable.Key.Split('/')[4];

                    string downloadReleaseURL = $"https://api.github.com/repos/{repoOwner}/{repoName}/releases/latest";

                    /*HTTPRequestHandle gitHubJsonRequest = SteamHTTP.CreateHTTPRequest(EHTTPMethod.k_EHTTPMethodGET, downloadReleaseURL);
                    SteamAPICall_t gitHubJsonCall = new SteamAPICall_t();
                    SteamHTTP.SendHTTPRequest(gitHubJsonRequest, out gitHubJsonCall);
                    OnHTTPRequestCompletedCallResult.Set(gitHubJsonCall);*/

                    string urlText = updaterClient.GetStringAsync(downloadReleaseURL).Result;
                    dynamic jsonText = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(urlText)!;

                    // look for a direct name match
                    JObject jsonObject = JObject.Parse(urlText);
                    foreach (JObject asset in jsonObject["assets"]!)
                    {
                        if (asset.GetValue("name") == null)
                        {
                            MelonLogger.Warning("Could not find asset name for: " + asset.ToString());
                            continue;
                        }

                        //dynamic assetJson = Newtonsoft.Json.JsonConvert.DeserializeObject(asset)
                        asset.TryGetValue("name", out var releaseAssetName);
                        asset.TryGetValue("browser_download_url", out var releaseDownloadURL);
                        if (releaseAssetName == null || releaseDownloadURL == null)
                        {
                            continue;
                        }

                        MelonLogger.Msg("Found asset: " + releaseAssetName.ToString());
                        foreach (var modInfo in modList)
                        {
                            MelonLogger.Msg("Found modname: " + modInfo.FileInfo.Name);
                            if (string.Equals(releaseAssetName.ToString(), modInfo.FileInfo.Name, StringComparison.OrdinalIgnoreCase))
                            {
                                MelonLogger.Msg("*** MATCHED ASSET TO MOD NAME : " + modInfo.FileInfo.Name);
                                
                                // directly download this file

                                if (!System.IO.Directory.Exists(modsTemporaryDirectory))
                                {
                                    MelonLogger.Msg("Creating temporary directory at: " + modsTemporaryDirectory);
                                    System.IO.Directory.CreateDirectory(modsTemporaryDirectory);
                                }

                                string temporaryFilePath = Path.Combine(modsTemporaryDirectory, modInfo.FileInfo.Name);

                                Methods.DownloadFile(updaterClient, releaseDownloadURL.ToString(), temporaryFilePath);
                                System.Version currentVersion = new System.Version(modInfo.Version);
                                MelonInfoAttribute? tempModAttributes = Methods.GetMelonModAttributes(temporaryFilePath);
                                if (tempModAttributes == null)
                                {
                                    MelonLogger.Warning("Could not find MelonMod attributes for " + temporaryFilePath);
                                    continue;
                                }
                                System.Version temporaryVersion = new System.Version(tempModAttributes.Version);

                                // do we already have the latest version?
                                if (!Methods.IsNewerVersion(currentVersion, temporaryVersion))
                                {
                                    MelonLogger.Msg("Skipping update for " + modInfo.FileInfo.Name + ". Version " + tempModAttributes.Version + " is the latest.");
                                    continue;
                                }

                                MelonLogger.Msg("Updating " + modInfo.FileInfo.Name + " to version " + tempModAttributes.Version + "...");

                                Methods.MakeModBackup(modInfo.FileInfo, currentVersion);
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