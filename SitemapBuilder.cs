using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Hosting;
using System.Xml;

namespace Binn.Sitemap
{
    public abstract class SitemapBuilder
    {
        protected string SitemapsFolderPath;
        protected string IndexFolderPath;
        protected string SitemapWebsitePath;
        protected string SitemapName;

        protected string SiteRoot;

        protected void Initialize()
        {
            // Get teh main directory the site is running under.
            IndexFolderPath = HostingEnvironment.MapPath("~");
            // This will be empty if running tests
            if (!string.IsNullOrEmpty(IndexFolderPath))
            {
                // Get directory of executing assembly
                GetMainDirectory();
            }

            SitemapsFolderPath = IndexFolderPath.TrimWith('\\') + "sitemaps\\";
            SitemapName = "sitemap";
        }



        public async Task<bool> Build(string rootUrl, string filename =null, bool indented = false)
        {
            if (!string.IsNullOrEmpty(filename))
            {
                SitemapName = filename;
            }

            // clear existing .xml files
            DeleteExistingFiles(SitemapsFolderPath, SitemapName + "*");

            using (var sitemap = new SitemapWriter(SitemapsFolderPath, SitemapName, indented))
            {
                await WriteItems(sitemap);

                var files = sitemap.SitemapFiles;
                CreateSitemapIndex(rootUrl, SitemapName, files);
            }
            
            //remove compressed files now that they can be rebuilt
            //DeleteExistingFiles(SitemapsFolderPath, "*.gz");

            //compress all files in sitemap folder to gz
            CompressFiles();

            //DeleteExistingFiles(IndexFolderPath);
            //CopyFromStagingToMain();

            return true;
        }

        /// <summary>
        /// Get all items and iterate through them generating urls and appending to the sitemap.
        /// </summary>
        protected abstract Task WriteItems(SitemapWriter sitemap);

        private void GetMainDirectory()
        {
            // get path of assembly
            // convert path from uri
            var path = new Uri(System.IO.Path.GetDirectoryName(
                                System.Reflection.Assembly.GetExecutingAssembly().CodeBase)
                                ).LocalPath;

            // retrieve the parent of the directory
            DirectoryInfo di = new DirectoryInfo(path).Parent;

            // set full folder path
            IndexFolderPath = di.FullName;
        }

        /// <summary>
        /// Creates a sitemap index file for each file specified
        /// </summary>
        public void CreateSitemapIndex(string rootUrl, string name, IEnumerable<string> files)
        {
            // create main sitemap file
            string siteMapPath = string.Format("{0}{1}{2}", SitemapsFolderPath, name.Replace(" ", "") + "Sitemap", SitemapWriter.SitemapFileExtension);
            Stream fs = new FileStream(siteMapPath, FileMode.Create);

            using (XmlTextWriter writer = new XmlTextWriter(fs, Encoding.UTF8))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("sitemapindex");
                writer.WriteAttributeString("xmlns", "http://www.sitemaps.org/schemas/sitemap/0.9");

                // create a link to each compressed sitemap file
                foreach (string file in files)
                {
                    writer.WriteStartElement("sitemap");
                    writer.WriteElementString("loc", rootUrl.TrimWith('/') + "/sitemaps/" + file + SitemapWriter.SitemapFileExtension + ".gz");
                    writer.WriteElementString("lastmod", DateTime.Now.ToString());
                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
                writer.WriteEndDocument();
                writer.Flush();
            }
            fs.Close();
        }

        /// <summary>
        /// Create Sitemap Index file - this should include all the gzip's
        /// </summary>
        public void CreateSitemapIndex()
        {
            Stream fs = new FileStream(string.Format("{0}{1}{2}", SitemapsFolderPath, "Sitemap", SitemapWriter.SitemapFileExtension), FileMode.Create);

            using (XmlTextWriter writer = new XmlTextWriter(fs, Encoding.UTF8))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("urlset");
                writer.WriteAttributeString("xmlns", "http://www.sitemaps.org/schemas/sitemap/0.9");

                DirectoryInfo directorySelected = new DirectoryInfo(SitemapsFolderPath);
                foreach (FileInfo file in directorySelected.GetFiles())
                {
                    if ((File.GetAttributes(file.FullName) & FileAttributes.Hidden) != FileAttributes.Hidden & file.Extension == ".gz")
                    {
                        writer.WriteStartElement("url");
                        writer.WriteElementString("loc", SitemapWebsitePath + "/" + file.Name);
                        writer.WriteElementString("lastmod", file.LastWriteTime.ToString());
                        writer.WriteEndElement();
                    }
                }

                writer.WriteEndElement();
                writer.WriteEndDocument();
                writer.Flush();
            }
            fs.Close();
        }

        /// <summary>
        /// Delete existing .xml and .gz files in the sitemap folder
        /// </summary>
        public void DeleteExistingFiles(string folderPath, string pattern = "*")
        {
            DirectoryInfo directorySelected = new DirectoryInfo(folderPath);
            if (!directorySelected.Exists)
            {
                Directory.CreateDirectory(folderPath);
                return;
            }

            foreach (FileInfo fileToDelete in directorySelected.GetFiles(pattern))
            {
                if ((File.GetAttributes(fileToDelete.FullName) & FileAttributes.Hidden) != FileAttributes.Hidden && fileToDelete.Name.ToLower().Contains("sitemap") && (fileToDelete.Extension == ".gz" || fileToDelete.Extension == ".xml"))
                    fileToDelete.Delete();
            }
        }

        /// <summary>
        /// Compress all the files in the sitemap folder into gz (gzip)
        /// </summary>
        public void CompressFiles()
        {
            DirectoryInfo directorySelected = new DirectoryInfo(SitemapsFolderPath);
            foreach (FileInfo fileToCompress in directorySelected.GetFiles())
            {
                using (FileStream originalFileStream = fileToCompress.OpenRead())
                {
                    if ((File.GetAttributes(fileToCompress.FullName) & FileAttributes.Hidden) != FileAttributes.Hidden & fileToCompress.Extension != ".gz")
                    {
                        using (FileStream compressedFileStream = File.Create(fileToCompress.FullName + ".gz"))
                        {
                            using (GZipStream compressionStream = new GZipStream(compressedFileStream, CompressionMode.Compress))
                            {
                                originalFileStream.CopyTo(compressionStream);
                            }
                        }
                    }
                }
            }
        }

        public void CopyFromStagingToMain()
        {
            DirectoryInfo directorySelected = new DirectoryInfo(SitemapsFolderPath);
            foreach (FileInfo fileToCopy in directorySelected.GetFiles())
            {
                if ((File.GetAttributes(fileToCopy.FullName) & FileAttributes.Hidden) != FileAttributes.Hidden & fileToCopy.Extension == ".gz")
                {
                    File.Copy(fileToCopy.FullName, IndexFolderPath + fileToCopy.Name, true);
                }
                // don't copy regular xml files
                // they'll stay in the /sitemaps/ folder and can be accessed there if necessary
                //else if ((File.GetAttributes(fileToCopy.FullName) & FileAttributes.Hidden) != FileAttributes.Hidden & fileToCopy.Name.ToLower().Contains("sitemap.xml"))
                //{
                //    File.Copy(fileToCopy.FullName, SiteRoot + fileToCopy.Name, true);
                //}
            }
        }
    }
}