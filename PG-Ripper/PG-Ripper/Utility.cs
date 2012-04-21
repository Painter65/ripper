// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Utility.cs" company="The Watcher">
//   Copyright (c) The Watcher Partial Rights Reserved.
//  This software is licensed under the MIT license. See license.txt for details.
// </copyright>
// <summary>
//   Code Named: PG-Ripper
//   Function  : Extracts Images posted on VB forums and attempts to fetch them to disk.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace PGRipper
{
    #region

    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Security.Cryptography;
    using System.Text;
    using System.Windows.Forms;
    using System.Xml.Serialization;

    using PGRipper.Objects;

    #endregion

    /// <summary>
    /// This page is probably the biggest mess I've ever managed to conceive.
    /// It's so nasty that I dare not even comment much.
    /// But as the file name says, it's just a bunch of non-dependant classes
    /// and funcs for doing nifty little things.
    /// </summary>
    public class Utility
    {
        #region Constants and Fields
        /*
        /// <summary>
        /// The app.
        /// </summary>
        private static readonly Configuration Conf =
            ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

        /// <summary>
        /// The conf.
        /// </summary>
        private static readonly AppSettingsSection App = (AppSettingsSection)Conf.Sections["appSettings"];
        */
        #endregion

        #region Public Methods

        /// <summary>
        /// Check the FilePath for Length because if its more then 260 characters long it will crash
        /// </summary>
        /// <param name="sFilePath">
        /// Folder Path to check
        /// </param>
        /// <returns>
        /// The check path length.
        /// </returns>
        public static string CheckPathLength(string sFilePath)
        {
            if (sFilePath.Length > 260)
            {
                string sShortFilePath = sFilePath.Substring(sFilePath.LastIndexOf("\\", StringComparison.Ordinal) + 1);

                sFilePath = Path.Combine(CacheController.Xform.userSettings.DownloadFolder, sShortFilePath);
            }

            return sFilePath;
        }

        /// <summary>
        /// Encrypts a password using MD5.
        ///   not my code in this func., but falls under public domain.
        ///   Author unknown. But Thanks to the author none the less.
        /// </summary>
        /// <param name="sOriginalPass">
        /// The s Original Pass.
        /// </param>
        /// <returns>
        /// The encode password.
        /// </returns>
        public static string EncodePassword(string sOriginalPass)
        {
            // Instantiate MD5CryptoServiceProvider, get bytes for original password and compute hash (encoded password)
            var md5 = new MD5CryptoServiceProvider();

            var originalBytes = Encoding.Default.GetBytes(sOriginalPass);
            var encodedBytes = md5.ComputeHash(originalBytes);

            // Convert encoded bytes back to a 'readable' string
            return BitConverter.ToString(encodedBytes);
        }

        // public static string sURLImg;

        /// <summary>
        /// Attempts to extract hotlinked and thumb-&gt;FullScale images.
        /// </summary>
        /// <param name="strDump">
        /// The str Dump.
        /// </param>
        /// <returns>
        /// The extract attachment images html.
        /// </returns>
        public static List<ImageInfo> ExtractAttachmentImagesHtml(string strDump)
        {
            List<ImageInfo> rtnList = new List<ImageInfo>();

            strDump = strDump.Replace("&amp;", "&");

            // use only message content
            var iStart = strDump.IndexOf("<!-- attachments -->");

            if (iStart < 0)
            {
                // Return Empty List
                return rtnList;
            }

            iStart += 21;

            var iEnd = strDump.IndexOf("<!-- / attachments -->");

            if (iEnd > 0)
            {
                strDump = strDump.Substring(iStart, iEnd - iStart);
            }

            ///////////////////////////////////////////////
            rtnList.AddRange(
                ItemFinder.ListAllLinks(strDump).Select(
                    link =>
                    new ImageInfo
                        {
                            ImageUrl = CacheController.Xform.userSettings.CurrentForumUrl + ReplaceHexWithAscii(link.Href),
                            ThumbnailUrl = string.Empty
                        }).Where(
                            newPicPoolItem => !IsImageNoneSense(newPicPoolItem.ImageUrl)));

            return rtnList;
        }

        /// <summary>
        /// The extract images html.
        /// </summary>
        /// <param name="strDump">
        /// The str dump.
        /// </param>
        /// <param name="sPostId">
        /// The s post id.
        /// </param>
        /// <returns>
        /// Extracted Images List
        /// </returns>
        public static List<ImageInfo> ExtractImagesHtml(string strDump, string sPostId)
        {
            List<ImageInfo> rtnList;

            if (CacheController.Xform.userSettings.CurrentForumUrl.Contains(@"scanlover.com"))
            {
                rtnList = ExtractAttachmentImagesHtml(strDump);
            }
            else if (CacheController.Xform.userSettings.CurrentForumUrl.Contains(@"sexyandfunny.com"))
            {
                rtnList = ExtractImagesLinksHtml(strDump, null);

                if (rtnList.Count.Equals(0))
                {
                    rtnList = ExtractAttachmentImagesHtml(strDump);
                }
            }
            else if (CacheController.Xform.userSettings.CurrentForumUrl.Contains(@"http://rip") ||
                     CacheController.Xform.userSettings.CurrentForumUrl.Contains(@"http://www.rip") ||
                     CacheController.Xform.userSettings.CurrentForumUrl.Contains(@"kitty-kats.com"))
            {
                rtnList = ExtractImagesLinksHtml(strDump, sPostId);
            }
            else
            {
                rtnList = ExtractImagesLinksHtml(strDump, null);
            }

            return rtnList;
        }

        /// <summary>
        /// Attempts to extract hotlinked and thumb-&gt;FullScale images.
        /// </summary>
        /// <param name="strDump">
        /// The str Dump.
        /// </param>
        /// <param name="sPostId">
        /// The s Post Id.
        /// </param>
        /// <returns>
        /// The extract images links html.
        /// </returns>
        public static List<ImageInfo> ExtractImagesLinksHtml(string strDump, string sPostId)
        {
            if (!string.IsNullOrEmpty(sPostId) && sPostId.StartsWith("http://"))
            {
                sPostId = sPostId.Substring(sPostId.IndexOf("#post") + 5);
            }

            strDump = strDump.Replace("&amp;", "&");

            // use only message content
            var sMessageStart = "<!-- message -->";
            var sMessageEnd = "<!-- / message -->";

            // If Forum uses VB 4.x or higher
            if (CacheController.Xform.userSettings.CurrentForumUrl.Contains(@"http://rip-") ||
                CacheController.Xform.userSettings.CurrentForumUrl.Contains(@"http://www.rip-") ||
                CacheController.Xform.userSettings.CurrentForumUrl.Contains(@"kitty-kats.com"))
            {
                sMessageStart = string.Format("<div id=\"post_message_{0}\">", sPostId);
                sMessageEnd = "</blockquote>";
            }

            var iStart = strDump.IndexOf(sMessageStart);

            iStart += sMessageStart.Length;

            var iEnd = strDump.IndexOf(sMessageEnd, iStart);

            strDump = strDump.Substring(iStart, iEnd - iStart);

            ///////////////////////////////////////////////

            // Parse all Links <a>
            var rtnList =
                ItemFinder.ListAllLinks(strDump).Select(
                    link =>
                    new ImageInfo
                        {
                            ImageUrl = RemoveRedirectLink(ReplaceHexWithAscii(link.Href)), 
                            ThumbnailUrl = string.Empty
                        }).Where(newPicPoolItem => !IsImageNoneSense(newPicPoolItem.ImageUrl)).ToList();

            // Parse all Image <a>
            rtnList.AddRange(
                ItemFinder.ListAllImages(strDump).Select(
                    link =>
                    new ImageInfo
                        { 
                            ImageUrl = RemoveRedirectLink(ReplaceHexWithAscii(link.Href)), 
                            ThumbnailUrl = string.Empty 
                        }));

            return rtnList;
        }

        /// <summary>
        /// Removes the redirect link.
        /// </summary>
        /// <param name="linkToCheck">
        /// The link to check.
        /// </param>
        /// <returns>
        /// The remove redirect link.
        /// </returns>
        public static string RemoveRedirectLink(string linkToCheck)
        {
            if (linkToCheck.Contains(@"http://rip-productions.net/redirect-to/?redirect="))
            {
                linkToCheck = linkToCheck.Replace(@"http://rip-productions.net/redirect-to/?redirect=", string.Empty).Replace("%3A", ":").Replace("%2F", "/");
            }

            // Remove anonym.to from Link if exists
            if (linkToCheck.Contains("www.anonym.to"))
            {
                linkToCheck = linkToCheck.Replace(@"http://www.anonym.to/?", string.Empty);
            }

            // Remove anonym.to from Link if exists
            if (linkToCheck.Contains("anonym.to"))
            {
                linkToCheck = linkToCheck.Replace(@"http://anonym.to/?", string.Empty);
            }

            return linkToCheck;
        }

        /// <summary>
        /// TODO : Change to regex
        /// Extracts links leading to other threads and postsfor indicies crawling.
        /// </summary>
        /// <param name="strDump">
        /// The str Dump.
        /// </param>
        /// <param name="sUrl">
        /// The s Url.
        /// </param>
        /// <returns>
        /// The extract index urls html.
        /// </returns>
        public static List<ImageInfo> ExtractIndexUrlsHtml(string strDump, string sUrl)
        {
            List<ImageInfo> rtnList = new List<ImageInfo>();

            const string StartHref = "<a ";
            const string Href = "href=\"";
            const string EndHref = "</a>";

            // use only message content
            if (!string.IsNullOrEmpty(sUrl) && sUrl.StartsWith("http://") && sUrl.Contains("#post"))
            {
                sUrl = sUrl.Substring(sUrl.IndexOf("#post") + 5);

                string sMessageStart = string.Format("<div id=\"post_message_{0}\">", sUrl);
                const string MessageEnd = "</blockquote>";

                int iStart = strDump.IndexOf(sMessageStart);

                iStart += sMessageStart.Length;

                int iEnd = strDump.IndexOf(MessageEnd, iStart);

                strDump = strDump.Substring(iStart, iEnd - iStart);
            }

            string sCopy = strDump;

            ///////////////////////////////////////////////
            int iStartHref = sCopy.IndexOf(StartHref);

            if (iStartHref >= 0)
            {
                //////////////////////////////////////////////////////////////////////////

                while (iStartHref >= 0)
                {
                    // Thread.Sleep(1);
                    int iHref = sCopy.IndexOf(Href, iStartHref);

                    if (!(iHref >= 0))
                    {
                        iStartHref = sCopy.IndexOf(StartHref, iStartHref + EndHref.Length);
                        continue;
                    }

                    int iEndHref = sCopy.IndexOf(EndHref, iHref);

                    if (iEndHref >= 0)
                    {
                        string substring = sCopy.Substring(iHref + Href.Length, iEndHref - (iHref + Href.Length));
                        sCopy = sCopy.Remove(iStartHref, iEndHref + EndHref.Length - iStartHref);

                        iStartHref = substring.IndexOf("\" target=\"_blank\">");

                        if (iStartHref >= 0)
                        {
                            ImageInfo imgInfoIndexLink = new ImageInfo
                                { ThumbnailUrl = string.Empty, ImageUrl = substring.Substring(0, iStartHref) };

                            if (imgInfoIndexLink.ImageUrl.Contains(@"showthread.php") ||
                                imgInfoIndexLink.ImageUrl.Contains(@"showpost.php"))
                            {
                                if (imgInfoIndexLink.ImageUrl.Contains("&amp;"))
                                {
                                    imgInfoIndexLink.ImageUrl =
                                        imgInfoIndexLink.ImageUrl.Remove(imgInfoIndexLink.ImageUrl.IndexOf("&amp;"));
                                }

                                rtnList.Add(imgInfoIndexLink);
                            }
                        }
                    }

                    iStartHref = 0;
                    iStartHref = sCopy.IndexOf(StartHref, iStartHref);
                }

                //////////////////////////////////////////////////////////////////////////
            }

            return rtnList;
        }

        /// <summary>
        /// TODO : Change to Regex
        /// Get Post ids of all Posts
        /// </summary>
        /// <param name="strDump">
        /// The str Dump.
        /// </param>
        /// <returns>
        /// The extract threadto posts html.
        /// </returns>
        public static List<ImageInfo> ExtractThreadtoPostsHtml(string strDump)
        {
            List<ImageInfo> rtnList = new List<ImageInfo>();

            const string Start = "<a name=\"post";

            string sEnd = "\">";

            if (CacheController.Xform.userSettings.CurrentForumUrl.Contains(@"http://rip-") ||
                CacheController.Xform.userSettings.CurrentForumUrl.Contains(@"http://www.rip-") ||
                CacheController.Xform.userSettings.CurrentForumUrl.Contains(@"kitty-kats.com"))
            {
                sEnd = "\" href";
            }

            string sCopy = strDump;

            int iStart = 0;

            iStart = sCopy.IndexOf(Start, iStart);

            while (iStart >= 0)
            {
                int iEnd = sCopy.IndexOf(sEnd, iStart);

                string sPostId = sCopy.Substring(iStart + Start.Length, iEnd - (iStart + Start.Length));

                ImageInfo newThumbPicPool = new ImageInfo { ImageUrl = sPostId };

                // iEnd = 0;
                if (IsNumeric(sPostId) && !string.IsNullOrEmpty(sPostId))
                {
                    rtnList.Add(newThumbPicPool);
                }

                iStart = sCopy.IndexOf(Start, iStart + sEnd.Length);
            }

            return rtnList;
        }

        /// <summary>
        /// This func checks to see if a file already exists at destination
        ///   thats of the same name. If so, it incrementally adds numerical
        ///   values prior to the image extension until the new file path doesn't
        ///   already have a file there.
        /// </summary>
        /// <param name="sPath">
        /// Image path
        /// </param>
        /// <returns>
        /// The get suitable name.
        /// </returns>
        public static string GetSuitableName(string sPath)
        {
            string newAlteredPath = sPath;
            int iRenameCnt = 1;
            string sbegining = newAlteredPath.Substring(0, newAlteredPath.LastIndexOf(".", StringComparison.Ordinal));
            string sEnd = newAlteredPath.Substring(newAlteredPath.LastIndexOf(".", StringComparison.Ordinal));

            while (File.Exists(newAlteredPath))
            {
                newAlteredPath = string.Format("{0}_{1}{2}", sbegining, iRenameCnt, sEnd);
                iRenameCnt++;
            }

            return newAlteredPath;
        }

        /// <summary>
        /// Check if Input is a Numeric Value (Numbers)
        /// </summary>
        /// <param name="valueToCheck">
        /// The value To Check.
        /// </param>
        /// <returns>
        /// The is numeric.
        /// </returns>
        public static bool IsNumeric(object valueToCheck)
        {
            double dummy;
            string inputValue = Convert.ToString(valueToCheck);

            bool numeric = double.TryParse(inputValue, NumberStyles.Any, null, out dummy);

            return numeric;
        }

        /// <summary>
        /// It's essential to give files legal names. Otherwise the Win32API 
        ///   sends back a bucket full of cow dung.
        /// </summary>
        /// <param name="sString">
        /// String to check
        /// </param>
        /// <returns>
        /// The remove illegal charecters.
        /// </returns>
        public static string RemoveIllegalCharecters(string sString)
        {
            string sNewComposed = sString;

            sNewComposed = sNewComposed.Replace("\\", string.Empty);
            sNewComposed = sNewComposed.Replace("/", "-");
            sNewComposed = sNewComposed.Replace("*", "+");
            sNewComposed = sNewComposed.Replace("?", string.Empty);
            sNewComposed = sNewComposed.Replace("!", string.Empty);
            sNewComposed = sNewComposed.Replace("\"", "'");
            sNewComposed = sNewComposed.Replace("<", "(");
            sNewComposed = sNewComposed.Replace(">", ")");
            sNewComposed = sNewComposed.Replace("|", "!");
            sNewComposed = sNewComposed.Replace(":", ";");
            sNewComposed = sNewComposed.Replace("&amp;", "&");
            sNewComposed = sNewComposed.Replace("&quot;", "''");
            sNewComposed = sNewComposed.Replace("&apos;", "'");
            sNewComposed = sNewComposed.Replace("&lt;", string.Empty);
            sNewComposed = sNewComposed.Replace("&gt;", string.Empty);
            sNewComposed = sNewComposed.Replace("�", "e");
            sNewComposed = sNewComposed.Replace("\t", string.Empty);

            // sNewComposed = newComposed.Replace("@", "-");
            sNewComposed = sNewComposed.Replace("\r", string.Empty);
            sNewComposed = sNewComposed.Replace("\n", string.Empty);

            return sNewComposed;
        }

        /// <summary>
        /// Although these are not hex, but rather html codes for special characters
        /// </summary>
        /// <param name="sURL">
        /// String to check
        /// </param>
        /// <returns>
        /// The replace hex with ascii.
        /// </returns>
        public static string ReplaceHexWithAscii(string sURL)
        {
            string sString = sURL;

            if (sString == null)
            {
                return string.Empty;
            }

            sString = sString.Replace("&amp;", "&");
            sString = sString.Replace("&quot;", "''");
            sString = sString.Replace("&lt;", string.Empty);
            sString = sString.Replace("&gt;", string.Empty);
            sString = sString.Replace("�", "e");
            sString = sString.Replace("\t", string.Empty);

            // sString = sString.Replace("@", "-");
            return sString;
        }

        /// <summary>
        /// Save all Jobs, and the current one which causes the crash to a CrashLog_...txt
        /// </summary>
        /// <param name="sExMessage">
        /// Exception Message
        /// </param>
        /// <param name="sStackTrace">
        /// Exception Stack Trace
        /// </param>
        /// <param name="mCurrentJob">
        /// Current Download Job
        /// </param>
        public static void SaveOnCrash(string sExMessage, string sStackTrace, JobInfo mCurrentJob)
        {
            const string ErrMessage =
                "An application error occurred. Please contact Admin (http://ripper.watchersnet.de/Feedback.aspx) " +
                "with the following information:";

            var currentDateTime =
                DateTime.Now.ToString().Replace("/", string.Empty).Replace(":", string.Empty).Replace(".", string.Empty)
                    .Replace(" ", "_");

            // Save Current Job and the Error to txt file
            string sFile = string.Format("Crash_{0}.txt", currentDateTime);

            // Save Current Job and the Error to txt file
            FileStream file = new FileStream(Path.Combine(Application.StartupPath, sFile), FileMode.CreateNew);
            StreamWriter sw = new StreamWriter(file);
            sw.WriteLine(ErrMessage);
            sw.Write(sw.NewLine);
            sw.Write(sExMessage);
            sw.Write(sw.NewLine);
            sw.Write(sw.NewLine);
            sw.WriteLine("Stack Trace:");
            sw.Write(sw.NewLine);
            sw.Write(sStackTrace);
            sw.Write(sw.NewLine);
            sw.Write(sw.NewLine);

            if (mCurrentJob != null)
            {
                sw.WriteLine("Current Job DUMP:");
                sw.Write(sw.NewLine);

                sw.WriteLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
                sw.WriteLine(
                    "<ArrayOfJobInfo xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\">");
                sw.WriteLine("  <JobInfo>");
                sw.WriteLine("    <sStorePath>{0}</sStorePath>", mCurrentJob.StorePath);
                sw.WriteLine("    <sTitle>{0}</sTitle>", mCurrentJob.Title);
                sw.WriteLine("    <sPostTitle>{0}</sPostTitle>", mCurrentJob.PostTitle);
                sw.WriteLine("    <sForumTitle>{0}</sForumTitle>", mCurrentJob.ForumTitle);
                sw.WriteLine("    <sURL>{0}</sURL>", mCurrentJob.URL);
                sw.WriteLine("    <sXMLPayLoad>{0}</sXMLPayLoad>", mCurrentJob.HtmlPayLoad);
                sw.WriteLine("    <sImageCount>{0}</sImageCount>", mCurrentJob.ImageCount);
                sw.WriteLine("  </JobInfo>");
                sw.WriteLine("</ArrayOfJobInfo>");
            }

            sw.Close();
            file.Close();
        }

        #endregion

        #region Config Settings

        /// <summary>
        /// Loads All the  settings.
        /// </summary>
        /// <returns>The Settings Class</returns>
        public static SettingBase LoadSettings()
        {
            SettingBase settings = new SettingBase();

            if (File.Exists(Path.Combine(Application.StartupPath, "Settings.xml")))
            {
                var serializer = new XmlSerializer(typeof(SettingBase));
                var textreader = new StreamReader(Path.Combine(Application.StartupPath, "Settings.xml"));

                settings = (SettingBase)serializer.Deserialize(textreader);
                textreader.Close();
            }
            else
            {
                var configFullPath = Path.Combine(
                    Application.StartupPath,
                    string.Format("{0}.Config", Assembly.GetExecutingAssembly().ManifestModule.Name));

                // Check if Legacy Settings Exists
                if (File.Exists(configFullPath))
                {
                    // Default Settings
                    try
                    {
                        settings.ClipBWatch = Convert.ToBoolean(LoadOldSetting("clipBoardWatch"));
                        settings.ShowPopUps = Convert.ToBoolean(LoadOldSetting("Show Popups"));
                        settings.SubDirs = Convert.ToBoolean(LoadOldSetting("SubDirs"));
                        settings.AutoThank = Convert.ToBoolean(LoadOldSetting("Auto TK Button"));
                        settings.DownInSepFolder = Convert.ToBoolean(LoadOldSetting("DownInSepFolder"));
                        settings.SavePids = Convert.ToBoolean(LoadOldSetting("SaveRippedPosts"));
                        settings.ShowCompletePopUp = Convert.ToBoolean(LoadOldSetting("Show Downloads Complete PopUp"));
                        settings.MinImageCount = Convert.ToInt32(LoadOldSetting("minImageCountThanks"));
                        settings.ThreadLimit = Convert.ToInt32(LoadOldSetting("Thread Limit"));
                        settings.DownloadFolder = LoadOldSetting("Download Folder");
                        settings.DownloadOptions = LoadOldSetting("Download Options");
                        settings.TopMost = Convert.ToBoolean(LoadOldSetting("Always OnTop"));
                        settings.Language = LoadOldSetting("UserLanguage");
                        settings.CurrentForumUrl = LoadOldSetting("forumURL");
                        settings.CurrentUserName = LoadOldSetting("User");
                        settings.WindowWidth = Convert.ToInt32(LoadOldSetting("Window width"));
                        settings.WindowHeight = Convert.ToInt32(LoadOldSetting("Window height"));
                        settings.WindowTop = Convert.ToInt32(LoadOldSetting("Window top"));
                        settings.WindowLeft = Convert.ToInt32(LoadOldSetting("Window left"));
                        settings.ForumsAccount = new List<ForumAccount>();

                        var oldForumAccount = new ForumAccount
                            {
                                ForumURL = LoadOldSetting("forumURL"),
                                UserName = LoadOldSetting("User"),
                                UserPassWord = LoadOldSetting("Password")
                            };

                        settings.ForumsAccount.Add(oldForumAccount);
                    }
                    catch (Exception)
                    {
                        // Default Settings
                        settings.ClipBWatch = true;
                        settings.ShowPopUps = true;
                        settings.SubDirs = true;
                        settings.AutoThank = false;
                        settings.DownInSepFolder = true;
                        settings.SavePids = true;
                        settings.ShowCompletePopUp = true;
                        settings.MinImageCount = 3;
                        settings.ThreadLimit = 3;
                        settings.DownloadFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                        settings.DownloadOptions = "0";
                        settings.TopMost = false;
                        settings.Language = "en-EN";
                        settings.CurrentForumUrl = "http://www.kitty-kats.com/";
                        settings.WindowWidth = 863;
                        settings.WindowHeight = 611;
                        settings.ForumsAccount = new List<ForumAccount>();
                    }

                    File.Delete(configFullPath);
                }
                else
                {
                    // Default Settings
                    settings.ClipBWatch = true;
                    settings.ShowPopUps = true;
                    settings.SubDirs = true;
                    settings.AutoThank = false;
                    settings.DownInSepFolder = true;
                    settings.SavePids = true;
                    settings.ShowCompletePopUp = true;
                    settings.MinImageCount = 3;
                    settings.ThreadLimit = 3;
                    settings.DownloadFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                    settings.DownloadOptions = "0";
                    settings.TopMost = false;
                    settings.Language = "en-EN";
                    settings.CurrentForumUrl = "http://www.kitty-kats.com/";
                    settings.WindowWidth = 863;
                    settings.WindowHeight = 611;
                    settings.ForumsAccount = new List<ForumAccount>();
                }
            }

            return settings;
        }

        /// <summary>
        /// Saves the settings.
        /// </summary>
        /// <param name="currentSettings">The current settings.</param>
        public static void SaveSettings(SettingBase currentSettings)
        {
            var serializer = new XmlSerializer(typeof(SettingBase));
            var textreader = new StreamWriter(Path.Combine(Application.StartupPath, "Settings.xml"));

            serializer.Serialize(textreader, currentSettings);
            textreader.Close();
        }

        private static readonly Configuration Conf =
            ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

        private static readonly AppSettingsSection App = (AppSettingsSection)Conf.Sections["appSettings"];

        /// <summary>
        /// Loads a Setting from the old App.config
        /// </summary>
        /// <param name="sKey">
        /// Setting name
        /// </param>
        /// <returns>
        /// Setting value
        /// </returns>
        public static string LoadOldSetting(string sKey)
        {
            string setting = App.Settings[sKey].Value;

            return setting;
        }

        /*
        /// <summary>
        /// Saves a setting to the App.config
        /// </summary>
        /// <param name="sKey">
        /// Setting Name
        /// </param>
        /// <param name="sValue">
        /// Setting Value
        /// </param>
        public static void SaveSetting(string sKey, string sValue)
        {
            if (App.Settings[sKey] != null)
            {
                App.Settings.Remove(sKey);
            }

            App.Settings.Add(sKey, sValue);


            App.SectionInformation.ForceSave = true;
            Conf.Save(ConfigurationSaveMode.Modified);

            ConfigurationManager.RefreshSection("appSettings");
        }

        /// <summary>
        /// Delete a Setting
        /// </summary>
        /// <param name="sKey">
        /// Setting Name
        /// </param>
        public static void DeleteSetting(string sKey)
        {
            if (App.Settings[sKey] != null)
            {
                App.Settings.Remove(sKey);
            }

            App.SectionInformation.ForceSave = true;
            Conf.Save(ConfigurationSaveMode.Modified);

            ConfigurationManager.RefreshSection("appSettings");
        }*/

        #endregion

        /// <summary>
        /// This function allows or disallows the inclusion of an image for fetching.
        ///   returning true DISALLOWS the image from inclusion...
        /// </summary>
        /// <param name="szImgPth">
        /// The sz Img Pth.
        /// </param>
        /// <returns>
        /// The is image none sense.
        /// </returns>
        private static bool IsImageNoneSense(string szImgPth)
        {
            return szImgPth.Contains(@"smilies") ||
                   (szImgPth.Contains(@"Smilies") ||
                    (szImgPth.Contains(@"emoticons") ||
                     (szImgPth.Contains(CacheController.Xform.userSettings.CurrentForumUrl) && szImgPth.Contains("images/misc") ||
                      szImgPth.Contains(@"applied/buttons"))));
        }
    }
}