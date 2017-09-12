using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using System;
using System.Configuration;

namespace WasteManagement.TelemetryLogger
{
    // https://github.com/elastacloud/parquet-dotnet

    public static class TelemetryLogger
    {
        static string storageAcct = ConfigurationManager.AppSettings["StorageAccount"];
        static CloudStorageAccount storageAccount;
        static CloudQueue loggingQueue;
        static CloudQueueClient queueClient;
        static string loggingQueueName = "logging";

        [FunctionName("TelemetryLogger")]
        public static void Run([TimerTrigger("0 0 */4 * * *")]TimerInfo myTimer, TraceWriter log)
        {

            bool found = true;

            storageAccount = CloudStorageAccount.Parse(storageAcct);
            queueClient = storageAccount.CreateCloudQueueClient();
            loggingQueue = queueClient.GetQueueReference(loggingQueueName);

            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer blobContainer = blobClient.GetContainerReference("logging");
            blobContainer.CreateIfNotExists();


            loggingQueue.FetchAttributes();
            int? cachedMessageCount = loggingQueue.ApproximateMessageCount;
            if (cachedMessageCount == 0 || cachedMessageCount == null) { return; }



            string createddate = Convert.ToDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd/HH-mm/");
            string blobName = "log/" + createddate + "telemetry.json";
            CloudAppendBlob appendBlob = blobContainer.GetAppendBlobReference(blobName);
            appendBlob.Properties.ContentType = "text/json";

            appendBlob.CreateOrReplace();

            while (cachedMessageCount > 0 && found)
            {
                found = false;

                int? messagesToRetrieve = cachedMessageCount > 32 ? 32 : cachedMessageCount;
                cachedMessageCount = cachedMessageCount - messagesToRetrieve;

                foreach (CloudQueueMessage message in loggingQueue.GetMessages((int)messagesToRetrieve, TimeSpan.FromMinutes(2)))
                {
                    found = true;

                    try
                    {
                        appendBlob.AppendText(message.AsString + Environment.NewLine);
                        loggingQueue.DeleteMessage(message);
                    }
                    catch
                    {
                        log.Info("problem logging data");
                    }
                }
            }
        }
    }
}
