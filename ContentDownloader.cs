using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace ConsoleWebDownload
{
    class ContentDownloader
    {
        private const string ERR_STATUS = "Status.ERROR";
        private const string STATUS_OK = "Status.OK";
        private const double ERR_TIME = -1;

        private string[] _resourceTypes = new string[] { ".css", ".js" };

        public Stopwatch Watch { get; private set; }
        public string Content { get; private set; }
        public string Status { get; private set; }
        public double DownloadTime { get; private set; }
        public string Uri { get; private set; }
        public double FirstByteDownload { get; private set; }
        public double AdditionalResourcesDownloadTime { get; set; }
        public int Latency { get; private set; }
        public List<Tuple<string, string>> Errors { get; private set; }

        public ContentDownloader(string uri, int latency)
        {
            this.Watch = new Stopwatch();
            this.Uri = uri;
            this.Latency = latency;
            this.Errors = new List<Tuple<string, string>>();
            try
            {
                this.DownloadContent(this.Uri, this.Latency);
            }
            catch (WebException ex)
            {
                this.Status = ERR_STATUS;
                this.Content = "";
                this.Errors.Add(Tuple.Create(this.Uri, ex.Message));
            }
        }

        private void DownloadContent(string url, int latencyAllowed)
        {
            var request = InitializeTheRequest(url);
            double responseTime = GetResponseTime(request);
            if (responseTime <= latencyAllowed && responseTime != -1)
            {
                request = InitializeTheRequest(url);
                GetDataOnSuccessfulResponse(request);

                List<string> additionalResourses = new List<string>();
                CollectResourcesUris(additionalResourses);
                this.AdditionalResourcesDownloadTime = GetAddtionalResourseDownloadTime(additionalResourses);
            }
            else
            {
                throw new WebException("The response time is too big/missing");
            }
        }

        private double GetAddtionalResourseDownloadTime(List<string> additionalResourses)
        {
            double downloadTime = 0;
            Parallel.ForEach(additionalResourses, (url) =>
            {
                var time = 0;
                bool IsAbsolute = url.StartsWith("http://") || url.StartsWith("https://") || url.StartsWith("//");
                if (!IsAbsolute)
                {
                    url = this.Uri + "/" + url;
                }
                var req = InitializeTheRequest(url);
                try
                {
                    downloadTime += MeasureSingleResourceDownloadTime(req, time);
                }
                catch (WebException ex)
                {
                    this.Errors.Add(Tuple.Create(this.Uri, req.RequestUri + "-" + ex.Message));
                }

            });
            return downloadTime;
        }

        private void CollectResourcesUris(List<string> additionalResourses)
        {
            foreach (string contentType in _resourceTypes)
            {
                GetAdditionalResources(this.Content, contentType, additionalResourses);
            }
        }

        private double MeasureSingleResourceDownloadTime(WebRequest request, double downloadTime)
        {
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    throw new WebException("Trying to acquire a forbidden resource");
                }
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                    {
                        this.Watch.Reset();
                        this.Watch.Start();
                        var content = reader.ReadToEnd();
                        this.Watch.Stop();
                        downloadTime = downloadTime + this.Watch.ElapsedMilliseconds;
                    }
                }
            }

            return downloadTime;
        }

        private WebRequest InitializeTheRequest(string url)
        {
            if (url.StartsWith("//"))
            {
                url = "http:" + url;
            }
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.Proxy = null;
            return request;
        }

        private void GetDataOnSuccessfulResponse(WebRequest request)
        {
            try
            {
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                        {
                            int firstByte = GetTheFirstByte(reader);
                            GetContentDownloadingTime(reader, firstByte);
                        }
                    }
                }
                this.Status = STATUS_OK;
                this.DownloadTime = this.Watch.ElapsedMilliseconds;
            }
            catch (WebException ex)
            {
                this.Errors.Add(Tuple.Create(this.Uri, ex.Message));
            }
        }

        private void GetContentDownloadingTime(StreamReader reader, int firstByte)
        {
            this.Watch.Reset();
            this.Watch.Start();
            SaveContent(reader);
            this.Watch.Stop();
            this.Content = Convert.ToChar(firstByte) + this.Content;
        }

        private void SaveContent(StreamReader reader)
        {
            this.Content = reader.ReadToEnd();
        }

        private int GetTheFirstByte(StreamReader reader)
        {
            this.Watch.Reset();
            this.Watch.Start();
            int firstByte = reader.Read();
            this.Watch.Stop();
            this.FirstByteDownload = this.Watch.ElapsedMilliseconds;
            return firstByte;
        }

        private double GetResponseTime(WebRequest request)
        {
            this.Watch.Reset();
            try
            {
                this.Watch.Start();
                using (var resp = (HttpWebResponse)request.GetResponse()) { }
                this.Watch.Stop();
                return Watch.ElapsedMilliseconds;
            }
            catch (WebException ex)
            {
                this.Errors.Add(Tuple.Create(this.Uri, ex.Message));
                return -1;
            }
        }

        private void GetAdditionalResources(string content, string type, List<string> result)
        {
            int currentIndex = 0;
            int lastIndex = 0;
            while (content.IndexOf(type, currentIndex) > -1)
            {
                lastIndex = content.IndexOf(type, currentIndex);
                char endSymbol = content[lastIndex + type.Length];
                var length = content.FirstIndexBackwards(endSymbol, lastIndex);
                var uri = content.Substring(lastIndex - length, length);
                currentIndex = lastIndex + 3;
                result.Add(uri + type);
            }
        }
    }
}
