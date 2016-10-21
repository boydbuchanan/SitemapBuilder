using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

namespace BI.Sitemap
{
    public class SitemapWriter : IDisposable
    {
        public const string SitemapFileExtension = ".xml";
        private readonly string _folderPath;
        private readonly string _fileName;
        private Stream fs;
        private XmlTextWriter writer;
        private const int MaxBytes = 500000;

        private int fileCounter = 1;

        private List<string> _sitemapFiles = new List<string>();

        public List<string> SitemapFiles
        {
            get { return _sitemapFiles; }
            set { _sitemapFiles = value; }
        }

        public SitemapWriter(string folderPath, string fileName, bool indented = false)
        {
            _folderPath = folderPath;
            _fileName = fileName;
            
            BeginFile(indented);

        }

        private void BeginFile(bool indented = false)
        {
            string fileName = _fileName + fileCounter;

            string filePath = string.Format("{0}{1}{2}", _folderPath, fileName, SitemapFileExtension);
            fs = new FileStream(filePath, FileMode.Create);
            writer = new XmlTextWriter(fs, Encoding.UTF8);
            if (indented)
            {
                writer.Formatting = Formatting.Indented;
            }
            writer.WriteStartDocument();
            writer.WriteStartElement("urlset");
            writer.WriteAttributeString("xmlns", "http://www.sitemaps.org/schemas/sitemap/0.9");
            SitemapFiles.Add(fileName);
        }

        private void EndFile()
        {
            writer.WriteEndElement();
            writer.WriteEndDocument();
            writer.Flush();
            fs.Close();
        }

        /// <summary>
        /// Appends a location xml to the xml file.
        /// </summary>
        /// <param name="writer">Reference to the xml writer to append to.
        /// <param name="location">Location object to generate xml for.
        /// <returns>none.</returns>
        public void AddLocationToSitemap(Location location)
        {

            if (fs.Length > MaxBytes)
            {
                EndFile();

                fileCounter++;
                BeginFile();
            }

            writer.WriteStartElement("url");
            writer.WriteElementString("loc", location.Url);
            writer.WriteElementString("lastmod", String.Format("{0:yyyy-MM-dd}", location.LastModified));

            if (location.ChangeFrequency.HasValue)
                writer.WriteElementString("changefreq", location.ChangeFrequency.ToString());

            if(location.Priority.HasValue)
                writer.WriteElementString("priority", location.Priority.ToString());

            writer.WriteEndElement();
        }

        public void Dispose()
        {
            EndFile();
        }

        public class Location
        {
            public enum eChangeFrequency
            {
                always,
                hourly,
                daily,
                weekly,
                monthly,
                yearly,
                never
            }

            public string Url { get; set; }
            public eChangeFrequency? ChangeFrequency { get; set; }
            public DateTime? LastModified { get; set; }
            public double? Priority { get; set; }
        }
    }
}