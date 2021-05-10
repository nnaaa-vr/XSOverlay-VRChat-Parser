using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
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
                else
                    names = names.Where(x => !x.Contains("_Standlone")).ToArray();

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

        public bool CleanTemp()
        {
            string[] directories = Directory.GetDirectories($@"{ConfigurationModel.ExpandedUserFolderPath}\Temp");
            string[] files = Directory.GetFiles($@"{ConfigurationModel.ExpandedUserFolderPath}\Temp");

            try
            {
                foreach (string dir in directories)
                    Directory.Delete(dir, true);
                foreach (string fn in files)
                    File.Delete(fn);

                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"Encountered an exception while attempting to clean temp directory at {ConfigurationModel.ExpandedUserFolderPath}\\Temp");
                Log.Exception(ex);
            }

            return false;
        }

        public bool DeployUpdater()
        {
            if (!Directory.Exists($@"{ConfigurationModel.ExpandedUserFolderPath}\Temp\UpdaterAssets"))
                throw new FileNotFoundException("Updater assets path could not be found.");

            string updaterDirectory = $@"{ConfigurationModel.ExpandedUserFolderPath}\Updater";

            string[] assetDirectories = Directory.GetDirectories($@"{ConfigurationModel.ExpandedUserFolderPath}\Temp\UpdaterAssets");

            if (!assetDirectories.Any())
                throw new FileNotFoundException("No subdirectories were present inside updater asset directory!");

            string updatedBinariesDirectory = Directory.GetDirectories($@"{ConfigurationModel.ExpandedUserFolderPath}\Temp\UpdaterAssets")[0]; // First child inside path

            try
            {
                if (Directory.Exists($@"{ConfigurationModel.ExpandedUserFolderPath}\Updater"))
                    Directory.Delete($@"{ConfigurationModel.ExpandedUserFolderPath}\Updater", true);
            }
            catch (Exception ex)
            {
                Log.Error($"Encountered an exception while attempting to clear existing updater directory at {ConfigurationModel.ExpandedUserFolderPath}\\Updater");
                Log.Exception(ex);
                return false;
            }

            try
            {
                Directory.Move(updatedBinariesDirectory, updaterDirectory);
            }
            catch (Exception ex)
            {
                Log.Error($"Encountered an exception while attempting to move updater to its new home.");
                Log.Exception(ex);
                return false;
            }

            return true;
        }

        public bool DeployParserRequiresAdmin()
        {
            string currentAssemblyLocation = Assembly.GetExecutingAssembly().Location;
            currentAssemblyLocation = currentAssemblyLocation.Substring(0, currentAssemblyLocation.LastIndexOf('\\'));
            return !IsDirectoryWritable(currentAssemblyLocation);
        }

        public async Task<bool> DownloadLatestUpdater()
        {
            return await UpdaterUpdater.DownloadLatestRelease($@"{ConfigurationModel.ExpandedUserFolderPath}\Temp");
        }

        public async Task<bool> DownloadLatestParser()
        {
            return await ParserUpdater.DownloadLatestRelease($@"{ConfigurationModel.ExpandedUserFolderPath}\Temp");
        }

        public bool StartUpdater()
        {
            string currentAssemblyLocation = Assembly.GetExecutingAssembly().Location;
            currentAssemblyLocation = currentAssemblyLocation.Substring(0, currentAssemblyLocation.LastIndexOf('\\'));
            string updatedBinariesDirectory = string.Empty;
            bool asAdmin = !IsDirectoryWritable(currentAssemblyLocation);

            try
            {
                updatedBinariesDirectory = Directory.GetDirectories($@"{ConfigurationModel.ExpandedUserFolderPath}\Temp\ParserAssets")[0];
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to find directory for updated parser assets at {updatedBinariesDirectory}");
                Log.Exception(ex);
                return false;
            }

            try
            {
                ProcessStartInfo updaterInfo = new ProcessStartInfo()
                {
                    FileName = $@"{ConfigurationModel.ExpandedUserFolderPath}\Updater\XSOverlay VRChat Parser Updater.exe",
                    UseShellExecute = true,
                    RedirectStandardOutput = false,
                    Arguments = $"\"{updatedBinariesDirectory}\" \"{currentAssemblyLocation}\" {Environment.ProcessId}",
                    WorkingDirectory = $@"{ConfigurationModel.ExpandedUserFolderPath}\Updater"
                };

                if (asAdmin)
                    updaterInfo.Verb = "runas";

                Process.Start(updaterInfo);
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to start updater process as {(asAdmin ? "administrator." : "user.")}");
                Log.Exception(ex);
                return false;
            }

            return true;
        }

        public bool UnpackUpdater()
        {
            string lastUpdaterPath = UpdaterUpdater.LastDownloadedReleasePath;

            if (lastUpdaterPath == string.Empty)
                throw new FileNotFoundException("Updater update was not successfully downloaded. No path was found.");

            if (!File.Exists(lastUpdaterPath))
                throw new FileNotFoundException($"File path provided by updater did not exist! Path: {lastUpdaterPath}");

            string updaterAssetPath = $@"{ConfigurationModel.ExpandedUserFolderPath}\Temp\UpdaterAssets";

            if (Directory.Exists(updaterAssetPath))
                throw new Exception($"Updater unpack path already exists! CleanTemp failed or was not called? Path: {updaterAssetPath}");

            Directory.CreateDirectory(updaterAssetPath);

            try
            {
                ZipFile.ExtractToDirectory(lastUpdaterPath, updaterAssetPath);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to extract parser from {lastUpdaterPath} to {updaterAssetPath} with the following exception...");
                Log.Exception(ex);
                return false;
            }
        }

        public bool UnpackParser()
        {
            string lastParserPath = ParserUpdater.LastDownloadedReleasePath;

            if (lastParserPath == string.Empty)
                throw new FileNotFoundException("Parser update was not successfully downloaded. No path was found.");

            if (!File.Exists(lastParserPath))
                throw new FileNotFoundException($"File path provided by updater did not exist! Path: {lastParserPath}");

            string parserAssetPath = $@"{ConfigurationModel.ExpandedUserFolderPath}\Temp\ParserAssets";

            if (Directory.Exists(parserAssetPath))
                throw new Exception($"Parser unpack path already exists! CleanTemp failed or was not called? Path: {parserAssetPath}");

            Directory.CreateDirectory(parserAssetPath);

            try
            {
                ZipFile.ExtractToDirectory(lastParserPath, parserAssetPath);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to extract parser from {lastParserPath} to {parserAssetPath} with the following exception...");
                Log.Exception(ex);
                return false;
            }
        }

        public float GetUpdaterVersion()
        {
            if (!Directory.Exists($@"{ConfigurationModel.ExpandedUserFolderPath}\Temp"))
            {
                if (!IsDirectoryWritable(ConfigurationModel.ExpandedUserFolderPath))
                    throw new UnauthorizedAccessException($"User running process does not have write/delete permissions to {ConfigurationModel.ExpandedUserFolderPath}");

                Directory.CreateDirectory($@"{ConfigurationModel.ExpandedUserFolderPath}\Temp");

                if (!IsDirectoryWritable($@"{ConfigurationModel.ExpandedUserFolderPath}\Temp"))
                    throw new UnauthorizedAccessException($"User running process does not have write/delete permissions to {ConfigurationModel.ExpandedUserFolderPath}\\Temp");
            }

            if (File.Exists($@"{ConfigurationModel.ExpandedUserFolderPath}\Temp\XSOverlay VRChat Parser Updater.exe"))
            {
                string versionString = FileVersionInfo.GetVersionInfo($@"{ConfigurationModel.ExpandedUserFolderPath}\Temp\XSOverlay VRChat Parser Updater.exe").ProductVersion;
                string[] versionTokens = versionString.Split('.');

                return float.Parse($"{versionTokens[0]}.{versionTokens[1]}");
            }

            return 0.0f;
        }

        public bool IsDirectoryWritable(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                    throw new DirectoryNotFoundException($"Failed to find directory at {path}");

                // I was going to use System.Security.AccessControl.DirectorySecurity here and check ACLs, but it's much faster and easier to just, well, try to write something

                File.WriteAllBytes($@"{path}\.writable", new byte[1] { 0x01 });
                File.Delete($@"{path}\.writable");

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
    }
}
