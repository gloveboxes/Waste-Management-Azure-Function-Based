using System;
using Microsoft.WindowsAzure.Storage.Table;

namespace WasteManagement.Models
{
    public class SensorEntity : TableEntity
    {
        public string Location { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string PhoneNumber { get; set; }
        public int Level { get; set; }
    }
}