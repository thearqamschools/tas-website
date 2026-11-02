using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace TheArqamSchools.Web.Models
{
    /// <summary>A published CMS page (dbo.pages).</summary>
    public class Page
    {
        public Guid Id { get; set; }
        public Guid? ParentId { get; set; }
        public string Title { get; set; }
        public string Slug { get; set; }
        public string Path { get; set; }
        public string Template { get; set; }
        public string Summary { get; set; }
        public string Status { get; set; }
        public Guid? SeoId { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    /// <summary>One block of a page (dbo.page_sections). Data is the section's JSON.</summary>
    public class PageSection
    {
        public Guid Id { get; set; }
        public string Type { get; set; }
        public JObject Data { get; set; }
        public int SortOrder { get; set; }

        public string Text(string key) { return Data.Value<string>(key) ?? ""; }

        /// <summary>id attribute for in-page links (#vision-mission and so on).</summary>
        public string Anchor { get { return Data.Value<string>("anchor"); } }

        /// <summary>Background: white, grey, warm or navy.</summary>
        public string Tone(string fallback) { return Data.Value<string>("tone") ?? fallback; }
    }

    /// <summary>A menu entry (dbo.navigation_items). Children are only shown as a dropdown when there are any.</summary>
    public class NavItem
    {
        public Guid Id { get; set; }
        public Guid? ParentId { get; set; }
        public string Label { get; set; }
        public string Url { get; set; }
        public string Target { get; set; }
        public string Description { get; set; }
        public int SortOrder { get; set; }
        public List<NavItem> Children { get; } = new List<NavItem>();
        public bool HasChildren { get { return Children.Count > 0; } }
        public bool OpensInNewTab { get { return Target == "blank"; } }
    }

    /// <summary>SEO fields for a page (dbo.seo_metadata).</summary>
    public class SeoMetadata
    {
        public Guid Id { get; set; }
        public string MetaTitle { get; set; }
        public string MetaDescription { get; set; }
        public string OgTitle { get; set; }
        public string OgDescription { get; set; }
        public string CanonicalUrl { get; set; }
        public bool RobotsNoindex { get; set; }
        public bool RobotsNofollow { get; set; }
    }

    /// <summary>What the layout needs to write the &lt;head&gt; of a page.</summary>
    public class PageMeta
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string CanonicalPath { get; set; }
        public bool NoIndex { get; set; }
        public string ImageUrl { get; set; }
        public string StructuredDataJson { get; set; }
    }

    public class Breadcrumb
    {
        public Breadcrumb(string label, string url) { Label = label; Url = url; }
        public string Label { get; private set; }
        public string Url { get; private set; }
    }

    /// <summary>A news post or an event, as shown on the public News &amp; Events page and in the admin list.</summary>
    public class NewsItem
    {
        public string Kind { get; set; }
        public Guid Id { get; set; }
        public string Slug { get; set; }
        public string Title { get; set; }
        public string Summary { get; set; }
        public string Body { get; set; }
        public string Category { get; set; }
        public DateTimeOffset Date { get; set; }
        public DateTimeOffset? EndsAt { get; set; }
        public string Venue { get; set; }
        public string CampusName { get; set; }
        public bool IsFeatured { get; set; }
        public bool IsAllDay { get; set; }
        public string Status { get; set; }
        public bool IsEvent { get { return Kind == "event"; } }
        public string Url { get { return "/news-events/" + Slug; } }
    }

    public class NewsListViewModel
    {
        public IList<NewsItem> Items { get; set; }
        public string Filter { get; set; }
        public PageMeta Meta { get; set; }
    }
}
