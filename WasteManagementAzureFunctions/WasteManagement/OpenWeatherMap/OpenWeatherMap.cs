using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using OpenWeatherMap;
using System;
using System.Configuration;
using WasteManagement.Models;

namespace WasteManagement.OpenWeatherMap
{
    public class LatestWeather
    {
        static OpenWeatherMapClient client;
        static string owmKey = ConfigurationManager.AppSettings["OpenWeatherMapKey"];
        static string owmLocation = ConfigurationManager.AppSettings["OwmLocation"];
        static string storageAcct = ConfigurationManager.AppSettings["StorageAccount"];
        static string appId = ConfigurationManager.AppSettings["ApplicationId"];
        static string weatherTable = "Weather";

        static WeatherEntity weatherEntity = new WeatherEntity();
        static CloudStorageAccount storageAccount;
        static CloudTableClient tableClient;
        static CloudTable table;

        [FunctionName("LatestWeather")]
        public static async void Run([TimerTrigger("0 */15 * * * *")]TimerInfo AlertTimer, TraceWriter log)
        {
            storageAccount = CloudStorageAccount.Parse(storageAcct);
            tableClient = storageAccount.CreateCloudTableClient();     // Create the table client.
            table = tableClient.GetTableReference(weatherTable);
            client = new OpenWeatherMapClient(owmKey);

            log.Info($"Open Weather Map Timer trigger function executed at: {DateTime.Now}");

            var result = await client.CurrentWeather.GetByName(owmLocation, MetricSystem.Metric);

            weatherEntity.Temperature = result.Temperature.Value;
            weatherEntity.Pressure = result.Pressure.Value;
            weatherEntity.Humidity = result.Humidity.Value;
            weatherEntity.Precipitation = result.Precipitation.Value;
            weatherEntity.WindSpeed = result.Wind.Speed.Value;
            weatherEntity.Clouds = result.Clouds.Value;
            weatherEntity.Weather = result.Weather.Value;

            weatherEntity.PartitionKey = appId;
            weatherEntity.RowKey = owmLocation;

            TableOperation insertOperation = TableOperation.InsertOrReplace(weatherEntity);
            table.Execute(insertOperation);
        }
    }
}