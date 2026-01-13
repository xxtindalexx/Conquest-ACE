using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.IO;
using System.Threading.Tasks;
using System.Text.Json;
using ACE.Server.Managers;
using log4net;

namespace ACE.Server.Network
{
    public class ISPInfo
    {
        public string ASN { get; set; }
        public string Provider { get; set; }
        public string Continent { get; set; }
        public string Country { get; set; }
        public string IsoCode { get; set; }
        public string Region { get; set; }
        public string RegionCode { get; set; }
        public string City { get; set; }
        public float Latitude { get; set; }
        public float Longitude { get; set; }
        public string Proxy { get; set; }
        public string Type { get; set; }

        public override string ToString()
        {
            return $"ASN = {ASN}, Provider = {Provider}, Continent = {Continent}, Country = {Country}, IsoCode = {IsoCode}, Region = {Region}, RegionCode = {RegionCode}, City = {City}, Latitude = {Latitude}, Longitude = {Longitude}, Proxy = {Proxy}, Type = {Type}";
        }
    }

    public static class VPNDetection
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static string ApiKey { get; set; } = PropertyManager.GetString("proxycheck_api_key");

        public static async Task<ISPInfo> CheckVPN(string ip)
        {
            //Console.WriteLine("In VPNDetection.CheckVPN");
            if (string.IsNullOrEmpty(ip) || ip.Equals("127.0.0.1"))
            {
                return null;
            }

            var url = $"https://proxycheck.io/v2/{ip}?vpn=1&asn=1";
            if (!string.IsNullOrWhiteSpace(ApiKey))
                url += "&key=" + ApiKey;
            var req = WebRequest.Create(url);
            var task = req.GetResponseAsync();
            if (!(await Task.WhenAny(task, Task.Delay(3000)) == task))
            {
                log.Warn($"VPNDetection.CheckVPN task timed out for ip = {ip}");
                return null; //timed out
            }
            var resp = task.Result;
            using (var stream = resp.GetResponseStream())
            {
                using (var sr = new StreamReader(stream))
                {
                    var data = sr.ReadToEnd();
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                    };

                    var doc = JsonDocument.Parse(data);
                    var root = doc.RootElement;

                    // Get the IP address property from the root object
                    if (!root.TryGetProperty(ip, out var ipData))
                    {
                        log.Warn($"VPNDetection.CheckVPN: IP property '{ip}' not found in response");
                        return null;
                    }
                    var ispinfo = new ISPInfo()
                    {
                        ASN = GetStringProperty(ipData, "asn"),
                        Provider = GetStringProperty(ipData, "provider"),
                        City = GetStringProperty(ipData, "city"),
                        Continent = GetStringProperty(ipData, "continent"),
                        Country = GetStringProperty(ipData, "country"),
                        IsoCode = GetStringProperty(ipData, "isocode"),
                        Latitude = GetFloatProperty(ipData, "latitude"),
                        Longitude = GetFloatProperty(ipData, "longitude"),
                        Proxy = GetStringProperty(ipData, "proxy"),
                        Region = GetStringProperty(ipData, "region"),
                        RegionCode = GetStringProperty(ipData, "regioncode"),
                        Type = GetStringProperty(ipData, "type")
                    };

                    if (!string.IsNullOrEmpty(ispinfo.Proxy) && ispinfo.Proxy.ToLower().Equals("yes"))
                    {
                        log.Debug($"VPN detected for ip = {ip} with ISPInfo = {ispinfo.ToString()}");
                    }
                    //Console.WriteLine($"VPNDetection.CheckVPN returning ISPInfo = {ispinfo.ToString()}");
                    return ispinfo;
                }
            }
        }

        private static string GetStringProperty(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var property))
            {
                return property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString();
            }
            return null;
        }

        private static float GetFloatProperty(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var property))
            {
                if (property.ValueKind == JsonValueKind.Number)
                    return property.GetSingle();
                if (property.ValueKind == JsonValueKind.String && float.TryParse(property.GetString(), out var result))
                    return result;
            }
            return 0f;
        }
    }
}
