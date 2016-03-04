# JTSmsPrototype
This is a simple prototype showing how you can loop through all jobs/activities and do something useful with them. 
Unfortunately, there's no way to go straight to jobs or activities using the JobTracker API - you have to start with accounts, then get jobs, then get activities. 
That makes the JT API only useful for "batch" functionality (something that's run on a regular basis like hourly or daily).

This example specifically looks for all activities of a certain type (like Install) that are scheduled (estimated or confirmed or auto-scheduled) for tomorrow. 
It then sends an SMS reminder (using Twilio.com) and stores the details of that reminder in another JobTracker activity on the job. 
That 2nd activity is useful to let humans know that a reminder was sent AND so this utility can avoid double-sending the reminder.

## THIS IS JUST A PROTOTYPE!!!! 
Though you might be able to make it "useful" with relatively little work, you still need to "operationalize" it for production. 
While we support the JobTracker API itself, Making YOUR notification tool production ready is beyond the scope of what we can support. 
If you need help, we recommend reaching out to one of our partners (like Data Bridge) at [moraware.com/partners](http://moraware.com/partners).

## Installing / testing the prototype
- Install Visual Studio (Tested on Visual Studio Ultimate 2013 - probably will work on others)
- Install [JobTracker API](http://help.moraware.com/article/380-jobtracker-api) and resolve reference in this project
- Set up [Twilio](http://twilio.com) account and use NuGet to add Twilio reference to this project
- Fill in variables at top of Program.cs
- *Run in debug mode* and step through project to make sure that it does what you expect

## Potential improvements for the reader to implement
- add a custom List of Values field in JT that must be a certain value in order for a reminder to be sent
- add twilio error handling (requires a web page to call back to) - collect together and send email?
- strip phone numbers of non-numbers and verify phone numbers exactly 10 digits, then add +1 (unless already starts with 1, then check to be 11 digits)
- instead of hard-coding rules, put them in a database
- operationalize - make it so the code can be run on a regular schedule, perhaps in Windows Azure
- put credentials/settings in a better place (typically App.config or Web.config, but it's clearer this way for a prototype)