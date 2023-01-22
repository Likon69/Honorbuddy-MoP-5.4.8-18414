using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows.Media;
using Styx.Common;

namespace HighVoltz.AutoAngler
{
    internal static class Updater
    {
        private const string PbSvnUrl = "https://autoangler2.googlecode.com/svn/trunk/";

        private static readonly Regex _linkPattern = new Regex(@"<li><a href="".+"">(?<ln>.+(?:..))</a></li>",
                                                               RegexOptions.CultureInvariant);

        public static void CheckForUpdate()
        {
            try
            {
				AutoAnglerBot.Instance.Log("Checking for new version");
                int remoteRev = GetRevision();
				if (AutoAnglerBot.Instance.MySettings.CurrentRevision != remoteRev)
                {
					AutoAnglerBot.Instance.Log("A new version was found.Downloading Update");
                    DownloadFilesFromSvn(new WebClient(), PbSvnUrl);
					AutoAnglerBot.Instance.Log("Download complete.");
					AutoAnglerBot.Instance.MySettings.CurrentRevision = remoteRev;
					AutoAnglerBot.Instance.MySettings.Save();

                    Logging.Write(Colors.Red, "A new version of AutoAngler was installed. Please restart Honorbuddy");
                }
                else
                {
					AutoAnglerBot.Instance.Log("No updates found");
                }
            }
            catch (Exception ex)
            {
				AutoAnglerBot.Instance.Err(ex.ToString());
            }
        }

        private static int GetRevision()
        {
            var client = new WebClient();
            string html = client.DownloadString(PbSvnUrl);
            var pattern = new Regex(@" - Revision (?<rev>\d+):", RegexOptions.CultureInvariant);
            Match match = pattern.Match(html);
            if (match.Success && match.Groups["rev"].Success)
                return int.Parse(match.Groups["rev"].Value);
            throw new Exception("Unable to retreive revision");
        }

        private static void DownloadFilesFromSvn(WebClient client, string url)
        {
            string html = client.DownloadString(url);
            MatchCollection results = _linkPattern.Matches(html);

            IEnumerable<Match> matches = from match in results.OfType<Match>()
                                         where match.Success && match.Groups["ln"].Success
                                         select match;
            foreach (Match match in matches)
            {
                string file = RemoveXmlEscapes(match.Groups["ln"].Value);
                string newUrl = url + file;
                if (newUrl[newUrl.Length - 1] == '/') // it's a directory...
                {
                    DownloadFilesFromSvn(client, newUrl);
                }
                else // its a file.
                {
                    string filePath, dirPath;
                    if (url.Length > PbSvnUrl.Length)
                    {
                        string relativePath = url.Substring(PbSvnUrl.Length);
						dirPath = Path.Combine(AutoAnglerBot.BotPath, relativePath);
                        filePath = Path.Combine(dirPath, file);
                    }
                    else
                    {
                        dirPath = Environment.CurrentDirectory;
						filePath = Path.Combine(AutoAnglerBot.BotPath, file);
                    }
					AutoAnglerBot.Instance.Log("Downloading {0}", file);
                    if (!Directory.Exists(dirPath))
                        Directory.CreateDirectory(dirPath);
                    client.DownloadFile(newUrl, filePath);
                }
            }
        }

        private static string RemoveXmlEscapes(string xml)
        {
            return
                xml.Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"").Replace(
                    "&apos;", "'");
        }
    }
}