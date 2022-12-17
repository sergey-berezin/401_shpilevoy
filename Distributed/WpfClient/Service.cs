using Newtonsoft.Json;
using Polly;
using Polly.Retry;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Contracts;
using System.Reflection.Metadata;
using Newtonsoft.Json.Linq;
using Microsoft.AspNetCore.Mvc;

namespace WpfClient
{
    public class Service
    {
        private Random jitterer = new();
        private AsyncRetryPolicy retryPolicy;
        private static readonly string serverAddres = "https://localhost:7091/api/ArcFace/";

        public Service()
        {
            retryPolicy = Policy.Handle<HttpRequestException>().WaitAndRetryAsync(
                5, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))  
                      + TimeSpan.FromMilliseconds(jitterer.Next(0, 1000)));
        }

        public async Task<int[]?> PostImages(List<ImageMinInfo> imgs, CancellationToken token)
        {
            HttpResponseMessage response = new();
            try
            {
                await retryPolicy.ExecuteAsync(async () => {
                    using var client = new HttpClient()
                    {
                        BaseAddress = new Uri(serverAddres)
                    };

                    var content = CreateImageContent(imgs);

                    response = await client.PostAsync("images", content, token);

                    response.EnsureSuccessStatusCode();
                });
            }
            catch
            {
                return null;
            }

            var strRes = await response.Content.ReadAsStringAsync(token);
            var res = JsonConvert.DeserializeObject<int[]>(strRes);
            return res;
        }


        public async Task<List<ImageInfo>?> GetAllImages()
        {
            HttpResponseMessage response = new();
            try
            {
                await retryPolicy.ExecuteAsync(async () => {
                    using var client = new HttpClient()
                    {
                        BaseAddress = new Uri(serverAddres)
                    };

                    response = await client.GetAsync("images");

                    response.EnsureSuccessStatusCode();
                });
            }
            catch
            {
                return null;
            }

            var strRes = await response.Content.ReadAsStringAsync();
            var res = JsonConvert.DeserializeObject<List<ImageInfo>>(strRes);
            return res;
        }


        public async Task<bool> DeleteAllImages()
        {
            HttpResponseMessage response = new();
            try
            {
                await retryPolicy.ExecuteAsync(async () => {
                    using var client = new HttpClient()
                    {
                        BaseAddress = new Uri(serverAddres)
                    };

                    response = await client.DeleteAsync("images");

                    response.EnsureSuccessStatusCode();
                });
                return true;
            }
            catch
            {
                return false;
            }
           
        }

        public async Task<bool> DeleteImage(int id)
        {
            HttpResponseMessage response = new();
            try
            {
                await retryPolicy.ExecuteAsync(async () => {
                    using var client = new HttpClient()
                    {
                        BaseAddress = new Uri(serverAddres)
                    };

                    response = await client.DeleteAsync($"images/id?id={id}");

                    response.EnsureSuccessStatusCode();
                });
                return true;
            }
            catch
            {
                return false;
            }
            
        }


        public async Task<Metrics?> GetCompare(int id1, int id2)
        {
            HttpResponseMessage response = new();
            try
            {
                await retryPolicy.ExecuteAsync(async () => {
                    using var client = new HttpClient()
                    {
                        BaseAddress = new Uri(serverAddres)
                    };

                    response = await client.GetAsync($"compare?id1={id1}&id2={id2}");

                    response.EnsureSuccessStatusCode();
                });

                var strRes = await response.Content.ReadAsStringAsync();
                var res = JsonConvert.DeserializeObject<Metrics>(strRes);
                return res;
            }
            catch
            {
                return null;
            }
        }



        //Private methods

        private static HttpContent CreateImageContent(List<ImageMinInfo> imgs)
        {
            var content = new StringContent(JsonConvert.SerializeObject(imgs));
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return content;
        }

    }
}