using System;

namespace Sitemap
{
    public class Location
    {
        public enum eChangeFrequency
        {
            always,
            hourly,
            daily,
            weekly,
            monthly,
            yearly,
            never
        }

        public string Url { get; set; }
        public eChangeFrequency? ChangeFrequency { get; set; }
        public DateTime? LastModified { get; set; }
        public double? Priority { get; set; }
    }

}
