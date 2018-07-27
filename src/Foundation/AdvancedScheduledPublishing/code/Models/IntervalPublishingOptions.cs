using System;
using System.Collections.Generic;
using Sitecore.Data.Items;

namespace Sitecore.Foundation.AdvancedScheduledPublishing.Models
{
    internal class IntervalPublishingOptions
    {
        public IntervalPublishingOptions()
        {
            Items = new List<Item>();
        }
        public bool EnablePublishingInterval { get; set; }
        public TimeSpan PublishingInterval { get; set; }
        public DateTime StartIntervalTime { get; set; }
        public DateTime EndIntervalTime { get; set; }

        public List<Item> Items { get; set; }
        public DateTime? LastPublishing { get; set; }
    }
}