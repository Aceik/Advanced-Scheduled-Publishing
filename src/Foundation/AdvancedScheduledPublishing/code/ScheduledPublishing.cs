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
        private const string EnablePublishingSchedule = "EnablePublishingSchedule";
        private const string PublishingSchedules = "PublishingSchedules";
        private const string LastPublishing = "LastPublishing";
        private const string PublishTime = "PublishTime";

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

        private ScheduledPublishingOptions InitialiseSchedulingOptions(Item scheduledPublishingOptionsItem)
        {
            if (scheduledPublishingOptionsItem == null)
            {
                return null;
            }

            var options = new ScheduledPublishingOptions();

            try
            {
                options.SitecoreItem = scheduledPublishingOptionsItem;

                options.EnablePublishingInterval = SetBoolFromCheckbox(scheduledPublishingOptionsItem.Fields[EnablePublishingInterval]);
                options.EnablePublishingSchedule = SetBoolFromCheckbox(scheduledPublishingOptionsItem.Fields[EnablePublishingSchedule]);

                options.StartIntervalTime = DateTime.ParseExact(
                    scheduledPublishingOptionsItem.Fields[StartIntervalTime].Value,
                    PublishTimeFormat,
                    CultureInfo.InvariantCulture);
                options.EndIntervalTime = DateTime.ParseExact(
                    scheduledPublishingOptionsItem.Fields[EndIntervalTime].Value,
                    PublishTimeFormat,
                    CultureInfo.InvariantCulture);

                options.PublishingInterval = TimeSpan.FromMinutes(int.Parse(scheduledPublishingOptionsItem.Fields[PublishingInterval].Value));
                options.LastPublishing = DateUtil.ParseDateTime(scheduledPublishingOptionsItem.Fields[LastPublishing].Value, DateTime.Now);
                options.PublishingSchedules = new List<PublishingSchedule>();

                if (!string.IsNullOrEmpty(scheduledPublishingOptionsItem.Fields[PublishingSchedules].Value))
                {
                    var db = Factory.GetDatabase(MasterDatabaseName);
                    var publishScheduleItemIds = scheduledPublishingOptionsItem.Fields[PublishingSchedules].Value.Split('|');

                    foreach (var id in publishScheduleItemIds)
                    {
                        var publishingScheduleItem = db.GetItem(id);

                        if (publishingScheduleItem == null) continue;
                        var publishingSchedule = new PublishingSchedule
                        {
                            PublishTime = DateTime.ParseExact(
                                publishingScheduleItem.Fields[PublishTime].Value,
                                PublishTimeFormat,
                                CultureInfo.InvariantCulture)
                        };
                        options.PublishingSchedules.Add(publishingSchedule);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("Failed to initialise scheduled publishing options: " + ex.Message, this);
            }

            return options;
        }

        private static void UpdateLastPublishingDate(Item item, DateTime lastPublishing)
        {
            using (new SecurityDisabler())
            {
                item.Editing.BeginEdit();
                item.Fields[LastPublishing].Value = DateUtil.ToIsoDate(lastPublishing);
                item.Editing.EndEdit();
            }
        }

        private static bool SetBoolFromCheckbox(Field field)
        {
            if (field == null) return false;

            return field.Value == CheckBoxTrueValue;
        }

        private void RunPublishingInterval(DateTime now, ScheduledPublishingOptions options)
        {
            if (IsValidPublishingInterval(now, options))
            {
                if (IsValidIntervalTime(now, options))
                {
                    Publish();
                    UpdateLastPublishingDate(options.SitecoreItem, now);
                }
                else
                {
                    Log.Info($"Current time: {now} has not met the next interval time: {options.PublishingInterval}", this);
                }
            }
            else
            {
                Log.Info("No valid publishing interval", this);
            }
        }

        private void RunPublishingSchedule(DateTime now, ScheduledPublishingOptions options)
        {
            if (IsValidPublishingSchedule(options))
            {
                foreach (var publishingSchedule in options.PublishingSchedules)
                {
                    if (IsValidScheduleTime(now, publishingSchedule.PublishTime))
                    {
                        Publish();
                        UpdateLastPublishingDate(options.SitecoreItem, now);
                    }
                    else
                    {
                        Log.Info(
                            $"Current time: {now} has not met the next scheduled time: {publishingSchedule.PublishTime}", this);
                    }
                }
            }
            else
            {
                Log.Info("No valid publishing schedule", this);
            }
        }

        private void Publish()
        {
            var db = Factory.GetDatabase(MasterDatabaseName);
            var rootNode = db.GetItem(RootNode);

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
                    PublishManager.PublishSmart(rootNode.Database, dbs, languages);
                }
                catch (Exception ex)
                {
                    Log.Error("Auto publishing failed: " + ex.Message, this);
                }
            }

            Log.Info("Auto publishing succeeded", this);
        }

        #region Validation

        private bool IsValidOptions(ScheduledPublishingOptions options)
        {
            if (options != null)
            {
                return true;
            }

            Log.Warn("Scheduled publishing options item not found", this);
            return false;
        }

        private static bool IsValidPublishingInterval(DateTime dateTime, ScheduledPublishingOptions options)
        {
            return options.EnablePublishingInterval && DateWithinDateRange(dateTime, options.StartIntervalTime, options.EndIntervalTime);
        }

        private static bool IsValidIntervalTime(DateTime now, ScheduledPublishingOptions options)
        {
            return now - options.LastPublishing > options.PublishingInterval;
        }

        private static bool IsValidPublishingSchedule(ScheduledPublishingOptions options)
        {
            return options.EnablePublishingSchedule && options.PublishingSchedules != null && options.PublishingSchedules.Any();
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