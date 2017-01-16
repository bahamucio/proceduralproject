using System.Diagnostics;
using System.IO;
using System.Net;

namespace ConsoleWebDownload
{
    class ContentDownloader
    {
        private const string ERR_STATUS = "Status.ERROR";
        private const string STATUS_OK = "Status.OK";
        private const string HTTP_START = "http://";

        public ContentDownloader()
        {
            this.Watch = new Stopwatch();
        }

        public string DownloadContent(string url, int latencyAllowed)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            double responseTime = GetResponseTime(request);
            
            if (responseTime <= latencyAllowed && responseTime != -1)
            {
                request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "GET";
                return url + "," + GetDataOnSuccessfulResponse(request);
            }
            else
            {
                return url + "," + "--- ms" + "," + ERR_STATUS + "," + "The responce time is too big/missing";
            }
        }

        private static string FormatUri(string url)
        {
            if (url.StartsWith(HTTP_START))
            {
                url = url.Replace(HTTP_START, "");
            }
            if (!url.StartsWith("www."))
            {
                url = "www." + url;
            }

            return url;
        }

        private string GetDataOnSuccessfulResponse(HttpWebRequest request)
        {
            string content = "";
            try
            {
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                        {
                            this.Watch.Reset();
                            this.Watch.Start();
                            content = reader.ReadToEnd();
                            this.Watch.Stop();
                        }
                    }
                }

                return Watch.ElapsedMilliseconds + " ms" + "," + STATUS_OK + "," + content;
            }
            catch (WebException ex)
            {
                return content = "--- ms" + "," + ERR_STATUS + "," + ex.Message;
            }
        }

        private double GetResponseTime(HttpWebRequest request)
        {
            this.Watch.Reset();
            try
            {
                this.Watch.Start();
                using(var resp = (HttpWebResponse)request.GetResponse()) { }
                this.Watch.Stop();
                return Watch.ElapsedMilliseconds;
            } catch (WebException)
            {
                return -1;
            }
        }

        public Stopwatch Watch { get; private set; }
    }
}
