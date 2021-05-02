using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XSOverlay_VRChat_Parser.Models;
using static XSOverlay_VRChat_Parser.Helpers.GitHubUpdater;

namespace XSOverlay_VRChat_Parser.Helpers
{
    class PackageUpdateManager
    {
        private bool IsParserStandalone;
        private GitHubUpdater UpdaterUpdater;
        private GitHubUpdater ParserUpdater;

        public PackageUpdateManager()
        {
            // Kind of a hacky way to detect whether or not it's a standalone or framework-dependent build, but it works.
            IsParserStandalone = File.Exists(ConfigurationModel.GetLocalResourcePath("\\createdump.exe"));

            OnReleaseTagFilter releaseTagFilter = delegate (string[] names)
            {
                names = names.Where(x => !x.Contains("prerelease")
                                    && !x.Contains("alpha")
                                    && !x.Contains("beta")
                                    && !x.Contains("rc")
                                    && !x.Contains("-")
                                    ).ToArray();

                for (int i = 0; i < names.Length; i++)
                    names[i] = names[i].Replace("v", "");

                float[] asFloats = Array.ConvertAll<string, float>(names, float.Parse);

                int idxMaxFloat = 0;
                float lastMaxFloat = 0.0f;
                for (int i = 0; i < asFloats.Length; i++)
                    if (asFloats[i] > lastMaxFloat)
                    {
                        lastMaxFloat = asFloats[i];
                        idxMaxFloat = i;
                    }

                return 'v' + names[idxMaxFloat];
            };

            OnReleaseAssetFilter releaseAssetFilter = delegate (string[] names)
            {
                if (IsParserStandalone)
                    names = names.Where(x => x.Contains("_Standalone")).ToArray();

                return names.Where(x => x.Contains(".zip")).FirstOrDefault();
            };

            OnReleaseTagFloatConverter releaseTagFloatConverter = delegate (string tag)
            {
                return float.Parse(tag.Replace("v", ""));
            };

            UpdaterUpdater = new GitHubUpdater("nnaaa-vr-xsoverlay-vrchat-parser-updater", "nnaaa-vr", "XSOverlay-VRChat-Parser-Updater",
                releaseTagFilter, releaseAssetFilter, releaseTagFloatConverter);

            ParserUpdater = new GitHubUpdater("nnaaa-vr-xsoverlay-vrchat-parser", "nnaaa-vr", "XSOverlay-VRChat-Parser",
                releaseTagFilter, releaseAssetFilter, releaseTagFloatConverter);
        }

        public async Task<bool> IsParserUpdateAvailable(float greaterThanVersion)
        {
            return await ParserUpdater.IsUpdateAvailable(greaterThanVersion);
        }

        public async Task<bool> IsUpdaterUpdateAvailable(float greaterThanVersion)
        {
            return await UpdaterUpdater.IsUpdateAvailable(greaterThanVersion);
        }

        //public async Task<bool> DownloadLatestUpdater()
        //{

        //}

        //public async Task<bool> DownloadLatestParser()
        //{

        //}

        //public async Task<bool> UnpackUpdater()
        //{

        //}

        //public async Task<bool> UnpackParser()
        //{

        //}

        //public async Task<bool> WriteUpdaterDirective()
        //{

        //}

        public float GetUpdaterVersion()
        {
            if (!Directory.Exists($@"{ConfigurationModel.ExpandedUserFolderPath}\Temp"))
                Directory.CreateDirectory($@"{ConfigurationModel.ExpandedUserFolderPath}\Temp");

            if (File.Exists($@"{ConfigurationModel.ExpandedUserFolderPath}\Temp\XSOverlay VRChat Parser Updater.exe"))
            {
                string versionString = FileVersionInfo.GetVersionInfo($@"{ConfigurationModel.ExpandedUserFolderPath}\Temp\XSOverlay VRChat Parser Updater.exe").ProductVersion;
                string[] versionTokens = versionString.Split('.');

                return float.Parse($"{versionTokens[0]}.{versionTokens[1]}");
            }

            return 0.0f;
        }
    }
}
