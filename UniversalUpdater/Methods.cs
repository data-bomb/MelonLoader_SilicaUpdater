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
using System;
using System.IO;
using System.Linq;
using System.Net.Http;

namespace UniversalUpdater
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

        public static bool IsNewerVersion(System.Version existingVersion, System.Version checkVersion)
        {
            if (existingVersion.CompareTo(checkVersion) < 0)
            {
                return true;
            }

            return false;
        }

        public static bool ShouldOverwriteBackup(String backupFile, System.Version currentVersion)
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

        public static void DownloadFile(HttpClient updaterClient, String fileURL, string fullPath)
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

        public static void MakeModBackup(FileInfo theMod, System.Version theVersion)
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
    }
}