using System;
using System.IO;
using System.Web;
using System.Web.Hosting;

namespace Binn.Sitemap
{
    public class SitemapHandler : IHttpHandler
    {
        public void ProcessRequest(HttpContext context)
        {
            // update to give custom name
            string siteapFilePath = string.Format("{0}{1}{2}", HostingEnvironment.MapPath("~"), "\\sitemaps\\sitemap", ".xml");

            if (File.Exists(siteapFilePath))
            {
                context.Response.ContentType = "application/xml";

                foreach (var line in File.ReadLines(siteapFilePath))
                {
                    context.Response.Write(line);
                    context.Response.Write(Environment.NewLine);
                }
                
            }
            else
            {
                string sitemapUrl = context.Request.Url.Host + "/sitemaps/sitemap.xml";
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