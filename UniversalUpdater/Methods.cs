/*
Universal Mod Updater Plugin
Copyright (C) 2023-2024 by databomb

* Description *
Checks each DLL file in the Mods\ directory for the assemblyInfo 
optional downloadLink URL. If a downloadLink URL is found then it 
will try and check for the files on the GitHub repo's releases and
replace the current mod if the downloaded version is newer.

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

using MelonLoader;
using Mono.Cecil;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;

namespace ModUpdater
{
    public class Methods : Updater
    {
        // there are 4 required parameters and 1 optional parameter (download link URL)
        // https://melonwiki.xyz/#/modders/attributes
        public static MelonInfoAttribute? GetMelonModAttributes(String fullModFilePath)
        {
            AssemblyDefinition assemblyDefinition = AssemblyDefinition.ReadAssembly(fullModFilePath);
            CustomAttribute? customAttribute = assemblyDefinition.CustomAttributes.First(k => k.AttributeType.FullName == "MelonLoader.MelonInfoAttribute");
            assemblyDefinition.Dispose();

            if (customAttribute == null)
            {
                MelonLogger.Warning("Could not find a MelonInfo attribute set for mod file: " + fullModFilePath);
                return null;
            }

            MelonInfoAttribute attributesOriginal = new MelonInfoAttribute(customAttribute.ConstructorArguments[0].Value.GetType(),
                                                                   (String)customAttribute.ConstructorArguments[1].Value,
                                                                   (String)customAttribute.ConstructorArguments[2].Value,
                                                                   (String)customAttribute.ConstructorArguments[3].Value,
                                                                   (String)customAttribute.ConstructorArguments[4].Value);

            return attributesOriginal;
        }

        public static bool IsNewerVersion(string currentVersionText, string temporaryVersionText)
        {
            System.Version currentVersion = new System.Version(currentVersionText);
            System.Version temporaryVersion = new System.Version(temporaryVersionText);

            if (currentVersion.CompareTo(temporaryVersion) < 0)
            {
                return true;
            }

            return false;
        }

        public static bool ShouldOverwriteBackup(String backupFile, string currentVersion)
        {
            MelonInfoAttribute? modAttributes = GetMelonModAttributes(backupFile);
            if (modAttributes == null)
            {
                return true;
            }

            if (IsNewerVersion(modAttributes.Version, currentVersion))
            {
                return true;
            }

            return false;
        }

        public static String FormatURLString(String downloadLink, String modNamespace, String subPath)
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

        public static void ProcessAssetZip(JToken releaseAssetName, JToken releaseDownloadURL, List<ModInfo> modList)
        {
            if (string.Equals(MelonLoader.InternalUtils.UnityInformationHandler.GameName, "Silica"))
            {
                if(MelonUtils.IsGameIl2Cpp() && releaseAssetName.ToString().StartsWith("Listen"))
                {
                    MelonLogger.Msg("Found listen server zip file: " + releaseAssetName.ToString());
                    return;
                }

                if (!MelonUtils.IsGameIl2Cpp() && releaseAssetName.ToString().StartsWith("Dedicated"))
                {
                    MelonLogger.Msg("Found dedicated server zip file: " + releaseAssetName.ToString());
                    return;
                }
            }

            MelonLogger.Msg("Skipping zip file: " + releaseAssetName.ToString());
        }

        public static void ProcessAssetDLL(JToken releaseAssetName, JToken releaseDownloadURL, List<ModInfo> modList)
        {
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

        public static bool IsZipAsset(string assetFile)
        {
            return assetFile.EndsWith(".zip");
        }

        public static bool IsDllAsset(string assetFile)
        {
            return assetFile.EndsWith(".dll");
        }

        public static void DownloadFile(HttpClient updaterClient, String fileURL, string fullPath)
        {
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

        public static void MakeModBackup(FileInfo theMod, string theVersion)
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

        public static void InitializeTemporaryDirectory()
        {
            if (!System.IO.Directory.Exists(modsTemporaryDirectory))
            {
                MelonLogger.Msg("Creating temporary directory at: " + modsTemporaryDirectory);
                System.IO.Directory.CreateDirectory(modsTemporaryDirectory);
            }
            else
            {
                RemoveTemporaryFiles();
            }
        }

        public static void RemoveTemporaryDirectory()
        {
            System.IO.DirectoryInfo tempDirectory = new DirectoryInfo(modsTemporaryDirectory);
            tempDirectory.Delete(true);
            MelonLogger.Msg("Removed all temporary files.");
        }

        public static void RemoveTemporaryFiles()
        {
            System.IO.DirectoryInfo tempDirectory = new DirectoryInfo(modsTemporaryDirectory);
            foreach (System.IO.FileInfo file in tempDirectory.GetFiles())
            {
                file.Delete();
            }
        }

        public static void InitializeWebClient()
        {
            updaterClient = new HttpClient();
            updaterClient.DefaultRequestHeaders.Add("User-Agent", "MelonUpdater");
            updaterClient.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue
            {
                NoCache = true,
                Private = true,
                NoStore = true
            };
        }

        public static FileInfo[] GetModFileList()
        {
            DirectoryInfo modsDirectory = new DirectoryInfo(modsPath);
            return modsDirectory.GetFiles("*.dll");
        }

        public static Dictionary<string, List<ModInfo>> GenerateDownloadList()
        {
            FileInfo[] modFiles = GetModFileList();
            Dictionary<string, List<ModInfo>> modDownloadList = new Dictionary<string, List<ModInfo>>();

            for (int i = 0; i < modFiles.Length; i++)
            {
                FileInfo thisMod = modFiles[i];

                // read assembly info without loading the mod
                MelonInfoAttribute? modAttributes = GetMelonModAttributes(thisMod.FullName);
                if (modAttributes == null)
                {
                    MelonLogger.Error("Could not find MelonMod attributes for " + thisMod.Name);
                    continue;
                }

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

            return modDownloadList;
        }

        public static bool IsGitHubLink(string downloadURL)
        {
            return downloadURL.StartsWith("https://github.com");
        }

        public static string GetGitHubReleaseLink(string downloadURL)
        {
            string repoOwner = downloadURL.Split('/')[3];
            string repoName = downloadURL.Split('/')[4];
            return $"https://api.github.com/repos/{repoOwner}/{repoName}/releases/latest";
        }

        public static JObject GetGitHubReleaseJson(string downloadURL)
        {
            string downloadReleaseURL = Methods.GetGitHubReleaseLink(downloadURL);
            string urlText = updaterClient.GetStringAsync(downloadReleaseURL).Result;
            dynamic jsonText = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(urlText)!;
            return JObject.Parse(urlText);
        }
    }
}