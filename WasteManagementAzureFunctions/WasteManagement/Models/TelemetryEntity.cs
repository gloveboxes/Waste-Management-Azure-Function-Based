using Newtonsoft.Json;
using System;

namespace WasteManagement.Models
{
    public class TelemetryEntity
    {
        public string DeviceId { get; set; } = string.Empty;
        public int Level { get; set; }
        public int Battery { get; set; }
        public int MsgId { get; set; }
        public int Schema { get; set; } = 1;
        public DateTime Timestamp { get; set; }
        public string Location { get; set; } = string.Empty;
        public string PhoneNumber { get; set; }
        public double Temperature { get; set; }
        public double Pressure { get; set; }
        public int Humidity { get; set; }
        public double Precipitation { get; set; }
        public double WindSpeed { get; set; }
        public int Clouds { get; set; }
        public string Weather { get; set; } = string.Empty;

        public string ToJson(string deviceId, int level)
        {
            this.DeviceId = deviceId;
            this.Level = level;
            return JsonConvert.SerializeObject(this);
        }
    }
}