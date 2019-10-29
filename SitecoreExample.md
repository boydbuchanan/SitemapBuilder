
# Usage

Create a service class like the following.

~~~
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.Fields;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Events;
using Sitecore.Publishing;
using Sitecore.Sites;
using Sitecore.Web;

namespace Project.Sitemap
{
    public class SimpleSitecoreSitemapService
    {
        private ISitecoreLinkRepository _linkRepository;
        private ISiteSearch _siteSearch;

        public SimpleSitecoreSitemapService()
        {
            _linkRepository = ContainerForSitecore.Container.Resolve<ISitecoreLinkRepository>();

            _siteSearch = ContainerForSitecore.Container.Resolve<ISiteSearch>();

        }

        /// <summary>
        /// The property name to search for. Expecting a checkbox property
        /// </summary>
        public string SitemapIncludeProperty
        {
            get { return Settings.GetSetting("Project.Sitemap.IncludeInSitemapFieldName", "Show in Site Map"); }
        }

        public void GenerateAllSitemaps()
        {
            List<SiteInfo> allSites = Sitecore.Configuration.Factory.GetSiteInfoList();
            foreach (var siteInfo in allSites.Where(x => !string.IsNullOrWhiteSpace(x.TargetHostName)))
            {
                Log.Debug($"Generating Sitemap For: {siteInfo.StartItem}");
                GenerateSitemapForSite(siteInfo);
            }
        }

        public void GenerateSitemapForSite(SiteInfo siteInfo)
        {
            if (siteInfo == null)
                return;

            // Set the site context for the site we're generating a sitemap for.
            SiteContext targetSiteContext = SiteContext.GetSite(siteInfo.Name);
            using (new Sitecore.Sites.SiteContextSwitcher(targetSiteContext))
            {
                // Set the db to web even if on CM
                // ensures db is set correctly for publish pipeline
                var webDB = Factory.GetDatabase("web");
                using (new Sitecore.Data.DatabaseSwitcher(webDB))
                {
                    string siteName = siteInfo.Name;

                    SitemapBuilder sitemapBuilder = new SitemapBuilder(siteInfo.Scheme, siteInfo.TargetHostName, siteName, true);

                    Log.Debug($"Starting Regenerate Sitemap: {siteName}");

                    Item startItem = webDB.GetItem(siteInfo.RootPath + siteInfo.StartItem);
                    // get all items to include in sitemap
                    var items = _siteSearch.SearchWithFieldMatch(startItem.ID, SitemapIncludeProperty, "1");
                    // map items to locations
                    IEnumerable<Location> locations = GetLocationsForItems(items);

                    sitemapBuilder.Build(locations);
                    Log.Debug($"Built Sitemap {sitemapBuilder.SitemapName}");
                }
            }
        }

        private IEnumerable<Location> GetLocationsForItems(IEnumerable<Item> sitemapItems)
        {
            foreach (var item in sitemapItems)
            {
                yield return new Location()
                {
                    Url = GenerateUrlForItem(item),
                    LastModified = GetLastModifiedDate(item),
                    ChangeFrequency = GetChangeFrequency(item),
                    Priority = GetPriority(item)
                };
            }
        }

        private double? GetPriority(Item item)
        {
            // Not implemented
            return null;
        }

        private Location.eChangeFrequency? GetChangeFrequency(Item item)
        {
            //Not implemented
            return null;
        }

        private DateTime? GetLastModifiedDate(Item item)
        {
            return item.Statistics.Updated;
        }

        private string GenerateUrlForItem(Item item)
        {
            return _linkRepository.GetUrlByItem(item);
        }

        public string GetSiteName(Item siteRoot)
        {
            return Sitecore.Context.GetSiteName().ToLower();
        }

        public string GetSiteTargetHostname(Item siteRoot)
        {
            return Sitecore.Context.Site.TargetHostName.ToLower();
        }


        public bool IncludeInSitemapChecked(Item item)
        {
            CheckboxField includeProp = (CheckboxField)item.Fields[SitemapIncludeProperty];
            if (includeProp == null)
                return false;

            return includeProp.Checked;
        }
    }
}
~~~

Your search class would look something like this. It searches the index for items under the start node that have the property to include in sitemap checked.
~~~
public class SiteSearch : ISiteSearch
{
    public IEnumerable<Item> SearchWithFieldMatch(ID startLocation, string field, string expectation)
    {
          using (var context = Sitecore.ContentSearch.ContentSearchManager.GetIndex("sitecore_web_index").CreateSearchContext())
          {
              var query = PredicateBuilder.True<SearchResultItem>().And(result => 
                  result.Paths.Contains(startLocation));

              query = query.And(result => result[field] == expectation);

              var results = context.GetQueryable<SearchResultItem>().Where(query);

              return results.ToList().Select<SearchResultItem, Item>(x => x.GetItem());
          }
    }
}
~~~

Create a class to hand the publish end and publish end remote events to start regenerating the sitemap.

~~~
using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Publishing;
using Sitecore.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sitecore.Data.Items;

namespace Project.Sitemap.Events
{
    public class OnPublish
    {
        public void PublishEnd(object sender, EventArgs args)
        {
            var sitecoreArgs = args as Sitecore.Events.SitecoreEventArgs;
            if (sitecoreArgs == null)
            {
                return;
            }

            var publisher = sitecoreArgs.Parameters[0] as Publisher;
            if (publisher == null)
            {
                return;
            }
            SimpleSitecoreSitemapService sitemapService = new SimpleSitecoreSitemapService();

            if (publisher.Options.Mode == PublishMode.SingleItem)
            {
                var rootItem = publisher.Options.RootItem;

                var startItem = GetSiteStartItem(rootItem);
                sitemapService.GenerateSitemapForSite(startItem);
                
            }
            else
            {
                sitemapService.GenerateAllSitemaps();
            }
        }

        public void PublishEndRemote(object sender, EventArgs args)
        {
            var sitecoreArgs = args as Sitecore.Data.Events.PublishEndRemoteEventArgs;
            if (sitecoreArgs == null)
                return;

            SimpleSitecoreSitemapService sitemapService = new SimpleSitecoreSitemapService();

            if (sitecoreArgs.Mode == PublishMode.SingleItem)
            {
                Item rootItem = Factory.GetDatabase("web").GetItem(new ID(sitecoreArgs.RootItemId));
                
                sitemapService.GenerateSitemapForSite(GetSiteStartItem(rootItem));
            }
            else
            {
                sitemapService.GenerateAllSitemaps();
            }
        }

        public SiteInfo GetSiteStartItem(Item item)
        {
            // find ancestors to start node
            List<SiteInfo> allSites = Sitecore.Configuration.Factory.GetSiteInfoList();
            var site = allSites.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.TargetHostName) && item.Paths.FullPath.Contains(x.RootPath));
            return site;
        }
    }
}
~~~

Add the following patch config to wire up the publish events.

~~~
<configuration xmlns:patch="http://www.sitecore.net/xmlconfig/">
  <sitecore>
    <settings>
      <!-- The item field to check that the item is to be included in teh sitemap -->
      <setting name="Project.Sitemap.IncludeInSitemapFieldName" value="Show in Site Map" />
    </settings>
    <events>
      <event name="publish:end">
        <handler type="Project.Sitemap.Events.OnPublish, Project.Sitemap" method="PublishEnd"/>
      </event>
      <event name="publish:end:remote">
        <handler type="Project.Sitemap.Events.OnPublish, Project.Sitemap" method="PublishEndRemote"/>
      </event>
    </events>
  </sitecore>
</configuration>
~~~
