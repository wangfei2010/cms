using System.Collections.Generic;
using SS.CMS.Abstractions;

namespace SS.CMS.StlParser.Model
{
    public class DbItemContainer
    {
        private DbItemContainer() { }

        public static void PopChannelItem(PageInfo pageInfo)
        {
            if (pageInfo.ChannelItems.Count > 0)
            {
                pageInfo.ChannelItems.Pop();
            }
        }

        public static void PopContentItem(PageInfo pageInfo)
        {
            if (pageInfo.ContentItems.Count > 0)
            {
                pageInfo.ContentItems.Pop();
            }
        }

        public static void PopSqlItem(PageInfo pageInfo)
        {
            if (pageInfo.SqlItems.Count > 0)
            {
                pageInfo.SqlItems.Pop();
            }
        }

        public static void PopSiteItems(PageInfo pageInfo)
        {
            if (pageInfo.SiteItems.Count > 0)
            {
                pageInfo.SiteItems.Pop();
            }
        }

        public static void PopEachItem(PageInfo pageInfo)
        {
            if (pageInfo.EachItems.Count > 0)
            {
                pageInfo.EachItems.Pop();
            }
        }

        public static DbItemContainer GetItemContainer(PageInfo pageInfo)
        {
            var dbItemContainer = new DbItemContainer();
            if (pageInfo.ChannelItems.Count > 0)
            {
                dbItemContainer.ChannelItem = pageInfo.ChannelItems.Peek();
            }
            if (pageInfo.ContentItems.Count > 0)
            {
                dbItemContainer.ContentItem = pageInfo.ContentItems.Peek();
            }
            if (pageInfo.SqlItems.Count > 0)
            {
                dbItemContainer.SqlItem = pageInfo.SqlItems.Peek();
            }
            if (pageInfo.SiteItems.Count > 0)
            {
                dbItemContainer.SiteItem = pageInfo.SiteItems.Peek();
            }
            if (pageInfo.EachItems.Count > 0)
            {
                dbItemContainer.EachItem = pageInfo.EachItems.Peek();
            }
            return dbItemContainer;
        }

        public KeyValuePair<int, Channel> ChannelItem { get; private set; }

        public KeyValuePair<int, Content> ContentItem { get; private set; }

        public KeyValuePair<int, Dictionary<string, object>> SqlItem { get; private set; }

        public KeyValuePair<int, Site> SiteItem { get; private set; }

        public KeyValuePair<int, object> EachItem { get; private set; }
    }
}
