﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using AATool.Configuration;
using AATool.Net;

namespace AATool.Saves
{
    public abstract class JsonFolder
    {
        private const string JsonPattern = "*.json";

        public readonly Dictionary<Uuid, JsonStream> Files;

        private DirectoryInfo folder;

        private string Path => this.folder?.FullName ?? string.Empty;

        public JsonFolder()
        {
            this.Files = new ();
        }

        public void SetFolder(DirectoryInfo newFolder)
        {
            if (this.folder?.FullName != newFolder.FullName)
            {
                this.Files.Clear();
                this.folder = newFolder;
            }
        }

        public bool TryUpdate()
        {
            bool modified = false;
            try
            {
                if (!Directory.Exists(this.Path))
                    return false;

                //get all uuid json files in folder
                Dictionary<Uuid, FileInfo> files = this.GetValidFiles();

                modified |= this.TryRemoveDeadFiles(files);
                modified |= this.TryAddNewFiles(files);
            }
            catch { }

            //update jsons
            foreach (JsonStream json in this.Files.Values)
            {
                bool invalidated = Tracker.ObjectivesChanged;
                invalidated |= Config.Tracking.UseDefaultPath.Changed;
                invalidated |= Config.Tracking.CustomSavePath.Changed;
                invalidated |= Config.Tracking.UseSftp.Changed;

                modified |= json.TryUpdate(invalidated);
            }
            return modified;
        }

        private Dictionary<Uuid, FileInfo> GetValidFiles()
        {
            var validFiles = new Dictionary<Uuid, FileInfo>();
            if (!Directory.Exists(this.Path))
                return validFiles;

            try
            {
                //get all json files in folder
                FileInfo[] files = this.folder.GetFiles(JsonPattern, SearchOption.TopDirectoryOnly);
                foreach (FileInfo file in files)
                {
                    string fileName = System.IO.Path.GetFileNameWithoutExtension(file.Name);
                    if (Uuid.TryParse(fileName, out Uuid id))
                    {
                        //filename is uuid
                        validFiles[id] = file;
                        Player.FetchIdentity(id);
                    }
                }
            }
            catch (IOException) { }
            catch (ArgumentException) { }
            catch (SecurityException) { }

            return validFiles;
        }

        private bool TryRemoveDeadFiles(Dictionary<Uuid, FileInfo> newFiles = null)
        {
            bool modified = false;
            foreach (Uuid id in this.Files.Keys.ToArray())
            {
                if (!this.Files.TryGetValue(id, out JsonStream json))
                    continue;

                if (newFiles is null || !newFiles.ContainsKey(id))
                {
                    this.Files.Remove(id);
                    modified = true;
                }
            }
            return modified;
        }

        private bool TryAddNewFiles(Dictionary<Uuid, FileInfo> newFiles)
        {
            bool modified = false;
            foreach (KeyValuePair<Uuid, FileInfo> file in newFiles)
            {
                try
                {
                    string fileName = System.IO.Path.GetFileNameWithoutExtension(file.Value.Name);
                    if (this.Files.ContainsKey(file.Key))
                        continue;

                    var stream = new JsonStream(file.Value.FullName);
                    this.Files[file.Key] = stream;
                    modified = true;
                }
                catch (ArgumentException) { }
            }
            return modified;
        }
    }
}
