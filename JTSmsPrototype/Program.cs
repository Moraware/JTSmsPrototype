using Moraware.JobTrackerAPI4;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Twilio;

namespace JTSmsPrototype
{
    class Program
    {
        /*
         * This is a simple prototype showing how you can loop through all jobs/activities and do something useful with them
         * Unfortunately, there's no way to go straight to jobs or activities - you have to start with accounts, then get jobs, then get activities
         * That makes the JT API only useful for "batch" functionality (something that's run on a regular basis like hourly or daily)
         * 
         * This example specifically looks for all activities of a certain type (like Install) that are scheduled 
         * (estimated or confirmed or auto-scheduled) for tomorrow.
         *
         * It then sends an SMS reminder (using Twilio.com) and stores the details of that reminder in another JobTracker activity on the job
         * That 2nd activity is useful to let humans know that a reminder was sent AND so this utility can avoid double-sending the reminder
         * 
         * THIS IS JUST A PROTOTYPE!!!! Though you might be able to make it useful with relatively little work, there is still work to do to 
         * "operationalize" it for your needs. Making your utility operational is beyond the scope of what we can support. If you need help,
         * we recommend reaching out to one of our partners (like Data Bridge) at moraware.com/partners
         * 
         */

        //Potential improvements for the reader to implement
        // - add a custom List of Values field in JT that must be a certain value in order for a reminder to be sent
        // - add twilio error handling (requires a web page to call back to)
        //      - collect together and send email?
        // - strip phone numbers of non-numbers and verify phone numbers exactly 10 digits, then add +1 (unless already starts with 1, then check to be 11 digits)
        // - instead of hard-coding rules, put them in a database
        // - operationalize - make it so the code can be run on a regular schedule, perhaps in Windows Azure
        // - put credentials/settings in a better place (typically App.config or Web.config, but it's clearer this way for a prototype)
        // 

        static void Main(string[] args)
        {
            // ALL OF THESE DATA NEED TO BE SET FOR YOUR SPECIFIC SITUATION
            ///////////
            var DB = ""; // this should be the part before moraware.net - so if your Moraware database is at patrick.moraware.net, enter "patrick"
            var JTURL = "https://" + DB + ".moraware.net/";
            var UID = ""; // enter a Moraware user id with API access
            var PWD = ""; // the password for that user id
            var OUTPUTFILE = "c:\\" + DB + "-SmsPrototype.html"; // decide where you want this
            var INSTALLTYPE = 124; // JobActivityTypeId for Install on my test DB - YOURS WILL BE DIFFERENT
                                    // To find it, click on the Activity Type (under job > edit settings) and choose View Change Log
                                    // this number will be the last part of the URL, id=124 in my sample database
            var REMINDERTYPE = 1283; // JobActivityTypeId for the activity you create in JobTracker to show you sent a text 
                                    // - mine was 1283 but YOURS WILL BE DIFFERENT ...
                                    // to find it, first create the Activity Type under Job > Edit Settings ... I called mine "Automatic Reminder"
                                    // then click on the Activity Type (still under job settings) and choose View Change Log for Automatic Reminder
                                    // this number will be the last part of the URL, id=1283 in my sample database
            var REMINDERSTATUS = 3; // JobActivityStatusId for the activity. Complete was 3 for me. It's probably 3 for you as well, but it's not guaranteed

            // Find your Account Sid and Auth Token at twilio.com/user/account
            string ACCOUNTSID = "";
            string AUTHTOKEN = "";
            var TWILIOFROM = ""; // Comes from your Twilio account. Make it look like this: +16162005000

            var twilio = new TwilioRestClient(ACCOUNTSID, AUTHTOKEN); // consider if this needs to be disposed

            StreamWriter w = File.AppendText(OUTPUTFILE); // should be wrapped in a using block for production
            w.AutoFlush = true;
            w.WriteLine("*********************<br />");
            w.WriteLine("JT SMS Prototype Start {0}<br />", DateTime.Now);
            w.WriteLine("*********************<br />");

            Connection conn = new Connection(JTURL + "api.aspx", UID, PWD);
            conn.Connect();

            var tomorrow = DateTime.Now.AddDays(1);
            DateTime startDate;

            var af = new AccountFilter(Moraware.JobTrackerAPI4.Account.AccountStatusType_Enum.Active);
            var accounts = conn.GetAccounts(af, new PagingOptions(0, 9999, true)); // if you have a huge number of accounts (> 9999), you'll have to page
            foreach (var account in accounts)
            {
                var jobs = conn.GetJobsForAccount(account.AccountId, true);
                foreach (var job in jobs)
                {
                    Console.WriteLine("Checking {0}: {1}", job.JobId, job.JobName);
                    if (job.Address == null)
                    {
                        continue;
                    }
                    if (String.IsNullOrEmpty(job.Address.Cell))
                    {
                        continue;
                    }
                    var installsToConfirm = new List<JobActivity>();
                    var confirmations = new Dictionary<int, JobActivity>();

                    var activities = conn.GetJobActivities(job.JobId, true, false);
                    foreach (var activity in activities)
                    {
                        if (activity.StartDate == null)
                        {
                            continue;
                        }
                        else
                        {
                            startDate = (DateTime)activity.StartDate;
                        }

                        // CHECK RULES HERE - there's no magic formula ... your rules can be completely different
                        if (activity.JobActivityTypeId == INSTALLTYPE && // Install
                            (activity.JobActivityStatusId == 1 || activity.JobActivityStatusId == 4 || activity.JobActivityStatusId == 2) && // Estimate or Auto-Schedule or Confirmed
                            // these status id's are not guaranteed to be the same for your database - use id's that are appropriate for you
                            startDate.Date == tomorrow.Date) // Starts tomorrow
                        {
                            installsToConfirm.Add(activity);
                        }

                        // CHECK FOR EXISTING REMINDERS HERE (don't double send)
                        if (activity.JobActivityTypeId == REMINDERTYPE)
                        {
                            var match = Regex.Match(activity.Notes, @"Reference activity: (\d+)");
                            if (match.Success)
                            {
                                int refId;
                                try
                                {
                                    refId = Int32.Parse(match.Groups[1].Value);
                                    confirmations.Add(refId, activity);
                                }
                                catch (Exception)
                                {
                                    // send error message somewhere?
                                }
                            }
                        }
                    }

                    foreach (var activity in installsToConfirm)
                    {
                        if (confirmations.ContainsKey(activity.JobActivityId))
                        {
                            //we've already created a notification for this activity, so don't resend it
                            continue;
                        }

                        Console.WriteLine("Create a notification for job id {0}, activity id {1}, type id {2}, status id {3}",
                            activity.JobId, activity.JobActivityId, activity.JobActivityTypeId, activity.JobActivityStatusId);

                        // Here's the message - edit to suit your needs!
                        String msg = String.Format("Reminder, you have a countertop installation scheduled for {0:M/dd} at {1:H:mm tt}. If this information is incorrect, please call xxx-xxx-xxxx as soon as possible. Thank you!",
                            activity.StartDate, activity.ScheduledTime);

                        var cell = job.Address.Cell; // NOTE - you should strip out text and validate this

                        String notes = String.Format("Type: SMS\nNumber: {0}\nReference activity: {1}\nMessage: {2}",
                            cell, activity.JobActivityId, msg);

                        Console.WriteLine(notes);

                        var reminder = new JobActivity(activity.JobId, REMINDERTYPE, REMINDERSTATUS);
                        foreach (var phase in activity.JobPhases)
                        {
                            reminder.JobPhases.Add(phase);
                        }
                        reminder.Notes = notes;
                        conn.CreateJobActivity(reminder); // once this is added, the notification won't be sent again ... so if twilio fails, this won't retry

                        // FINALLY, actually send the text
                        var message = twilio.SendMessage(TWILIOFROM, cell, msg, ""); // last param is a callback to handle errors/status ... needs to call a web service (not implemented here)

                    }
                }
            }

            Console.WriteLine("Press any key to continue ...");
            Console.ReadKey();

            w.Close();
            conn.Disconnect();
        }
    }
}
