# SitemapBuilder

SitemapBuilder creates multiple sitemap files for use in sites with a very large amount of urls, 
or projects that have multiple sites, or the project wants to organize urls into different sitemaps.

# SitemapWriter

SitemapWriter keeps track of the filesize of the sitemap being generated, when it becomes too large, 
it saves and closes the file and opens a new file. This keeps the size of the index from becoming
too large to be indexed by crawlers.

# Resources

https://support.google.com/webmasters/answer/75712?visit_id=1-636123203431029869-4222802395&rd=1

```
<?xml version="1.0" encoding="UTF-8"?>
   <sitemapindex xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
   <sitemap>
      <loc>http://www.example.com/sitemap1.xml.gz</loc>
      <lastmod>2004-10-01T18:23:17+00:00</lastmod>
   </sitemap>
   <sitemap>
      <loc>http://www.example.com/sitemap2.xml.gz</loc>
      <lastmod>2005-01-01</lastmod>
   </sitemap>
 </sitemapindex>
 ```
