using System.Collections.Generic;

namespace TheArqamSchools.Web.Models
{
    public class StatusViewModel
    {
        public bool DatabaseConnected { get; set; }
        public string DatabaseError { get; set; }
        public string ServerVersion { get; set; }
        public int CompatibilityLevel { get; set; }
        public bool RegexSupported { get; set; }
        public bool FullTextInstalled { get; set; }
        public IList<KeyValuePair<string, int>> Counts { get; set; } = new List<KeyValuePair<string, int>>();
    }
}
