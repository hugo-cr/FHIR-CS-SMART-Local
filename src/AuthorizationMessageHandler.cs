using System;

namespace smart_local
{

    public class AuthorizationMessageHandler: HttpClientHandler
    {
        public System.Net.Http.Headers.AuthenticationHeaderValue Authorization { get; set; }

        protected async override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Authorization != null) request.Headers.Authorization = Authorization;

            return await base.SendAsync(request, cancellationToken);
        }
    }
}