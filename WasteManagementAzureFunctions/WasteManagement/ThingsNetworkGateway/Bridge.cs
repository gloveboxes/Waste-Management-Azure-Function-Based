using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Configuration;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using WasteManagement.Models;

namespace WasteManagement.ThingsNetworkGateway
{
    public static class Bridge
    {
        static CloudStorageAccount storageAccount;
        static CloudQueueClient queueClient;
        static CloudQueue telemetryQueue;
        static string telemetryQueueName = "telemetry";

        public class Telemetry
        {
            public string DeviceId { get; set; } = string.Empty;
            public uint Level { get; set; }
            public int MsgId { get; set; } = 1;
            public int Schema { get; set; } = 1;
            public DateTime Timestamp { get; set; }
        }

        const int ResonableLevelMax = 100; // 100% Level

        static string storageAcct = ConfigurationManager.AppSettings["StorageAccount"];
        static string ttnAppIDs = ConfigurationManager.AppSettings["TTNAppIDsCommaSeperated"];


        [FunctionName("TheThingsNetworkBridge")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = null)]HttpRequestMessage req, TraceWriter log)
        {
            DateTime timestamp = DateTime.UtcNow;
            TheThingsNetworkEntity ttn;            
            Telemetry telemetry = new Telemetry();

            dynamic data = await req.Content.ReadAsAsync<object>(); // Get request body

            try
            {
                ttn = JsonConvert.DeserializeObject<TheThingsNetworkEntity>(data.ToString(), new JsonSerializerSettings() { ContractResolver = new CamelCasePropertyNamesContractResolver() });
            }
            catch
            {
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            if (!ValidTtnApplicationId(ttn.app_id)) {
                log.Info("Invalid The Things Network Appid");
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            var result = DecodeRawData(ttn.payload_raw);

            if (result > ResonableLevelMax)
            {
                log.Info($"Level data invalid. {result} > {ResonableLevelMax}");
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            storageAccount = CloudStorageAccount.Parse(storageAcct);
            queueClient = storageAccount.CreateCloudQueueClient();
            telemetryQueue = queueClient.GetQueueReference(telemetryQueueName);
            

            telemetry.DeviceId = ttn.dev_id;
            telemetry.Timestamp = timestamp;
            telemetry.Level = result;

            try
            {
                string json = JsonConvert.SerializeObject(telemetry);
                telemetryQueue.AddMessage(new CloudQueueMessage(json));
            }
            catch (Exception ex)
            {
                log.Info($"Problem queuing telemetry: {ex.Message}");
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            log.Info($"device '{ttn.dev_id}', raw payload '{ttn.payload_raw}', decoded value '{result}'");

            return req.CreateResponse(HttpStatusCode.OK);
        }

        public static uint DecodeRawData(string base64EncodedData)
        {
            uint result = 0;
            var base64EncodedBytes = System.Convert.FromBase64String(base64EncodedData);
            var d = System.Text.Encoding.UTF7.GetString(base64EncodedBytes);

            for (int i = 0; i < d.Length; i++)
            {
                result = result << (8);
                result += (byte)d[i];
            }
            return result;
        }

        public static bool ValidTtnApplicationId(string appId)
        {
            bool found = false;
            string[] appIds = ttnAppIDs.Split(',');
            foreach (string id in appIds)
            {
                if (appId == id)
                {
                    found = true;
                    break;
                }
            }
            return found;
        }
    }
}
