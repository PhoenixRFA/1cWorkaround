using Newtonsoft.Json;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace _1cWorkaround
{
    public static class Constants
    {
        public const int TENANT_ID = 1551352;
        public const string Login = "apiIntegrationUser1";
        public const string Password = "123Qwer";
        public const string ArchiveName = "data.zip";
        public const string XmlName = "example.xml";
    }

    static class Program
    {
        static async Task Main(string[] args)
        {
            ZipBuilder.Build(Constants.XmlName, Constants.ArchiveName);

            string url = $"https://1cfresh.com:443/a/ea/{Constants.TENANT_ID}/hs/dt/storage/integration/post/";

            DataTransferResponse res = await SimpleBrowser.DataTransferStart(url);

            ResponseModel job = await SimpleBrowser.DataTransferFinish(res.Url, Constants.ArchiveName);

            url = $"https://1cfresh.com:443/a/ea/{Constants.TENANT_ID}/hs/dt/storage/{job.Result.Storage}/{job.Result.Id}";

            JobResult jobResult = await SimpleBrowser.GetJobStart(url);

            if (jobResult == null)
            {
                for (int i = 0; i < 10; i++)
                {
                    Thread.Sleep(1000);
                    jobResult = await SimpleBrowser.GetJob(url);

                    if (jobResult != null) break;
                }
            }

            if (jobResult == null) return;

            //Upload Complete
        }
    }

    public static class ZipBuilder
    {
        public static void Build(string path, string archiveName)
        {
            string json =
            @"{
                ""upload"": [
                    {
                        ""file"":""data.xml"",
                        ""handler"":""enterprise_data""
                    }
                ]
            }";

            if (File.Exists(archiveName))
            {
                File.Delete(archiveName);
            }

            using (var stream = new FileStream(archiveName, FileMode.Create))
            {
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
                {
                    ZipArchiveEntry manifest = zip.CreateEntry("manifest.json");
                    using (Stream s = manifest.Open())
                    {
                        using (var writer = new StreamWriter(s))
                        {
                            writer.Write(json);
                        }
                    }

                    var xml = zip.CreateEntry("data.xml");
                    using (Stream s = xml.Open())
                    {
                        using (var fileStream = new FileStream(path, FileMode.Open))
                        {
                            fileStream.CopyTo(s);
                        }
                    }
                }
            }
        }
    }

    public static class SimpleBrowser
    {
        private static HttpClient _client;
        private static HttpClientHandler _handler;
        private const string _login = Constants.Login;
        private const string _pass = Constants.Password;

        static SimpleBrowser()
        {
            _handler = new HttpClientHandler();
            _client = new HttpClient(_handler);
        }

        public static async Task<DataTransferResponse> DataTransferStart(string url)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_login}:{_pass}")));
            request.Headers.Add("IBSession", "start");

            HttpResponseMessage response = await _client.SendAsync(request);
            bool isOk = response.IsSuccessStatusCode;

            var res = new DataTransferResponse
            {
                Url = response.Headers.Location.ToString(),
                //Cookie = response.Headers.GetValues("Set-Cookie").First()
                //httpClient set cookies on its own
            };

            return res;
        }

        public static async Task<ResponseModel> DataTransferFinish(string url, string path)
        {
            var request = new HttpRequestMessage(HttpMethod.Put, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_login}:{_pass}")));
            request.Headers.Add("IBSession", "finish");

            FileStream fileStream = File.OpenRead(path);
            var content = new MultipartFormDataContent();
            content.Add(new StreamContent(fileStream));

            request.Content = content;

            HttpResponseMessage response = await _client.SendAsync(request);
            bool isOk = response.IsSuccessStatusCode;

            string responseJson = await response.Content.ReadAsStringAsync();

            ResponseModel res = JsonConvert.DeserializeObject<ResponseModel>(responseJson);

            return res;
        }

        public static async Task<JobResult> GetJobStart(string url)
        {
            _clearSessionCookies();

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_login}:{_pass}")));
            request.Headers.Add("IBSession", "start");

            HttpResponseMessage response = await _client.SendAsync(request);
            bool isOk = response.IsSuccessStatusCode;

            string responseJson = await response.Content.ReadAsStringAsync();
            JobResponseModel resp = JsonConvert.DeserializeObject<JobResponseModel>(responseJson);

            return resp.Result?.FirstOrDefault();
        }

        public static async Task<JobResult> GetJob(string url)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_login}:{_pass}")));

            HttpResponseMessage response = await _client.SendAsync(request);
            bool isOk = response.IsSuccessStatusCode;

            string responseJson = await response.Content.ReadAsStringAsync();
            JobResponseModel resp = JsonConvert.DeserializeObject<JobResponseModel>(responseJson);

            return resp.Result?.FirstOrDefault();
        }

        private static void _clearSessionCookies()
        {
            var cookies = _handler.CookieContainer.GetCookies(new Uri($"https://1cfresh.com:443/a/ea/{Constants.TENANT_ID}/"));
            foreach (Cookie cookie in cookies)
            {
                if (cookie.Name != "ibsession") continue;

                cookie.Expired = true;
            }
        }
    }

    public class DataTransferResponse
    {
        public string Url { get; set; }
        public string Cookie { get; set; }
    }

    public class ResponseModel
    {
        public GeneralPart General { get; set; }
        public ResultPart Result { get; set; }
    }
    public class GeneralPart
    {
        public int Response { get; set; }
        public bool Error { get; set; }
        public string Message { get; set; }
    }
    public class ResultPart
    {
        public string Storage { get; set; }
        public string Id { get; set; }
    }
    public class JobResult
    {
        public string File { get; set; }
        public string Version { get; set; }
        public string Handler { get; set; }
        public int Response { get; set; }
        public bool Error { get; set; }
        public string Message { get; set; }
    }
    public class JobResponseModel
    {
        public GeneralPart General { get; set; }
        public JobResult[] Result { get; set; }
    }
}
