/*
Universal Mod Updater Plugin
Copyright (C) 2023 by databomb

* Description *
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

using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using Newtonsoft.Json;
using MelonLoader.Utils;
using System.Net;
using UnityEngine;
using UniversalUpdater;

[assembly: MelonInfo(typeof(Updater), "Universal Mod Updater", "1.0.0", "databomb")]
[assembly: MelonGame(null, null)]

namespace UniversalUpdater
{
    public class Updater : MelonPlugin
    {

        public class UpdaterEntry
        {
            public String Version
            {
                get;
                set;
            }
            public String UpdateNotes
            {
                get;
                set;
            }
            public bool StoreBackup
            {
                get;
                set;
            }
            public DependencyEntry[]? DependencyEntries
            {
                get;
                set;
            }
        }

        public class DependencyEntry
        {
            public String WebLocation
            {
                get;
                set;
            }
            public String LocalLocation
            {
                get;
                set;
            }
            public bool ForceUpdate
            {
                get;
                set;
            }
        }
        // iterate through mod files
        public override void OnPreInitialization() 
        {
            string modsPath = MelonEnvironment.ModsDirectory;
            DirectoryInfo modsDirectory = new DirectoryInfo(modsPath);
            FileInfo[] modFiles = modsDirectory.GetFiles("*.dll");

            HttpClient updaterClient = new HttpClient();

            for (int i = 0; i < modFiles.Length; i++)
            {
                FileInfo thisMod = modFiles[i];
                String modFilePath = Path.Combine(modsPath, thisMod.Name);
                byte[] byteBuffer = System.IO.File.ReadAllBytes(modFilePath);
                string byteBufferAsString = System.Text.Encoding.UTF8.GetString(byteBuffer);
                Int32 wrapOffset = byteBufferAsString.IndexOf("WrapNonExceptionThrows");
                int typeOffset = 0;

                if (wrapOffset != -1)
                {
                    // align on first 'W'?
                    for (int j = wrapOffset;  j < byteBuffer.Length; j++)
                    {
                        // "W" 
                        if (byteBuffer[j] == 87)
                        {
                            wrapOffset = j;
                            break;
                        }
                    }
                    // add the length of the string plus offset to find start of assembly info
                    typeOffset = wrapOffset + 36;
                    // we're still off by one in many cases so let's try and find another reference in the backwards direction
                    for (int j = typeOffset; j > 0; j--)
                    {
                        if (byteBuffer[j] == 0)
                        {
                            typeOffset = j+1;
                            break;
                        }
                    }

                    // there are 4 required parameters and 1 optional parameter
                    // https://melonwiki.xyz/#/modders/attributes

                    // type (required)
                    int size = byteBuffer[typeOffset];
                    String type = System.Text.Encoding.UTF8.GetString(byteBuffer, typeOffset+1, size);

                    // name (required)
                    int nameOffset = typeOffset + 1 + size;
                    size = byteBuffer[nameOffset];
                    String name = System.Text.Encoding.UTF8.GetString(byteBuffer, nameOffset + 1, size);

                    // version (required)
                    int versionOffset = nameOffset + 1 + size;
                    size = byteBuffer[versionOffset];
                    String version = System.Text.Encoding.UTF8.GetString(byteBuffer, versionOffset + 1, size);

                    // author (required)
                    int authorOffset = versionOffset + 1 + size;
                    size = byteBuffer[authorOffset];
                    String author = System.Text.Encoding.UTF8.GetString(byteBuffer, authorOffset + 1, size);

                    // downloadLink (optional)
                    int downloadOffset = authorOffset + 1 + size;
                    size = byteBuffer[downloadOffset];

                    // check if optional downloadLink was included
                    // 0xFF = NOT included
                    if (size != 255)
                    {
                        String downloadLink = System.Text.Encoding.UTF8.GetString(byteBuffer, downloadOffset + 1, size);
                        MelonLogger.Msg(type + " " + name + " " + version + " " + author + " " + downloadLink);

                        // attempt to see if there is an update

                        // build URL
                        String modURL = type.Split('.')[0];
                        String updaterText = "";

                        if (downloadLink.StartsWith("http"))
                        {
                            String updateURL = "";
                            try
                            {
                                // check for GitHub and translate to raw URL
                                if (downloadLink.StartsWith("https://github.com/"))
                                {
                                    String githubAccount = downloadLink.Split('/')[3];
                                    String githubRepo = downloadLink.Split('/')[4];
                                    updateURL = "https://raw.githubusercontent.com/" + githubAccount + "/" + githubRepo + "/main/" + modURL + "/updater.json";
                                }
                                else
                                {
                                    updateURL = downloadLink + "/" + modURL + "/updater.json";
                                }

                                MelonLogger.Msg(updateURL);

                                updaterText = updaterClient.GetStringAsync(updateURL).Result;
                            }
                            catch (Exception ex)
                            {
                                MelonLogger.Msg("Updater not found for " + thisMod.Name);
                            }

                            MelonLogger.Msg(updaterText);
                            UpdaterEntry thisUpdater = JsonConvert.DeserializeObject<UpdaterEntry>(updaterText);

                            if (thisUpdater == null)
                            {
                                MelonLogger.Msg("Skipping " + thisMod.Name + " due to json object corruption");
                                continue;
                            }

                            // compare the two versions
                            Version thisVersion = new Version(thisUpdater.Version);
                            Version currentVersion = new Version(version);
                            // is the updater version higher
                            if (currentVersion.CompareTo(thisVersion) < 0)
                            {
                                MelonLogger.Msg("Updating " + thisMod.Name + "...");
                                if (thisUpdater.UpdateNotes.Length > 0)
                                {
                                    MelonLogger.Msg(thisMod.Name + " Patch Notes- " + thisUpdater.UpdateNotes);
                                }

                                if (thisUpdater.StoreBackup)
                                {
                                    String backupDirectory = System.IO.Path.Combine(MelonEnvironment.ModsDirectory, @"backup\");
                                    if (!System.IO.Directory.Exists(backupDirectory))
                                    {
                                        MelonLogger.Msg("Creating backup directory at: " + backupDirectory);
                                        System.IO.Directory.CreateDirectory(backupDirectory);
                                    }

                                    MelonLogger.Msg("Moving " + thisMod.Name + " to backup directory");
                                    String backupFilePath = Path.Combine(backupDirectory, thisMod.Name);
                                    System.IO.File.Move(modFilePath, backupFilePath);
                                }

                                // download new and replace
                                String fileURL = updateURL.Remove(updateURL.Length - 13);
                                fileURL = fileURL + "/bin/" + thisMod.Name;
                                MelonLogger.Msg(fileURL);
                                Stream downloadStream = updaterClient.GetStreamAsync(fileURL).Result;
                                FileStream fileStream = new FileStream(modFilePath, FileMode.Create);
                                downloadStream.CopyTo(fileStream);

                                MelonLogger.Msg("Update for " + thisMod.Name + " complete.");

                                // TODO: deal with dependencies
                            }
                        }
                        else
                        {
                            MelonLogger.Msg("Download URL invalid for " + thisMod.Name);
                        }
                    }
                }
            }
        }
    }
}