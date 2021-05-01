using Octokit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using XSOverlay_VRChat_Parser.Models;

namespace XSOverlay_VRChat_Parser.Helpers
{
    class GitHubUpdater
    {
        private static GitHubClient Client { get; set; }
        private OnReleaseAssetFilter ReleaseAssetFilter { get; set; }
        private OnReleaseTagFilter ReleaseTagFilter { get; set; }
        private OnReleaseTagFloatConverter ReleaseTagFloatConverter { get; set; }
        private Release CachedLatestRelease { get; set; }

        public delegate string OnReleaseAssetFilter(string[] names);
        public delegate string OnReleaseTagFilter(string[] names);
        public delegate float OnReleaseTagFloatConverter(string tag);
        public string ProductHeaderValue { get; private set; }
        public string RepositoryOwner { get; private set; }
        public string RepositoryName { get; private set; }

        public GitHubUpdater(string productHeaderValue,
                             string repositoryOwner,
                             string repositoryName,
                             OnReleaseTagFilter tagFilterFunc,
                             OnReleaseAssetFilter assetFilterFunc,
                             OnReleaseTagFloatConverter tagFloatConverterFunc
                             )
        {
            ProductHeaderValue = productHeaderValue;
            RepositoryOwner = repositoryOwner;
            RepositoryName = repositoryName;
            ReleaseAssetFilter = assetFilterFunc;
            ReleaseTagFilter = tagFilterFunc;
            ReleaseTagFloatConverter = tagFloatConverterFunc;

            if (Client == null)
                Client = new GitHubClient(new ProductHeaderValue(productHeaderValue));
        }

        private async Task<RateLimit> GetRateLimits()
        {
            ApiInfo apiInfo = Client.GetLastApiInfo();
            RateLimit rateLimit = apiInfo?.RateLimit;

            if (rateLimit == null)
                rateLimit = (await Client.Miscellaneous.GetRateLimits()).Rate;

            return rateLimit;
        }

        private async Task<string> GetAndCacheLatestReleaseTag()
        {
            IReadOnlyList<Release> releases;
            string latestReleaseTag = string.Empty;

            RateLimit limits = await GetRateLimits();
            if (!(limits.Remaining > 1))
                throw new Exception($"Failed while attempting to retrieve latest release tag due to rate limits.");

            try
            {
                releases = await Client.Repository.Release.GetAll(RepositoryOwner, RepositoryName);
            }
            catch (Exception ex) { throw new Exception($"Failed while retrieving release metadata: {ex.Message}"); }

            latestReleaseTag = ReleaseTagFilter(releases.Select(x => x.TagName).ToArray());

            if (!string.IsNullOrWhiteSpace(latestReleaseTag))
                CachedLatestRelease = releases.Where(x => x.TagName == latestReleaseTag).FirstOrDefault();

            return latestReleaseTag;
        }

        public async Task<bool> IsUpdateAvailable(float currentVersion, bool useCachedLatestReleaseIfAvailable = false)
        {
            string latestReleaseTag = string.Empty;

            if (!useCachedLatestReleaseIfAvailable || CachedLatestRelease == null)
            {
                RateLimit limits;
                try
                {
                    limits = await GetRateLimits();
                }
                catch (Exception ex) { throw new Exception($"Failed to retrieve rate limits from GitHub: {ex.Message}"); }

                if (!(limits.Remaining > 1))
                    throw new Exception($"Failed while retrieving release metadata due to rate limits.");

                latestReleaseTag = await GetAndCacheLatestReleaseTag();
            }
            else
                latestReleaseTag = CachedLatestRelease.TagName;

            return ReleaseTagFloatConverter(latestReleaseTag) > currentVersion;
        }

        public async Task<string> GetLatestReleaseTag(bool useCachedLatestReleaseIfAvailable = true)
        {
            if (!useCachedLatestReleaseIfAvailable || CachedLatestRelease == null)
                return await GetAndCacheLatestReleaseTag();

            return CachedLatestRelease.TagName;
        }

        public async Task<bool> DownloadLatestRelease(string targetDirectory, bool useCachedLatestReleaseIfAvailable = true)
        {
            if (!useCachedLatestReleaseIfAvailable || CachedLatestRelease == null)
                await GetAndCacheLatestReleaseTag();

            if (!CachedLatestRelease.Assets.Any())
                throw new Exception("Failed because cached release has no assets!");

            string releaseAssetName = ReleaseAssetFilter(CachedLatestRelease.Assets.Select(x => x.Name).ToArray());

            if (string.IsNullOrWhiteSpace(releaseAssetName))
                throw new Exception("Failed because no release asset matched expected pattern!");

            return await DownloadReleaseInternal(targetDirectory, CachedLatestRelease.Assets.Where(x => x.Name == releaseAssetName).FirstOrDefault().BrowserDownloadUrl);
        }

        public async Task<bool> DownloadReleaseByTag(string targetDirectory, string tag)
        {
            IReadOnlyList<Release> releases;
            RateLimit rateLimit = await GetRateLimits();

            if (!(rateLimit.Remaining > 1))
                throw new Exception($"Failed while attempting to retrieve releases due to rate limits.");

            try
            {
                releases = await Client.Repository.Release.GetAll(RepositoryOwner, RepositoryName);
            }
            catch (Exception ex) { throw new Exception($"Failed while retrieving release metadata: {ex.Message}"); }

            if (releases.Count == 0) return false;

            Release thisRelease = releases.Where(x => x.TagName == tag).FirstOrDefault();

            if (thisRelease == null) return false;

            string thisAssetName = ReleaseAssetFilter(thisRelease.Assets.Select(x => x.Name).ToArray());

            if (string.IsNullOrWhiteSpace(thisAssetName))
                throw new Exception("Failed to locate asset in tagged release with filter delegate.");

            return await DownloadReleaseInternal(targetDirectory, thisRelease.Assets.Where(x => x.Name == thisAssetName).FirstOrDefault().BrowserDownloadUrl);
        }

        private async Task<bool> DownloadReleaseInternal(string targetDirectory, string Uri)
        {
            if (!Directory.Exists(targetDirectory))
                throw new Exception("Failed to download release because target directory did not exist.");

            if (string.IsNullOrWhiteSpace(Uri.ToString()))
                throw new Exception("Failed to download release because Uri was null or empty.");

            string uriExtractedFilename = Uri.Substring(Uri.LastIndexOf('/') + 1);

            try
            {
                using (WebClient wc = new WebClient())
                {
                    await wc.DownloadFileTaskAsync(Uri, $"{targetDirectory}\\{uriExtractedFilename}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"An exception occurred while attempting to download a release: {ex.Message}");
            }

            return true;
        }
    }
}
