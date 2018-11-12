using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Stock.Trading.HttpClients
{
    public abstract class HttpClientBase : HttpClient
    {
        protected HttpClientBase(string url)
        {
            BaseAddress = new Uri(url);

            DefaultRequestHeaders.Accept.Clear();
            DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<T> GetAsync<T>(string url)
        {
            var response = await GetAsync(url);

            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();

            return JsonConvert.DeserializeObject<T>(content);
        }

        public async Task<T> PostJsonAsync<T>(string url, object obj)
        {
            var response = await PostJsonAsync(url, obj);

            string res = await response.Content.ReadAsStringAsync();

            return JsonConvert.DeserializeObject<T>(res);
        }

        public async Task<HttpResponseMessage> PostJsonAsync(string url, object obj)
        {
            string json = JsonConvert.SerializeObject(obj);

            var response = await PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));

            response.EnsureSuccessStatusCode();

            return response;
        }
    }
}