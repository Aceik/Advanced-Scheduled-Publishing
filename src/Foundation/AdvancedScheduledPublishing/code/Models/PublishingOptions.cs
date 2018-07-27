using System;
using System.Collections.Generic;
using Sitecore.Data.Items;

namespace Sitecore.Foundation.AdvancedScheduledPublishing.Models
{
    internal class PublishingOptions
    {
        public IntervalPublishingOptions IntervalPublishingOptions { get; set; }
        public ScheduledPublishingOptions ScheduledPublishingOptions { get; set; }
        public Item SitecoreItem { get; set; }
    }
}