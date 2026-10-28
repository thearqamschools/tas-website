using System;

namespace TheArqamSchools.Web.Models
{
    /// <summary>A head office shown in the website footer (table dbo.head_offices).</summary>
    public class HeadOffice
    {
        public Guid Id { get; set; }
        public int OfficeNumber { get; set; }
        public string Name { get; set; }
        public string City { get; set; }
        public string Address { get; set; }
        public string Phone { get; set; }
        public string PhoneAlt { get; set; }
        public string Whatsapp { get; set; }
        public string Email { get; set; }
        public string GoogleMapsUrl { get; set; }
        public string Hours { get; set; }
        public bool IsPublished { get; set; }
    }
}
