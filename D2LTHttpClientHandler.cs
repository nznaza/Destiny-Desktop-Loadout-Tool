using Serilog;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DestinyLoadoutTool
{
    internal class D2LTHttpClientHandler : HttpClientHandler
    {
        CookieCollection PreviousKnownCookies = [];
        public D2LTHttpClientHandler() : base()
        {
            CookieContainer = new CookieContainer();
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.Remove("User-Agent");
            request.Headers.Add("User-Agent", "Destiny Desktop Loadout Powered by DotNetBungieAPI/1.0 AppId/48057 (discord: @nznaza)");
            CookieCollection requestCookies = CookieContainer.GetAllCookies();
            Log.Information($"Calling {request.Method} on {request.RequestUri}");

            Log.Debug($"__cflb = {requestCookies.FirstOrDefault(x => x.Name == "__cflb")?.Value ?? "null"}");
            foreach(Cookie? cookie in requestCookies.ExceptBy(PreviousKnownCookies.Select(e => e.Name), (x => x.Name)))
            {
                Log.Debug($"new cookie ({cookie.Name}): {cookie.Value}");
            }
            foreach (Cookie? cookie in requestCookies.IntersectBy(PreviousKnownCookies.Select(e => e.Name), (x => x.Name)))
            {
                if (PreviousKnownCookies.First(e=> e.Name == cookie.Name).Value != cookie.Value)
                Log.Debug($"cookie ({cookie.Name}) changed Value: {cookie.Value}");
            }
            PreviousKnownCookies = requestCookies;
            HttpResponseMessage result = await base.SendAsync(request, cancellationToken);
            return result;
        }
    }
}
