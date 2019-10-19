using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using System.Web.Hosting;
using System.Xml;

namespace Sitemap
{
    public class SitemapBuilder
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
        protected string SitemapsFolderName = "sitemaps";

        /// <summary>
        /// The parent directory of the sitemaps directory.
        /// </summary>
        protected string IndexFolderPath { get; set; }

        /// <summary>
        /// The name to give the sitemap.
        /// </summary>
        public string SitemapName { get; set; }

        /// <summary>
        /// Compresses sitemap files to *.gz
        /// </summary>
        public bool GZipSitemap { get; set; }

        /// <summary>
        /// Instantiates a new sitemap builder.
        /// </summary>
        /// <param name="rootUrl">The hostname of the site hosting the sitemaps.</param>
        /// <param name="filename">The name to give the sitemaps files.</param>
        /// <param name="indented">Indicates if the xml should be indented for readability.</param>
        public SitemapBuilder(string rootUrl, string filename = null, bool indented = false, bool compressed = false)
        {
            _indented = indented;
            GZipSitemap = compressed;
            RootUrl = rootUrl;
            SitemapName = !string.IsNullOrEmpty(filename) ? filename : "sitemap";
            Init();
        }

        public SitemapBuilder(string scheme, string hostName, string filename = null, bool indented = false, bool compressed = false) 
            : this(new UriBuilder(scheme, hostName).ToString(), filename, indented, compressed)
        { }

        protected void Init()
        {
            // Get the main directory the site is running under.
            IndexFolderPath = HostingEnvironment.MapPath("~");
            // This will be empty if running tests
            if (string.IsNullOrEmpty(IndexFolderPath))
            {
                // Get directory of executing assembly
                IndexFolderPath = GetMainDirectory();
            }

            SitemapsFolderPath = IndexFolderPath.TrimWith('\\') + SitemapsFolderName.TrimWith('\\');
        }


        public void Build(IEnumerable<Location> locations)
        {

            // clear existing .xml files
            DeleteExistingFiles(SitemapsFolderPath, SitemapName + "*");

            using (var sitemap = new SitemapWriter(SitemapsFolderPath, SitemapName, _indented))
            {
                foreach (Location item in locations)
                {
                    sitemap.AddLocationToSitemap(item);
                }

                var files = sitemap.SitemapFiles;
                CreateSitemapIndex(files);
            }
        }


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
            //compress all files in sitemap folder to gz
            if (GZipSitemap)
            {
                CompressFiles();
            }

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
                        string loc = RootUrl.TrimWith('/') + SitemapsFolderName.TrimWith('/') + file + SitemapWriter.SitemapFileExtension;
                        if (GZipSitemap)
                        {
                            loc += ".gz";
                        }

                        writer.WriteStartElement("sitemap");
                        writer.WriteElementString("loc", loc);
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

    }
}
