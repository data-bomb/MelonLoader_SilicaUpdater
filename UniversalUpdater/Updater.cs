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

[assembly: MelonInfo(typeof(Updater), "Universal Mod Updater", "2.0.4", "databomb", "https://github.com/data-bomb/MelonLoader_UniversalUpdater")]
[assembly: MelonGame(null, null)]

namespace UniversalUpdater
{
    public class Updater : MelonPlugin
    {
        static readonly string modsBackupDirectory = Path.Combine(MelonEnvironment.ModsDirectory, @"backup\");
        static readonly string modsTemporaryDirectory = Path.Combine(MelonEnvironment.ModsDirectory, @"temp\");

        public class ModInfo
        {
            private FileInfo _fileinfo = null!;

            public FileInfo FileInfo
            {
                get => _fileinfo;
                set => _fileinfo = value ?? throw new ArgumentNullException(nameof(value));
            }

            private string _version = null!;

            public string Version
            {
                get => _version;
                set => _version = value ?? throw new ArgumentNullException(nameof(value));
            }
        }

        // there are 4 required parameters and 1 optional parameter (download link URL)
        // https://melonwiki.xyz/#/modders/attributes
        static MelonInfoAttribute? GetMelonModAttributes(String fullModFilePath)
        {
            AssemblyDefinition assemblyDefinition = AssemblyDefinition.ReadAssembly(fullModFilePath);
            CustomAttribute? customAttribute = assemblyDefinition.CustomAttributes.First(k => k.AttributeType.FullName == "MelonLoader.MelonInfoAttribute");
            assemblyDefinition.Dispose();

            if (customAttribute == null)
            {
                MelonLogger.Warning("Could not find a MelonInfo attribute set for mod file: " +  fullModFilePath);
                return null;
            }

            MelonInfoAttribute attributesOriginal = new MelonInfoAttribute(customAttribute.ConstructorArguments[0].Value.GetType(),
                                                                   (String)customAttribute.ConstructorArguments[1].Value,
                                                                   (String)customAttribute.ConstructorArguments[2].Value,
                                                                   (String)customAttribute.ConstructorArguments[3].Value,
                                                                   (String)customAttribute.ConstructorArguments[4].Value);

            return attributesOriginal;
        }

        static bool IsNewerVersion(System.Version existingVersion, System.Version checkVersion)
        {
            if (existingVersion.CompareTo(checkVersion) < 0)
            {
                return true;
            }

            return false;
        }

        static bool ShouldOverwriteBackup(String backupFile, System.Version currentVersion)
        {
            MelonInfoAttribute? modAttributes = GetMelonModAttributes(backupFile);
            if (modAttributes == null)
            {
                return true;
            }

            System.Version version = new System.Version(modAttributes.Version);
            System.Version backupVersion = version;
            if (IsNewerVersion(backupVersion, currentVersion))
            {
                return true;
            }

            return false;
        }

        static String FormatURLString(String downloadLink, String modNamespace, String subPath)
        {
            // check for GitHub and translate to raw URL
            if (downloadLink.StartsWith("https://github.com/"))
            {
                String githubAccount = downloadLink.Split('/')[3];
                String githubRepo = downloadLink.Split('/')[4];
                return $"https://raw.githubusercontent.com/{githubAccount}/{githubRepo}/main/{modNamespace}/{subPath}";
            }
            else
            {
                return $"{downloadLink}/{modNamespace}/{subPath}";
            }
        }

        static void DownloadFile(HttpClient updaterClient, String fileURL, string fullPath)
        {
            MelonLogger.Msg("Trying to access URL: " + fileURL + " to location " + fullPath);

            if (File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
            }

            Stream downloadStream = updaterClient.GetStreamAsync(fileURL).Result;
            FileStream fileStream = new FileStream(fullPath, FileMode.Create);
            downloadStream.CopyTo(fileStream);
            fileStream.Dispose();

            MelonLogger.Msg("Download of " + Path.GetFileName(fullPath) + " complete.");
        }

        static void MakeModBackup(FileInfo theMod, System.Version theVersion)
        {
            if (!System.IO.Directory.Exists(modsBackupDirectory))
            {
                MelonLogger.Msg("Creating backup directory at: " + modsBackupDirectory);
                System.IO.Directory.CreateDirectory(modsBackupDirectory);
            }

            
            String backupFilePath = Path.Combine(modsBackupDirectory, theMod.Name);
            if (System.IO.File.Exists(backupFilePath))
            {
                if (ShouldOverwriteBackup(backupFilePath, theVersion))
                {
                    MelonLogger.Msg("Overwriting existing backup for " + theMod.Name);
                    System.IO.File.Delete(backupFilePath);
                    System.IO.File.Copy(theMod.FullName, backupFilePath, true);
                }
            }
            else
            {
                MelonLogger.Msg("Moving " + theMod.Name + " to backup directory");
                System.IO.File.Move(theMod.FullName, backupFilePath);
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
                    MelonInfoAttribute? modAttributes = GetMelonModAttributes(thisMod.FullName);
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

                                DownloadFile(updaterClient, releaseDownloadURL.ToString(), temporaryFilePath);
                                System.Version currentVersion = new System.Version(modInfo.Version);
                                MelonInfoAttribute? tempModAttributes = GetMelonModAttributes(temporaryFilePath);
                                if (tempModAttributes == null)
                                {
                                    MelonLogger.Warning("Could not find MelonMod attributes for " + temporaryFilePath);
                                    continue;
                                }
                                System.Version temporaryVersion = new System.Version(tempModAttributes.Version);

                                // do we already have the latest version?
                                if (!IsNewerVersion(currentVersion, temporaryVersion))
                                {
                                    MelonLogger.Msg("Skipping update for " + modInfo.FileInfo.Name + ". Version " + tempModAttributes.Version + " is the latest.");
                                    continue;
                                }

                                MelonLogger.Msg("Updating " + modInfo.FileInfo.Name + " to version " + tempModAttributes.Version + "...");

                                MakeModBackup(modInfo.FileInfo, currentVersion);
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
    }
}