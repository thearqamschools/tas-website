using System;
using System.Collections.Generic;

namespace TheArqamSchools.Web.Models
{
    /// <summary>A published campus (dbo.branches) with its city and district names.</summary>
    public class Campus
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Slug { get; set; }
        public string Area { get; set; }
        public string AddressLine { get; set; }
        public string Phone { get; set; }
        public string PhoneAlt { get; set; }
        public string Whatsapp { get; set; }
        public string Email { get; set; }
        public string Timings { get; set; }
        public string Overview { get; set; }
        public string PrincipalName { get; set; }
        public string PrincipalTitle { get; set; }
        public string PrincipalMessage { get; set; }
        public string Status { get; set; }
        public string AdmissionStatus { get; set; }
        public string AdmissionNote { get; set; }
        public string GoogleMapsUrl { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public string SocialLinks { get; set; }
        public Guid? SeoId { get; set; }
        public string CityName { get; set; }
        public string CitySlug { get; set; }
        public string DistrictName { get; set; }
        public string DistrictSlug { get; set; }
        public string CoverBucket { get; set; }
        public string CoverPath { get; set; }
        public string CoverAlt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }

        public string Url { get { return "/campuses/" + CitySlug + "/" + Slug; } }

        /// <summary>"Samanabad, Faisalabad", without repeating the city.</summary>
        public string Locality
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Area) || string.Equals(Area, CityName, StringComparison.OrdinalIgnoreCase))
                    return CityName;
                return Area + ", " + CityName;
            }
        }

        /// <summary>Street address plus the area and city when the address does not already contain them.</summary>
        public string FullAddress
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(AddressLine)) parts.Add(AddressLine.Trim());
                foreach (var p in new[] { Area, CityName })
                {
                    if (string.IsNullOrWhiteSpace(p)) continue;
                    if (parts.Exists(x => x.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0)) continue;
                    parts.Add(p.Trim());
                }
                return string.Join(", ", parts);
            }
        }

        /// <summary>The admin-set map link, or a Google Maps search for the address (no invented coordinates).</summary>
        public string MapUrl
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(GoogleMapsUrl)) return GoogleMapsUrl;
                var a = FullAddress;
                if (string.IsNullOrWhiteSpace(a)) return null;
                return "https://www.google.com/maps/search/?api=1&query=" + Uri.EscapeDataString(a + ", Pakistan");
            }
        }

        public string AdmissionLabel
        {
            get
            {
                switch (AdmissionStatus)
                {
                    case "open": return "Admissions open";
                    case "waiting_list": return "Waiting list";
                    case "opening_soon": return "Admissions opening soon";
                    case "closed": return "Admissions closed";
                    default: return "Contact the campus about admissions";
                }
            }
        }
    }

    public class CampusClass
    {
        public string Name { get; set; }
        public string Stage { get; set; }
        public bool IsAdmissionOpen { get; set; }
        public string Shift { get; set; }
        public string Note { get; set; }
    }

    public class ClassLevel
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Slug { get; set; }
        public string Stage { get; set; }
        public int SortOrder { get; set; }
    }

    /// <summary>A city or district that has at least one published campus (for the filters).</summary>
    public class FilterOption
    {
        public string Slug { get; set; }
        public string Name { get; set; }
        public int Count { get; set; }
    }
}
