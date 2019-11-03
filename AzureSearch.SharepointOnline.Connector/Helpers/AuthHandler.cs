//THIS CODE IS PROVIDED AS IS WITHOUT WARRANTY OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.

using Microsoft.Graph;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace AzureSearch.SharePointConnector
{
    // This class allows an implementation of IAuthenticationProvider to be inserted into the DelegatingHandler
    // pipeline of an HttpClient instance.  In future versions of GraphSDK, many cross-cutting concerns will
    // be implemented as DelegatingHandlers.  This AuthHandler will come in the box.
    public class AuthHandler : DelegatingHandler
    {
        private IAuthenticationProvider _authenticationProvider;

        public AuthHandler(IAuthenticationProvider authenticationProvider, HttpMessageHandler innerHandler)
        {
            InnerHandler = innerHandler;
            _authenticationProvider = authenticationProvider;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await _authenticationProvider.AuthenticateRequestAsync(request);
            return await base.SendAsync(request, cancellationToken);
        }
    }
}