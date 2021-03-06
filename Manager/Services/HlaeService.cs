﻿using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Core;
using ICSharpCode.SharpZipLib.Zip;
using Manager.Models.GitHub;
using Newtonsoft.Json;

namespace Manager.Services
{
	public class HlaeService
	{
		public const string GITHUB_ENDPOINT = "https://api.github.com/repos/ripieces/advancedfx/releases/latest";

		/// <summary>
		/// Return the path where HLAE is installed.
		/// </summary>
		/// <returns></returns>
		public static string GetHlaePath()
		{
			return Path.Combine(AppSettings.GetLocalAppDataPath(), "hlae");
		}

		/// <summary>
		/// Return the file's path containing the HLAE version number installed.
		/// </summary>
		/// <returns></returns>
		public static string GetHlaeVersionFilePath()
		{
			return GetHlaePath() + Path.DirectorySeparatorChar + "version";
		}

		/// <summary>
		/// Return the version of HLAE installed.
		/// </summary>
		/// <returns></returns>
		public static string GetHlaeVersion()
		{
			string versionFilePath = GetHlaeVersionFilePath();
			if (!File.Exists(versionFilePath)) return null;

			return File.ReadAllText(versionFilePath);
		}

		/// <summary>
		/// Return the HLAE.exe path.
		/// </summary>
		/// <returns></returns>
		public static string GetHlaeExePath()
		{
			return GetHlaePath() + Path.DirectorySeparatorChar + "hlae.exe";
		}

		/// <summary>
		/// Indicates if HLAE is installed.
		/// We test if we have the HLAE exe and a "version" file required to check update.
		/// </summary>
		/// <returns></returns>
		public static bool IsHlaeInstalled()
		{
			return File.Exists(GetHlaeExePath()) && GetHlaeVersion() != null;
		}

		public static async Task<bool> IsUpdateAvailable()
		{
			try
			{
				LatestRelease release = await GetLastReleaseObject();
				string version = release.TagName.Remove(0, 1);

				return new Version(version) > new Version(GetHlaeVersion());
			}
			catch (Exception e)
			{
				Logger.Instance.Log(e);
				return false;
			}
		}

		public static async Task<bool> UpgradeHlae()
		{
			try
			{
				LatestRelease release = await GetLastReleaseObject();
				if (release?.Assets != null && release.Assets.Count > 0)
				{
					string archivePath = AppSettings.GetLocalAppDataPath() + Path.DirectorySeparatorChar + "hlae.zip";
					string downloadUrl = release.Assets[0].BrowserDownloadUrl;
					string version = release.TagName.Remove(0, 1);

					bool downloaded = await Download(downloadUrl, archivePath);
					if (downloaded)
					{
						bool extracted = await ExtractArchive(archivePath);
						if (extracted)
						{
							// create a file containing the version installed to check update later
							File.WriteAllText(GetHlaeVersionFilePath(), version);
							return true;
						}
					}
				}
			}
			catch (Exception e)
			{
				Logger.Instance.Log(e);
				return false;
			}

			return false;
		}

		private static async Task<LatestRelease> GetLastReleaseObject()
		{
			try
			{
				HttpWebRequest request = WebRequest.Create(GITHUB_ENDPOINT) as HttpWebRequest;
				request.Method = "GET";
				request.Proxy = null;
				request.UserAgent = "advancedfx";

				using (WebResponse response = await request.GetResponseAsync())
				{
					using (StreamReader reader = new StreamReader(response.GetResponseStream()))
					{
						string json = reader.ReadToEnd();
						return await Task.Factory.StartNew(() => JsonConvert.DeserializeObject<LatestRelease>(json));
					}
				}
			}
			catch (Exception e)
			{
				Logger.Instance.Log(e);
				return null;
			}
		}

		/// <summary>
		/// Download the last HLAE release.
		/// </summary>
		/// <param name="url"></param>
		/// <param name="archivePath"></param>
		private static async Task<bool> Download(string url, string archivePath)
		{
			using (WebClient webClient = new WebClient())
			{
				try
				{
					Uri uri = new Uri(url);
					await Task.Factory.StartNew(() => webClient.DownloadFile(uri, archivePath));
					return true;
				}
				catch (Exception e)
				{
					Logger.Instance.Log(e);
					return false;
				}
			}
		}

		private static async Task<bool> ExtractArchive(string archivePath)
		{
			try
			{
				string destination = GetHlaePath();
				if (File.Exists(archivePath))
				{
					if (Directory.Exists(destination)) Directory.Delete(destination, true);
					FastZip fast = new FastZip();
					await Task.Factory.StartNew(() => fast.ExtractZip(archivePath, destination, null));
					File.Delete(archivePath);
				}
				return true;

			}
			catch (Exception e)
			{
				Logger.Instance.Log(e);
				return false;
			}
		}
	}
}
