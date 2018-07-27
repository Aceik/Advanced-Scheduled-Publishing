using Sitecore.Data.Items;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.Fields;
using Sitecore.Diagnostics;
using Sitecore.Foundation.AdvancedScheduledPublishing.Models;
using Sitecore.Globalization;
using Sitecore.Publishing;
using Sitecore.SecurityModel;
using Sitecore.Tasks;

namespace Sitecore.Foundation.AdvancedScheduledPublishing
{
    public class ScheduledPublishing
    {
        #region Constants

        private const string PublishingTargetsPath = "/sitecore/System/Publishing targets";
        private const string RootNode = "/sitecore";
        private const string MasterDatabaseName = "master";
        private const string TargetDatabase = "target database";
        private const string PublishTimeFormat = "HHmm";
        private const string CheckBoxTrueValue = "1";

        //Field Names
        private const string EnablePublishingInterval = "EnablePublishingInterval";
        private const string PublishingInterval = "PublishingInterval";
        private const string StartIntervalTime = "StartIntervalTime";
        private const string EndIntervalTime = "EndIntervalTime";
        private const string IntervalPublishingItems = "IntervalPublishingItems";
        private const string LastIntervalPublishing = "LastIntervalPublishing";

        private const string EnablePublishingSchedule = "EnablePublishingSchedule";
        private const string ScheduledPublishingItems = "ScheduledPublishingItems";
        private const string LastScheduledPublishing = "LastScheduledPublishing";
        private const string ScheduledPublishTime = "ScheduledPublishTime";

        #endregion Constants

        private static TimeSpan ScheduledAgentFrequency => TimeSpan.Parse(Factory.GetConfigNode("scheduling/frequency")?.InnerXml ?? "00:05:00");

        public void Run(Item[] itemArray, CommandItem commandItem, ScheduleItem scheduledItem)
        {
            Log.Info("Running Scheduled Publishing Task", this);

            var options = InitialiseSchedulingOptions(itemArray[0]);

            if (!IsValidOptions(options)) return;

            var now = DateTime.Now;
            RunPublishingInterval(now, options);
            RunPublishingSchedule(now, options);
        }

        private PublishingOptions InitialiseSchedulingOptions(Item publishingOptionsItem)
        {
            if (publishingOptionsItem == null)
            {
                return null;
            }

            var options = new PublishingOptions();

            try
            {
                options.SitecoreItem = publishingOptionsItem;

                // Interval Publishing

                options.IntervalPublishingOptions = new IntervalPublishingOptions
                {
                    EnablePublishingInterval =
                        SetBoolFromCheckbox(publishingOptionsItem.Fields[EnablePublishingInterval]),

                    LastPublishing =
                        string.IsNullOrWhiteSpace(publishingOptionsItem.Fields[LastIntervalPublishing].Value)
                            ? null
                            : (DateTime?) DateUtil.ParseDateTime(publishingOptionsItem.Fields[LastIntervalPublishing].Value, DateTime.Now),

                    StartIntervalTime =
                        publishingOptionsItem.Fields[StartIntervalTime].HasValue
                        ?
                        DateTime.ParseExact(
                        publishingOptionsItem.Fields[StartIntervalTime].Value,
                        PublishTimeFormat,
                        CultureInfo.InvariantCulture)
                        : DateTime.MinValue,

                    EndIntervalTime =
                        publishingOptionsItem.Fields[EndIntervalTime].HasValue
                        ?
                        DateTime.ParseExact(
                        publishingOptionsItem.Fields[EndIntervalTime].Value,
                        PublishTimeFormat,
                        CultureInfo.InvariantCulture)
                        : DateTime.MinValue,

                    PublishingInterval = publishingOptionsItem.Fields[PublishingInterval].HasValue
                        ? TimeSpan.FromMinutes(int.Parse(publishingOptionsItem.Fields[PublishingInterval].Value))
                        : TimeSpan.Zero
                };

                if (!string.IsNullOrEmpty(publishingOptionsItem.Fields[IntervalPublishingItems].Value))
                {
                    var db = Factory.GetDatabase(MasterDatabaseName);
                    var publishScheduleItemIds = publishingOptionsItem.Fields[IntervalPublishingItems].Value.Split('|');
                    
                    foreach (var id in publishScheduleItemIds)
                    {
                        var item = db.GetItem(id);

                        if (item == null) continue;
                        options.IntervalPublishingOptions.Items.Add(item);
                    }
                }


                // Scheduled Publishing

                options.ScheduledPublishingOptions = new ScheduledPublishingOptions
                {
                    EnablePublishingSchedule =
                        SetBoolFromCheckbox(publishingOptionsItem.Fields[EnablePublishingSchedule]),
                    LastPublishing = string.IsNullOrWhiteSpace(publishingOptionsItem.Fields[LastScheduledPublishing].Value)
                        ? null
                        : (DateTime?)(DateUtil.ParseDateTime(publishingOptionsItem.Fields[LastScheduledPublishing].Value, DateTime.Now)),
                    PublishTime = publishingOptionsItem.Fields[ScheduledPublishTime].HasValue
                        ? DateTime.ParseExact(publishingOptionsItem.Fields[ScheduledPublishTime].Value, PublishTimeFormat, CultureInfo.InvariantCulture)
                        : DateTime.MaxValue
                };

                if (!string.IsNullOrEmpty(publishingOptionsItem.Fields[ScheduledPublishingItems].Value))
                {
                    var db = Factory.GetDatabase(MasterDatabaseName);
                    var publishScheduleItemIds = publishingOptionsItem.Fields[ScheduledPublishingItems].Value.Split('|');
                    foreach (var id in publishScheduleItemIds)
                    {
                        var item = db.GetItem(id);

                        if (item == null) continue;
                        options.ScheduledPublishingOptions.Items.Add(item);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("Failed to initialise scheduled publishing options: " + ex.Message, this);
            }

            return options;
        }

        private static void UpdateLastPublishingDate(Item item, string fieldName, DateTime lastPublishing)
        {
            using (new SecurityDisabler())
            {
                item.Editing.BeginEdit();
                item.Fields[fieldName].Value = DateUtil.ToIsoDate(lastPublishing);
                item.Editing.EndEdit();
            }
        }

        private static bool SetBoolFromCheckbox(Field field)
        {
            if (field == null) return false;

            return field.Value == CheckBoxTrueValue;
        }

        private void RunPublishingInterval(DateTime now, PublishingOptions options)
        {
            if (IsValidPublishingInterval(now, options.IntervalPublishingOptions))
            {
                if (IsValidIntervalTime(now, options.IntervalPublishingOptions))
                {
                    Log.Info("Running Interval Publishing for items:", this);
                    options.IntervalPublishingOptions.Items.ForEach(x => Log.Info($"{x.Paths.Path}", this));

                    Publish(options.IntervalPublishingOptions.Items);
                    UpdateLastPublishingDate(options.SitecoreItem, LastIntervalPublishing, now);
                }
                else
                {
                    Log.Info($"Current time: {now} has not met the next interval time: {options.IntervalPublishingOptions.PublishingInterval}", this);
                }
            }
            else
            {
                Log.Info("No valid publishing interval", this);
            }
        }

        private void RunPublishingSchedule(DateTime now, PublishingOptions options)
        {
            if (IsValidPublishingSchedule(options.ScheduledPublishingOptions))
            {
                if (IsValidScheduleTime(now, options.ScheduledPublishingOptions.PublishTime))
                {
                    Log.Info("Running Scheduled Publishing for items:", this);
                    options.ScheduledPublishingOptions.Items.ForEach(x => Log.Info($"{x.Paths.Path}", this));

                    Publish(options.IntervalPublishingOptions.Items);
                    UpdateLastPublishingDate(options.SitecoreItem, LastScheduledPublishing, now);
                }
                else
                {
                    Log.Info(
                        $"Current time: {now} has not met the next scheduled time: {options.ScheduledPublishingOptions.PublishTime}", this);
                }
            }
            else
            {
                Log.Info("No valid publishing schedule", this);
            }
        }

        private void Publish(List<Item> itemsToPublish)
        {
            var db = Factory.GetDatabase(MasterDatabaseName);
            
            Language[] languages = { Language.Current };

            var publishTarget = db.GetItem(PublishingTargetsPath);
            var dbList = string.Empty;
            foreach (Item dbName in publishTarget.GetChildren())
            {
                dbList += dbName[TargetDatabase] + ",";
            }
            var dbNames = dbList.Substring(0, dbList.Length - 1).Split(',');

            var dbsList = new List<Database>();
            foreach (var name in dbNames)
            {
                try
                {
                    dbsList.Add(Factory.GetDatabase(name));
                }
                catch (Exception ex)
                {
                    Log.Error("Cannot find predefined database: " + ex.Message, this);
                }
            }

            if (dbsList.Count > 0)
            {
                try
                {
                    var dbs = dbsList.ToArray();
                    itemsToPublish.ForEach(x => PublishManager.PublishSmart(x.Database, dbs, languages));
                }
                catch (Exception ex)
                {
                    Log.Error("Auto publishing failed: " + ex.Message, this);
                }
            }

            Log.Info("Auto publishing succeeded", this);
        }

        #region Validation

        private bool IsValidOptions(PublishingOptions options)
        {
            if (options != null)
            {
                return true;
            }

            Log.Warn("Scheduled publishing options item not found", this);
            return false;
        }

        private static bool IsValidPublishingInterval(DateTime dateTime, IntervalPublishingOptions options)
        {
            return options.EnablePublishingInterval && DateWithinDateRange(dateTime, options.StartIntervalTime, options.EndIntervalTime);
        }

        private static bool IsValidIntervalTime(DateTime now, IntervalPublishingOptions options)
        {
            return options.LastPublishing == null || now - options.LastPublishing > options.PublishingInterval;
        }

        private static bool IsValidPublishingSchedule(ScheduledPublishingOptions options)
        {
            return options.EnablePublishingSchedule && options.Items != null && options.Items.Any();
        }

        private static bool IsValidScheduleTime(DateTime now, DateTime schduleDateTime)
        {
            var schduleEndDateTime = schduleDateTime.Add(ScheduledAgentFrequency);
            return DateWithinDateRange(now, schduleDateTime, schduleEndDateTime);
        }

        private static bool DateWithinDateRange(DateTime datetime, DateTime startDateTime, DateTime endDateTime)
        {
            return datetime >= startDateTime && datetime < endDateTime;
        }

        #endregion Validation
    }



}