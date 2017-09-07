using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WasteManagement.Models;

namespace WasteManagement.SmsAlerter
{
    public class SmsManager
    {
        string telstraSmsApiConsumerKey;
        string telstraSmsConsumerSecret;
        TelstraSms telstraSms;

        TimeSpan ts = TimeSpan.MaxValue;

        List<TelemetryEntity> smsList = new List<TelemetryEntity>();
 

        public SmsManager(string telstraSmsApiConsumerKey, string telstraSmsConsumerSecret)
        {
            this.telstraSmsApiConsumerKey = telstraSmsApiConsumerKey;
            this.telstraSmsConsumerSecret = telstraSmsConsumerSecret;

            telstraSms = new TelstraSms(telstraSmsApiConsumerKey, telstraSmsConsumerSecret);
        }

        public void ProcessSmsEntity(string data)
        {
            var alert = JsonConvert.DeserializeObject<TelemetryEntity>(data, new JsonSerializerSettings() { ContractResolver = new CamelCasePropertyNamesContractResolver() });
            smsList.Add(alert);
        }

        public bool SendAlerts(TraceWriter log)
        {
            StringBuilder sb = new StringBuilder();

            if (smsList.Count == 0) { return true; }

            var phones = smsList.Select(x => x.PhoneNumber).Distinct();
            foreach (var phone in phones)
            {
                var queryResult = (from l in smsList
                                   select new TelemetryEntity()
                                   {
                                       DeviceId = l.DeviceId,
                                       PhoneNumber = l.PhoneNumber,
                                       Level = l.Level,
                                       Location = l.Location
                                   }).Where(p => p.PhoneNumber == phone).GroupBy(x => x.DeviceId).Select(z => z.OrderByDescending(i => i.Level).First()).OrderByDescending(l => l.Level).ToList();

                log.Info("============Begin Output Data============");

                foreach (var item in queryResult)
                {

                    log.Info($"BinId: {item.DeviceId}, Location: {item.Location}, Level: {item.Level}, Phone: {item.PhoneNumber}");
                }

                log.Info("============End Output Data============");

                sb.Clear();

                foreach (var item in queryResult)
                {
                    if (sb.Length + item.Location.Length + 6 > 160)
                    {
                        sb.Remove(sb.Length - 2, 2); // remove the last comma and space
                        log.Info(sb.ToString());

                        telstraSms.SendSms(phone, sb.ToString());

                        sb.Clear();
                    }

                    sb.Append(item.Location);
                    sb.Append("(" + item.Level + ")" + ", ");
                }

                sb.Remove(sb.Length - 2, 2); // remove the last comma and space

                log.Info(sb.ToString());
                telstraSms.SendSms(phone, sb.ToString());
            }

            log.Info("");

            return true;
        }
    }
}
