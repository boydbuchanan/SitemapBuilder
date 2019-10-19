using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Hosting;

namespace Sitemap
{
    public class SitemapHandler : IHttpHandler
    {
        public void ProcessRequest(HttpContext context)
        {
            // update to give custom name
            string siteName = "sitemap";

            string rootDirectory = HostingEnvironment.MapPath("~");
            string folderPath = "\\sitemaps\\";
            string ext = "xml";
            string siteMapFilePath = $"{rootDirectory}{folderPath}{siteName}.{ext}";

            if (File.Exists(siteMapFilePath))
            {
                context.Response.ContentType = "application/xml";

                foreach (var line in File.ReadLines(siteMapFilePath))
                {
                    context.Response.Write(line);
                    context.Response.Write(Environment.NewLine);
                }

            }
            else
            {
                string sitemapUrl = context.Request.Url.Host + $"/sitemaps/{siteName}.{ext}";
                context.Response.Redirect(sitemapUrl);
            }

        }

        /// <summary>
        /// You will need to configure this handler in the Web.config file of your
        /// web and register it with IIS before being able to use it. For more information
        /// see the following link: http://go.microsoft.com/?linkid=8101007
        /// </summary>
        /// <returns>true if the <see cref="T:System.Web.IHttpHandler" /> instance is reusable; otherwise, false.</returns>
        public bool IsReusable
        {
            // Return false in case your Managed Handler cannot be reused for another request.
            // Usually this would be false in case you have some state information preserved per request.
            get { return true; }
        }
    }
}
