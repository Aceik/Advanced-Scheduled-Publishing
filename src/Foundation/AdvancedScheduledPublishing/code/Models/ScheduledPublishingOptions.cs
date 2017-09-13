using System;
using System.Collections.Generic;
using Sitecore.Data.Items;

namespace Sitecore.Foundation.AdvancedScheduledPublishing.Models
{
    internal class ScheduledPublishingOptions
    {
        public bool EnablePublishingInterval { get; set; }
        public TimeSpan PublishingInterval { get; set; }
        public DateTime StartIntervalTime { get; set; }
        public DateTime EndIntervalTime { get; set; }
        public bool EnablePublishingSchedule { get; set; }
        public List<PublishingSchedule> PublishingSchedules { get; set; }
        public DateTime LastPublishing { get; set; }
        public Item SitecoreItem { get; set; }
    }
}