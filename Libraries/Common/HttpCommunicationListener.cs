using System;
using System.Fabric;
using System.Fabric.Description;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Services.Communication.Runtime;

namespace SFAuction.Svc {
   public sealed class HttpCommunicationListener : ICommunicationListener {
      public readonly String PublishedUri;
      private readonly HttpListener m_httpListener = new HttpListener();
      private readonly Func<HttpListenerContext, CancellationToken, Task> m_processRequest;
      private readonly CancellationTokenSource m_processRequestsCancellation = new CancellationTokenSource();

      // Url Prefix Strings: https://msdn.microsoft.com/en-us/library/aa364698(v=vs.85).aspx
      public HttpCommunicationListener(String uriPrefix, Func<HttpListenerContext, CancellationToken, Task> processRequest) {
         m_processRequest = processRequest;
         PublishedUri = uriPrefix.Replace("+", FabricRuntime.GetNodeContext().IPAddressOrFQDN);
         m_httpListener.Prefixes.Add(uriPrefix);
      }

      public void Abort() {
         m_processRequestsCancellation.Cancel(); m_httpListener.Abort();
      }

      public Task CloseAsync(CancellationToken cancellationToken) {
         m_processRequestsCancellation.Cancel();
         m_httpListener.Close(); return Task.FromResult(true);//Task.CompletedTask;
      }
      public Task<string> OpenAsync(CancellationToken cancellationToken) {
         m_httpListener.Start();
         var noWarning = ProcessRequestsAsync(m_processRequestsCancellation.Token);
         return Task.FromResult(PublishedUri);
      }
      private async Task ProcessRequestsAsync(CancellationToken processRequests) {
         while (!processRequests.IsCancellationRequested) {
            HttpListenerContext request = await m_httpListener.GetContextAsync();

            // The ContinueWith forces rethrowing the exception if the task fails.
            var noWarning = m_processRequest(request, m_processRequestsCancellation.Token)
               .ContinueWith(async t => await t /* Rethrow unhandled exception */, TaskContinuationOptions.OnlyOnFaulted);
         }
      }
   }
}
