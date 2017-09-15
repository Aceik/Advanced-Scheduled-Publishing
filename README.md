# Advanced-Scheduled-Publishing

This module provides more options for scheduled publishing.  

Rather than use the Sitecore default of having publishing run continuously at a set interval, this module allows the interval publishing to occur only during a set time range and/or at a set time of day.

## Setup

This package will install the following templates:
* /sitecore/templates/Foundation/Advanced Scheduled Publishing/Publishing Schedule
* /sitecore/templates/Foundation/Advanced Scheduled Publishing/Scheduled Publishing Options

and the command:
* /sitecore/system/Tasks/Commands/Scheduled Publish/Scheduled Publish

To setup Advanced Scheduled Publishing, follow these steps:
1. Create a new item using the "Scheduled Publishing Options" template.
2. Choose Enable Publishing Interval or Enable Publishing Schedule (or both).  
	For Interval publishing, enter the Publishing Interval in minutes and the start and end interval times (as HHmm).  
	For Schedule Publishing, create an item using the "Publishing Schedule" template and enter the publishing time (as HHmm), then add that item to the Publishing Schedules.
3. Create a Schedule item under /sitecore/system/Tasks/Schedules/.  
	Set its Command to be /sitecore/system/Tasks/Commands/Scheduled Publish/Scheduled Publish.  
	In the Items field, enter the ID of the item created in step 1.  
	In the Schedule field, enter a pipe-delimited value in the format StartDate|FinishDate|DaysBits|Interval.  
	An example is 20131231T130000|20981231T130000|127|00:10:00, which runs every 10 minutes, every day between 31 Dec 2013 and 31 Dec 2098.  

When the Advanced Scheduled Publishing is being used, the Sitecore Publish Agent should be disabled by setting the interval to 00:00:00 as follows:  
`<agent type="Sitecore.Tasks.PublishAgent" method="Run" interval="00:00:00">`