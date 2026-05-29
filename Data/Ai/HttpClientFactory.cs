using System;
using System.Net;
using System.Net.Http;

namespace MaestroNotes.Data.Ai
{
    public static class HttpClientFactory
    {
        private static HttpClient? CreateClient(string? proxy = null, bool bypasslocal = false)
        {
            if (!string.IsNullOrEmpty(proxy))
            {
                HttpClientHandler handler = new HttpClientHandler();
                handler.Proxy = new WebProxy(proxy, bypasslocal);
                handler.UseProxy = true;
                handler.UseDefaultCredentials = true;
                return new HttpClient(handler);
            }
            else
            {
                // Return a default HttpClient if no proxy is needed
                return new HttpClient();
            }
        }

        public static HttpClient Create(AiSettings settings)
        {
            return CreateClient(settings.Proxy, settings.BypassLocal) ?? new HttpClient();
        }
    }
}
