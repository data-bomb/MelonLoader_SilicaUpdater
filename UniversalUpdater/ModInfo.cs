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

using System;
using System.IO;

namespace UniversalUpdater
{
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
}