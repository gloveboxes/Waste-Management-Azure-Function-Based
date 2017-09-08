using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using System;
using System.Collections.Generic;
using System.Configuration;

namespace WasteManagement.SmsAlerter
{
    public static class SendAlert
    {
        static string storageAcct = ConfigurationManager.AppSettings["StorageAccount"];
        static CloudStorageAccount storageAccount;
        static CloudQueueClient queueClient;
        static CloudQueue alertQueue;


        static string telstraSmsApiConsumerKey = ConfigurationManager.AppSettings["TelstraSMSApiConsumerKey"];
        static string telstraSmsConsumerSecret = ConfigurationManager.AppSettings["TelstraSmsConsumerSecret"];
        static string alertQueueName = "alerts";


        [FunctionName("SendAlertTrigger")]
        public static void Run([TimerTrigger("0 * */2 * * *")]TimerInfo AlertTimer, TraceWriter log)
        {
            List<CloudQueueMessage> messages = new List<CloudQueueMessage>();
            var smsManager = new SmsManager(telstraSmsApiConsumerKey, telstraSmsConsumerSecret);

            bool found = true;

            storageAccount = CloudStorageAccount.Parse(storageAcct);
            queueClient = storageAccount.CreateCloudQueueClient();
            alertQueue = queueClient.GetQueueReference(alertQueueName);


            alertQueue.FetchAttributes();
            int? cachedMessageCount = alertQueue.ApproximateMessageCount;
            if (cachedMessageCount == null) { return; }

            while (cachedMessageCount > 0 && found)
            {
                found = false;

                int? messagesToRetrieve = cachedMessageCount > 32 ? 32 : cachedMessageCount;
                cachedMessageCount = cachedMessageCount - messagesToRetrieve;

                foreach (CloudQueueMessage message in alertQueue.GetMessages((int)messagesToRetrieve, TimeSpan.FromMinutes(2)))
                {
                    found = true;
                    messages.Add(message);

                    try
                    {
                        smsManager.ProcessSmsEntity(message.AsString);
                    }
                    catch
                    {
                        log.Info("Invalid data");
                    }
                }
            }

            smsManager.SendAlerts(log);

            foreach (var message in messages)
            {
                alertQueue.DeleteMessage(message);
            }           
        }
    }
}