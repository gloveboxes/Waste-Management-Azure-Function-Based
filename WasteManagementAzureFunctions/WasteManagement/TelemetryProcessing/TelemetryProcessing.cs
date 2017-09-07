//https://docs.microsoft.com/en-au/azure/storage/queues/storage-dotnet-how-to-use-queues

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage; // Namespace for CloudStorageAccount
using Microsoft.WindowsAzure.Storage.Queue; // Namespace for Queue storage types
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using WasteManagement.Models;

namespace WasteManagement.TelemetryProcessing
{
    public static class TelemetryProcessing
    {
        static CloudStorageAccount storageAccount;
        static CloudTableClient tableClient;
        static CloudTable sensorStateTable;
        static CloudTable weatherTable;
        static CloudQueueClient queueClient;
        static CloudQueue telemetryQueue;
        static CloudQueue archiveQueue;

        static string storageAcct = ConfigurationManager.AppSettings["StorageAccount"];
        //static string alertLevelSetting = ConfigurationManager.AppSettings["AlertLevel"];
        static string sensorStateTableName = "SensorState";
        static string telemetryQueueName = "telemetry";

        static WeatherEntity weatherEntity = new WeatherEntity();
        static string weatherTableName = "Weather";
        static string weatherRowkey = ConfigurationManager.AppSettings["OwmLocation"];
        static string weatherPartionkey = "sfm";

        static string alertQueueName = "alerts";
        static string archiveQueueName = "archive";
        static int alertLevel = 80;

        [FunctionName("TelemetryProcessing")]
        public static void Run([TimerTrigger("0 */10 * * * *")]TimerInfo myTimer, TraceWriter log)
        {
            List<CloudQueueMessage> messages = new List<CloudQueueMessage>();
            List<TelemetryEntity> telemetry = new List<TelemetryEntity>();
            List<SensorEntity> sensor = new List<SensorEntity>();
            bool found = true;

            Initialise();

            telemetryQueue.FetchAttributes();

            int? cachedMessageCount = telemetryQueue.ApproximateMessageCount;
            if (cachedMessageCount == null) { return; }

            while (cachedMessageCount > 0 && found)
            {
                found = false;

                int? messagesToRetrieve = cachedMessageCount > 32 ? 32 : cachedMessageCount;
                cachedMessageCount = cachedMessageCount - messagesToRetrieve;

                foreach (CloudQueueMessage message in telemetryQueue.GetMessages((int)messagesToRetrieve, TimeSpan.FromMinutes(2)))
                {
                    found = true;
                    messages.Add(message);
                    try
                    {
                        telemetry.Add(JsonConvert.DeserializeObject<TelemetryEntity>(message.AsString, new JsonSerializerSettings() { ContractResolver = new CamelCasePropertyNamesContractResolver() }));
                    }
                    catch { log.Info("Invalid data"); }
                }
            }

            List<TelemetryEntity> queryResult = UpdateSensorState(telemetry, sensor, log);

            WriteAlerts(sensor, queryResult, log);

            foreach (var message in messages)
            {
                telemetryQueue.DeleteMessage(message);
            }
        }

        private static void Initialise()
        {
            storageAccount = CloudStorageAccount.Parse(storageAcct);

            queueClient = storageAccount.CreateCloudQueueClient();
            telemetryQueue = queueClient.GetQueueReference(telemetryQueueName);
            archiveQueue = queueClient.GetQueueReference(archiveQueueName);

            tableClient = storageAccount.CreateCloudTableClient();
            sensorStateTable = tableClient.GetTableReference(sensorStateTableName);
            weatherTable = tableClient.GetTableReference(weatherTableName);

            GetCurrentWeather();
        }

        private static void GetCurrentWeather()
        {
            TableOperation retrieveOperation = TableOperation.Retrieve<WeatherEntity>(weatherPartionkey, weatherRowkey);     //retrieve Bin SMS Alter data
            TableResult retrievedResult = weatherTable.Execute(retrieveOperation);

            if (retrievedResult.Result != null)
            {
                weatherEntity = (WeatherEntity)retrievedResult.Result;
            }
        }

        private static void WriteAlerts(List<SensorEntity> sensor, List<TelemetryEntity> queryResult, TraceWriter log)
        {
            var alertResult = (from l in queryResult
                               join s in sensor on l.DeviceId equals s.RowKey
                               select new TelemetryEntity()
                               {
                                   DeviceId = l.DeviceId,
                                   Level = l.Level,
                                   Timestamp = l.Timestamp,
                                   Location = s.Location,
                                   PhoneNumber = s.PhoneNumber
                               }).Where(z => z.Level > alertLevel).ToList();

            log.Info($"{alertResult.Count} alerts queued");

            if (alertResult.Count == 0) { return; }

            // write alerts to alert queue
            CloudQueue alertQueue = queueClient.GetQueueReference(alertQueueName);

            foreach (var item in alertResult)
            {
                string json = JsonConvert.SerializeObject(item);
                alertQueue.AddMessage(new CloudQueueMessage(json));
            }
        }

        private static List<TelemetryEntity> UpdateSensorState(List<TelemetryEntity> telemetry, List<SensorEntity> sensor, TraceWriter log)
        {
            var queryResult = (from l in telemetry
                               select new TelemetryEntity()
                               {
                                   DeviceId = l.DeviceId,
                                   Level = l.Level,
                                   Temperature = weatherEntity.Temperature,
                                   Pressure = weatherEntity.Pressure,
                                   Humidity = weatherEntity.Humidity,
                                   Precipitation = weatherEntity.Precipitation,
                                   WindSpeed = weatherEntity.WindSpeed,
                                   Clouds = weatherEntity.Clouds,
                                   Weather = weatherEntity.Weather,
                                   Timestamp = DateTime.UtcNow
                               }).GroupBy(x => x.DeviceId).Select(z => z.OrderByDescending(i => i.Timestamp).First()).ToList();

            log.Info($"{queryResult.Count} sensor data items archived");

            foreach (var item in queryResult)
            {
                TableOperation retrieveOperation = TableOperation.Retrieve<SensorEntity>("sfm", item.DeviceId);     //retrieve Bin SMS Alter data
                TableResult retrievedResult = sensorStateTable.Execute(retrieveOperation);

                if (retrievedResult.Result != null)
                {
                    var sensorEntity = (SensorEntity)retrievedResult.Result;

                    // update sensor state
                    sensor.Add(sensorEntity);
                    sensorEntity.Level = item.Level;

                    TableOperation mergeOperation = TableOperation.Merge(sensorEntity);
                    sensorStateTable.Execute(mergeOperation);

                    item.Location = sensorEntity.Location;
                    item.PhoneNumber = sensorEntity.PhoneNumber;

                    string json = JsonConvert.SerializeObject(item);
                    archiveQueue.AddMessage(new CloudQueueMessage(json));
                }
            }
            return queryResult;
        }
    }
}
