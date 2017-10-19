using Google.ProtocolBuffers;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Configuration;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using WasteManagement.Models;

namespace WasteManagement.ThingsNetworkGateway
{
    public static class Bridge
    {
        public class Telemetry
        {
            public string DeviceId { get; set; } = string.Empty;
            public int Level { get; set; }
            public int Battery { get; set; }
            public int MsgId { get; set; } = 1;
            public int Schema { get; set; } = 1;
            public DateTime Timestamp { get; set; }
        }

        static CloudStorageAccount storageAccount;
        static CloudQueueClient queueClient;
        static CloudQueue telemetryQueue;
        static Telemetry telemetry = new Telemetry();

        static string telemetryQueueName = "telemetry";

        const int ResonableLevelMax = 200;

        static string storageAcct = ConfigurationManager.AppSettings["StorageAccount"];
        static string ttnAppIDs = ConfigurationManager.AppSettings["TTNAppIDsCommaSeperated"];
        

        [FunctionName("TheThingsNetworkBridge")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = null)]HttpRequestMessage req, TraceWriter log)
        {
            DateTime timestamp = DateTime.UtcNow;
            TheThingsNetworkEntity ttn;

            dynamic data = await req.Content.ReadAsAsync<object>(); // Get request body

            try
            {
                ttn = JsonConvert.DeserializeObject<TheThingsNetworkEntity>(data.ToString(), new JsonSerializerSettings() { ContractResolver = new CamelCasePropertyNamesContractResolver() });
            }
            catch
            {
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            if (!ValidTtnApplicationId(ttn.app_id))
            {
                log.Info($"Invalid The Things Network Appid: {ttn.app_id}");
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            var packetBytes = Convert.FromBase64String(ttn.payload_raw);

            // Use the google protocol buffers library to decode the bytes according to the Protos/SkippyMessage.proto file
            var packet = SkippyCore.SkippyMessage.CreateBuilder().MergeFrom(ByteString.Unsafe.FromBytes(packetBytes)).Build();
            
            var packetJson = packet.ToJson();

            JObject jsontelemetry = JObject.Parse(packetJson);

            telemetry.Level = (int)jsontelemetry["ultrasound"];
            telemetry.Battery = (int)jsontelemetry["batteryVoltage_mV"];
            telemetry.DeviceId = ttn.dev_id;
            telemetry.Timestamp = timestamp;


            //if (!DecodeRawData(ttn.payload_raw))
            //{
            //    log.Info($"Invalid Raw Data: {ttn.payload_raw}");
            //    return req.CreateResponse(HttpStatusCode.BadRequest);
            //}

            if (telemetry.Level < 0 || telemetry.Level > ResonableLevelMax)
            {
                log.Info($"Level data invalid. {telemetry.Level} > {ResonableLevelMax}");
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            storageAccount = CloudStorageAccount.Parse(storageAcct);
            queueClient = storageAccount.CreateCloudQueueClient();
            telemetryQueue = queueClient.GetQueueReference(telemetryQueueName);

            try
            {
                string json = JsonConvert.SerializeObject(telemetry);
                telemetryQueue.AddMessage(new CloudQueueMessage(json));

                log.Info(json);
            }
            catch (Exception ex)
            {
                log.Info($"Problem queuing telemetry: {ex.Message}");
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            return req.CreateResponse(HttpStatusCode.OK);
        }

        public static bool DecodeRawData(string base64EncodedData)
        {
            //Data Payload
            //Hexadecimal in format AAAABBBBCC where
            //AAAA Static application identifier
            //BBBB Tank level in centimetres, 16 bits signed integer
            //CC Internal battery level in 1 / 10 volts, 8 bits unsigned integer

            //  For example, 000400d525 should be interpreted as
            //0004 Application identifier, which does not change
            //00d5 Tank level. Converting to decimal, this is 213 cm
            //25 Internal battery. Converting to decimal, this is 3.7V

            var base64EncodedBytes = System.Convert.FromBase64String(base64EncodedData);
            var d = System.Text.Encoding.UTF7.GetString(base64EncodedBytes);

            if (d.Length != 5)
            {
                return false;
            }
            else
            {
                telemetry.Level = (byte)(d[2] << (8));
                telemetry.Level += (byte)d[3];

                telemetry.Battery = (byte)d[4];
            }
            return true;
        }

        public static bool ValidTtnApplicationId(string appId)
        {
            bool found = false;
            string[] appIds = ttnAppIDs.Split(',');
            foreach (string id in appIds)
            {
                if (appId.Trim() == id.Trim())
                {
                    found = true;
                    break;
                }
            }
            return found;
        }
    }
}
