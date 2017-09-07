using Microsoft.WindowsAzure.Storage.Table;

namespace WasteManagement.Models
{
    //    WeatherId;Temperature;Pressure;Humidity;Precipitation;Wind;Cloud;Weather
    //    Sydney, AU;16.45;1014;29;0;10.3;0;clear sky

    public class WeatherEntity : TableEntity
    {
        public double Temperature { get; set; }
        public double Pressure { get; set; }
        public int Humidity { get; set; }
        public double Precipitation { get; set; }
        public double WindSpeed { get; set; }
        public int Clouds { get; set; }
        public string Weather { get; set; }
    }
}
