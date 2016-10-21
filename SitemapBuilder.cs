using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using System.Web.Hosting;
using System.Xml;
using Binn.Sitemap;

namespace BI.Sitemap
{
    public abstract class SitemapBuilder
    {
        private readonly bool _indented;

        protected string RootUrl { get; set; }

        /// <summary>
        /// Windows folderpath to the working directory to build sitemaps.
        /// </summary>
        protected string SitemapsFolderPath { get; set; }

        /// <summary>
        /// The name of the directory where the sitemaps will be built.
        /// </summary>
        protected string SitemapsFolderName { get; set; }

        /// <summary>
        /// The parent directory of the sitemaps directory.
        /// </summary>
        protected string IndexFolderPath { get; set; }

        /// <summary>
        /// The name to give the sitemap.
        /// </summary>
        protected string SitemapName { get; set; }

        /// <summary>
        /// Instantiates a new sitemap builder.
        /// </summary>
        /// <param name="rootUrl">The hostname of the site hosting the sitemaps.</param>
        /// <param name="filename">The name to give the sitemaps files.</param>
        /// <param name="indented">Indicates if the xml should be indented for readability.</param>
        protected SitemapBuilder(string rootUrl, string filename = null, bool indented = false)
        {
            _indented = indented;
            RootUrl = rootUrl;
            SitemapName = !string.IsNullOrEmpty(filename) ? filename : "sitemap";
            Init();
        }

        protected void Init()
        {
            // Get teh main directory the site is running under.
            IndexFolderPath = HostingEnvironment.MapPath("~");
            // This will be empty if running tests
            if (string.IsNullOrEmpty(IndexFolderPath))
            {
                // Get directory of executing assembly
                IndexFolderPath = GetMainDirectory();
            }

            SitemapsFolderPath = IndexFolderPath.TrimWith('\\') + SitemapsFolderName.TrimWith('\\');

            Initialize();
        }

        protected virtual void Initialize()
        {
        }

        public async Task<bool> Build()
        {

            // clear existing .xml files
            DeleteExistingFiles(SitemapsFolderPath, SitemapName + "*");

            using (var sitemap = new SitemapWriter(SitemapsFolderPath, SitemapName, _indented))
            {
                await WriteItems(sitemap);

                var files = sitemap.SitemapFiles;
                CreateSitemapIndex(files);
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

        protected virtual string GetMainDirectory()
        {
            // get path of assembly
            // convert path from uri
            var path = new Uri(System.IO.Path.GetDirectoryName(
                                System.Reflection.Assembly.GetExecutingAssembly().CodeBase)
                                ).LocalPath;

            // retrieve the parent of the directory
            DirectoryInfo di = new DirectoryInfo(path).Parent;

            // set full folder path
            return di.FullName;
        }

        /// <summary>
        /// Creates a sitemap index file for each file specified
        /// </summary>
        public void CreateSitemapIndex(IEnumerable<string> files)
        {
            // create main sitemap file
            using (Stream fs = OpenSitemapIndexFile())
            {
                using (XmlTextWriter writer = new XmlTextWriter(fs, Encoding.UTF8))
                {
                    writer.WriteStartDocument();
                    writer.WriteStartElement("sitemapindex");
                    writer.WriteAttributeString("xmlns", "http://www.sitemaps.org/schemas/sitemap/0.9");

                    // create a link to each compressed sitemap file
                    foreach (string file in files)
                    {
                        writer.WriteStartElement("sitemap");
                        writer.WriteElementString("loc",
                            RootUrl.TrimWith('/') + SitemapsFolderName.TrimWith('/') + file +
                            SitemapWriter.SitemapFileExtension + ".gz");
                        writer.WriteElementString("lastmod", DateTime.Now.ToString());
                        writer.WriteEndElement();
                    }

                    writer.WriteEndElement();
                    writer.WriteEndDocument();
                    writer.Flush();
                }
                fs.Close();
            }
        }

        private Stream OpenSitemapIndexFile()
        {
            string siteMapPath = string.Format("{0}{1}{2}", SitemapsFolderPath, SitemapName.Replace(" ", ""),
                SitemapWriter.SitemapFileExtension);
            Stream fs = new FileStream(siteMapPath, FileMode.Create);
            return fs;
        }

        /// <summary>
        /// Create Sitemap Index file - this should include all the gzip's
        /// </summary>
        public void CreateSitemapIndex()
        {
            using (Stream fs = OpenSitemapIndexFile())
            {
                using (XmlTextWriter writer = new XmlTextWriter(fs, Encoding.UTF8))
                {
                    writer.WriteStartDocument();
                    writer.WriteStartElement("urlset");
                    writer.WriteAttributeString("xmlns", "http://www.sitemaps.org/schemas/sitemap/0.9");

                    DirectoryInfo directorySelected = new DirectoryInfo(SitemapsFolderPath);
                    foreach (FileInfo file in directorySelected.GetFiles())
                    {
                        if ((File.GetAttributes(file.FullName) & FileAttributes.Hidden) != FileAttributes.Hidden &
                            file.Extension == ".gz")
                        {
                            writer.WriteStartElement("url");
                            writer.WriteElementString("loc", RootUrl.TrimWith('/') + "sitemaps/" + file.Name);
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