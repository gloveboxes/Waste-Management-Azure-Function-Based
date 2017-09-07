using Newtonsoft.Json.Linq;
using System.Net;
using System.Text;

namespace WasteManagement.SmsAlerter
{
    public class TelstraSms
    {
        string consumerKey;
        string consumerSecret;
        static string token;
        bool initialsed = false;

        public TelstraSms(string telstraSmsApiConsumerKey, string telstraSmsConsumerSecret)
        {
            consumerKey = telstraSmsApiConsumerKey;
            consumerSecret = telstraSmsConsumerSecret;
        }

        private void GetAccessToken()
        {
            string url = string.Format("https://api.telstra.com/v1/oauth/token?client_id={0}&client_secret={1}&grant_type=client_credentials&scope=SMS", consumerKey, consumerSecret);

            using (var webClient = new System.Net.WebClient())
            {
                var json = webClient.DownloadString(url);
                var obj = JObject.Parse(json);
                token = obj.GetValue("access_token").ToString();
            }

            initialsed = true;
        }

        public void SendSms(string recipientNumber, string message)
        {
            if (!initialsed)
            {
                GetAccessToken();
            }

            using (var webClient = new System.Net.WebClient())
            {
                webClient.Headers.Clear();
                webClient.Headers.Add(HttpRequestHeader.ContentType, @"application/json");
                webClient.Headers.Add(HttpRequestHeader.Authorization, "Bearer " + token);

                string data = "{\"to\":\"" + recipientNumber + "\", \"body\":\"" + message + "\"}";
                var response = webClient.UploadData("https://api.telstra.com/v1/sms/messages", "POST", Encoding.Default.GetBytes(data));
                var responseString = Encoding.Default.GetString(response);
                var obj = JObject.Parse(responseString);
                //return obj.GetValue("messageId").ToString();
                // Now parse with JSON.Net
            }
        }
    }
}
