using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TheArqamSchools.Web.Data;

namespace TheArqamSchools.Web.Helpers
{
    /// <summary>
    /// The homepage's words and pictures (owner, 13 November 2026), kept in the site setting
    /// "home.content" and edited in the admin panel under Home Page. When the setting is missing
    /// (before Database/19 has run) the built-in copy below is used, which is the homepage as it was.
    /// Missing keys also fall back to the built-in copy, so a partly filled setting never breaks the page.
    /// </summary>
    public static class HomeContent
    {
        public const string SettingKey = "home.content";

        public const string DefaultJson = @"{""hero_badge"":""Admissions Open 2026-27"",""hero_line_1"":""Knowledge with values."",""hero_line_2"":""Education with purpose."",""hero_intro"":""Modern education rooted in Islamic values, from Play Group to Matric, with Hifz classes, at 350+ campuses across Pakistan."",""hero_primary_cta"":{""label"":""Apply for Admission"",""href"":""/admissions""},""hero_secondary_cta"":{""label"":""Find a Campus"",""href"":""/campuses""},""slides"":[{""image"":{""bucket"":""public-media"",""path"":""network/content/hero-students-classroom--20261014075037.jpg"",""alt"":""Students learning together in a classroom"",""width"":1958,""height"":803},""mobile_image"":{""bucket"":""public-media"",""path"":""network/content/hero-mobile--20261014075037.jpg"",""alt"":""Students learning together in a classroom"",""width"":660,""height"":803}}],""figures"":[{""value"":""34"",""label"":""Years of Excellence""},{""value"":""350+"",""label"":""Campuses""},{""value"":""250,000+"",""label"":""Students""},{""value"":""20,000+"",""label"":""Hafiz-e-Quran""}],""explore_heading"":""Explore The Arqam Schools"",""tiles"":[{""icon"":""users"",""title"":""About Us"",""text"":""Who we are, our vision and mission, and our values."",""href"":""/about""},{""icon"":""book-open"",""title"":""Academics"",""text"":""Teaching, academic levels and student development."",""href"":""/academics""},{""icon"":""graduation-cap"",""title"":""Admissions"",""text"":""The admission process and an online inquiry form."",""href"":""/admissions""},{""icon"":""map-pin"",""title"":""Campuses"",""text"":""Find a campus by name, city, district or area."",""href"":""/campuses""},{""icon"":""award"",""title"":""News & Events"",""text"":""Achievements, events and announcements."",""href"":""/news-events""},{""icon"":""headset"",""title"":""Contact Us"",""text"":""Call, send a WhatsApp message or write to us."",""href"":""/contact""}],""whole_child_heading"":""Educating the whole child"",""whole_child_lead"":""At The Arqam Schools, modern education and Islamic values go together, to help children grow in knowledge, character, discipline and confidence, from Play Group to Matric."",""whole_child_points"":[{""icon"":""book-open"",""title"":""Academic learning"",""text"":""Modern teaching that builds strong foundations, from Play Group to Matric.""},{""icon"":""moon-star"",""title"":""Islamic education"",""text"":""Islamic values at the heart of school life, with Hifz classes.""},{""icon"":""shield-check"",""title"":""Ethics and discipline"",""text"":""Good manners and discipline as part of everyday learning.""},{""icon"":""heart-handshake"",""title"":""Confidence and character"",""text"":""Supporting each child to grow in confidence and strong character.""}],""whole_child_link"":{""label"":""About The Arqam Schools"",""href"":""/about""},""whole_child_image"":{""bucket"":""public-media"",""path"":""network/content/home-girl-painting.jpg"",""alt"":""A smiling girl painting with watercolours in an art class"",""width"":1200,""height"":805},""news_heading"":""Latest news & events"",""finder_heading"":""Find a campus"",""finder_lead"":""Find a campus by name, city, district or area."",""admissions_heading"":""Admissions Open for 2026-27"",""admissions_body"":""Give your child a strong start with modern education rooted in Islamic values. Send an admission inquiry and the campus team will guide you through the next steps."",""admissions_cta"":{""label"":""Start Your Application"",""href"":""/admissions#inquiry""}}";

        private static readonly JObject Default = JObject.Parse(DefaultJson);

        public static JObject Get(IDictionary<string, JToken> settings)
        {
            var stored = SiteSettingsRepository.Json(settings, SettingKey) as JObject;
            if (stored == null) return (JObject)Default.DeepClone();
            var merged = (JObject)Default.DeepClone();
            foreach (var p in stored.Properties()) merged[p.Name] = p.Value.DeepClone();
            return merged;
        }

        public static string Text(JObject home, string key)
        {
            var t = home[key];
            return t != null && t.Type == JTokenType.String ? (string)t : "";
        }

        public static string Text(JToken obj, string key)
        {
            var t = obj == null ? null : obj[key];
            return t != null && t.Type == JTokenType.String ? (string)t : "";
        }

        public static IList<JObject> List(JObject home, string key)
        {
            var arr = home[key] as JArray;
            return arr == null ? new List<JObject>() : arr.OfType<JObject>().ToList();
        }

        /// <summary>A link from the content, or "#" when it is not a safe link.</summary>
        public static string Href(JToken obj, string key = "href")
        {
            var h = Text(obj, key);
            return Site.IsSafeHref(h) ? h : "#";
        }
    }
}
