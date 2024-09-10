using GuacamoleExporter.ExternalServices.GuacamoleApiService.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OtpNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace GuacamoleExporter.ExternalServices.GuacamoleApiService
{
    public class GuacamoleApiService
    {
        private string _guacamoleHostname;
        private string _guacamoleUsername;
        private string _guacamolePassword;
        private string _guacamoleDatasource;

        private string? _token;
        private string? _totpKey;
        private string _totpFilePath;

        public GuacamoleApiService(string guacamoleHostname, string guacamoleUsername, string guacamolePassword, string guacamoleDatasource = "mysql")
        {
            _guacamoleHostname = guacamoleHostname;
            _guacamoleUsername = guacamoleUsername;
            _guacamolePassword = guacamolePassword;
            _guacamoleDatasource = guacamoleDatasource;

            if (_guacamoleHostname.EndsWith("/"))
                _guacamoleHostname = _guacamoleHostname.Substring(_guacamoleHostname.Length - 1);
            if (!_guacamoleHostname.EndsWith("/guacamole"))
                _guacamoleHostname += "/guacamole";

            if (!Directory.Exists("/data"))
                Directory.CreateDirectory("/data");

            _totpFilePath = Path.Combine("/data", "totp.txt");

            if (File.Exists(_totpFilePath))
                _totpKey = File.ReadAllText(_totpFilePath);
        }

        public async Task<bool> GetGuacIsUp()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_guacamoleHostname}/api/patches");

            var response = await Request(request);

            return response.IsSuccessStatusCode;
        }

        public async Task<int> GetCountOfUser()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_guacamoleHostname}/api/session/data/mysql/users");

            var response = await Request(request);

            response.EnsureSuccessStatusCode();

            string str = await response.Content.ReadAsStringAsync();

            JObject obj = JObject.Parse(str);

            return obj.Count;
        }

        public async Task<List<ActiveConnection>> GetActiveConnection()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_guacamoleHostname}/api/session/data/mysql/activeConnections");
            var response = await Request(request);

            response.EnsureSuccessStatusCode();

            string str = await response.Content.ReadAsStringAsync();

            var result = JsonConvert.DeserializeObject<Dictionary<string, ActiveConnection>>(str)!.Select(x => x.Value).ToList();

            var detailConnectionToGet = result.Select(x => x.ConnectionIdentifier)
                .Distinct()
                .ToList();

            Task<HttpResponseMessage>[] tasks = detailConnectionToGet
                .Select(x => Request(new HttpRequestMessage(HttpMethod.Get, $"{_guacamoleHostname}/api/session/data/mysql/connections/{x}")))
                .ToArray();

            Task.WaitAll(tasks);

            foreach (var task in tasks)
            {
                var taskResult = task.Result;

                taskResult.EnsureSuccessStatusCode();

                string taskStr = await taskResult.Content.ReadAsStringAsync();

                var taskObj = JObject.Parse(taskStr);

                result.Where(x => x.ConnectionIdentifier == (string)taskObj["identifier"]!).ToList().ForEach(x =>
                {
                    x.ConnectionName = (string)taskObj["name"]!;
                    x.ConnectionProtocol = (string)taskObj["protocol"]!;
                });
            }

            return result;
        }

        private async Task<HttpResponseMessage> Request(HttpRequestMessage request, bool auth = true)
        {
            HttpClient client = new HttpClient();

            if(auth)
            {
                if (_token == null)
                    _token = await GenerateToken();

                UriBuilder uriBuilder = new UriBuilder(request.RequestUri!);
                var query = HttpUtility.ParseQueryString(uriBuilder.Query);
                query["token"] = _token;
                uriBuilder.Query = query.ToString();
                request.RequestUri = uriBuilder.Uri;
            }

            HttpResponseMessage response = await client.SendAsync(request);

            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                _token = await GenerateToken();
                response = await Request(request, auth);
            }

            return response;
        }


        private async Task<string> GenerateToken()
        {
            HttpClient client = new HttpClient();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, $"{_guacamoleHostname}/api/tokens");
            List<KeyValuePair<string, string>> collection = new List<KeyValuePair<string, string>>
            {
                new("username", _guacamoleUsername),
                new("password", _guacamolePassword)
            };
            if (_totpKey != null)
            {
                Totp totp = new Totp(Base32Encoding.ToBytes(_totpKey));

                collection.Add(new("guac-totp", totp.ComputeTotp()));
            }

            request.Content = new FormUrlEncodedContent(collection);
            var response = await client.SendAsync(request);

            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden && collection.Where(x => x.Key == "guac-totp").Count() == 0)
            {
                var _resultStr = await response.Content.ReadAsStringAsync();

                JObject _resultObj = JObject.Parse(_resultStr);

                if (_resultObj["translatableMessage"]!.Value<string>("key") == "TOTP.INFO_ENROLL_REQUIRED")
                {
                    string totpsecret = _resultObj["expected"]![0]!.Value<string>("secret")!;

                    Totp otp = new Totp(Base32Encoding.ToBytes(totpsecret));

                    collection.Add(new("guac-totp", otp.ComputeTotp()));

                    HttpRequestMessage request2 = new HttpRequestMessage(HttpMethod.Post, $"{_guacamoleHostname}/api/tokens");
                    request2.Content = new FormUrlEncodedContent(collection);

                    response = await client.SendAsync(request2);


                    if (!File.Exists(_totpFilePath) || File.ReadAllText(_totpFilePath) != totpsecret)
                        File.WriteAllText(_totpFilePath, totpsecret);
                }
            }

            var resultStr = await response.Content.ReadAsStringAsync();

            JObject resultObj = JObject.Parse(resultStr);

            return (string?)resultObj["authToken"] ?? throw new Exception("Auth error");
        }
    }
}
