// Resolve service from not-C#: https://msdn.microsoft.com/en-us/library/azure/dn707638.aspx
using System;
using System.Collections.Generic;
using System.Fabric;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using Richter.Utilities;
using SFAuction.Common;
using SFAuction.JsonRpc;
using SFAuction.Svc.Auction;

namespace SFAuction.Svc.ApiGateway {
   // My Reliable Service is derived from a Service Fabric base class
   internal sealed class ApiGatewaySvc : StatelessService {
      public ApiGatewaySvc(StatelessServiceContext serviceContext) : base(serviceContext) { }

      #region private fields
      private const String c_RestEndpoint = "RestEndpoint";
      private static readonly JavaScriptSerializer s_jsSerializer = new JavaScriptSerializer();
      private static readonly Uri AuctionServiceNameUri = new Uri(@"fabric:/SFAuction/AuctionSvcInstance");
      private static readonly HttpClient s_httpClient = new HttpClient();
      private static readonly PartitionEndpointsResolver m_partitionEndpointResolver =
         new PartitionEndpointsResolver();
      private readonly ServiceOperations m_operations = new ServiceOperations(m_partitionEndpointResolver, AuctionServiceNameUri);
      private String m_selfUrl;
      private Boolean firstTime = true;
      static ApiGatewaySvc() { s_jsSerializer.RegisterConverters(new[] { new DataTypeJsonConverter() }); }
      #endregion

      // Here I tell SF what endpoints I want my service to listen to.
      protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners() {
         return new[] { new ServiceInstanceListener(CreateInputListener, c_RestEndpoint) };
      }

      // SF calls this when I need to open my endpoint; the returned endpoint is published in the naming service
      private ICommunicationListener CreateInputListener(StatelessServiceContext context) {
         var ep = context.CodePackageActivationContext.GetEndpoint(c_RestEndpoint);
         var listener = new HttpCommunicationListener($"{ep.Protocol}://+:{ep.Port}/Rest/", ProcessInputRequest);
         m_selfUrl = listener.PublishedUri;
         return listener;
      }

      // My endpoint listener calls this method for each client request
      private async Task ProcessInputRequest(HttpListenerContext context, CancellationToken cancelRequest) {
         String output = null;
         if (firstTime) { firstTime = false; output = await PrimeAsync(m_selfUrl, cancelRequest); }
         try {
            HttpListenerRequest request = context.Request;
            foreach (String key in request.QueryString) {
               String queryValue = request.QueryString[key];
               switch (key.ToLowerInvariant()) {
                  case "prime":
                     output = await PrimeAsync(m_selfUrl, cancelRequest);
                     break;
                  case "jsonrpc":   // Process request to get response:
                     JsonRpcRequest jsonRequest = JsonRpcRequest.Parse(queryValue);
                     JsonRpcResponse jsonResponse = await jsonRequest.InvokeAsync(s_jsSerializer, m_operations, cancelRequest);
                     output = jsonResponse.ToString();
                     break;
               }
            }
         }
         catch (Exception ex) { output = ex.ToString(); }
         // Write response to client:
         using (var response = context.Response) {
            if (output != null) {
               response.AppendHeader("Access-Control-Allow-Origin", null);
               Byte[] outBytes = Encoding.UTF8.GetBytes(output);
               response.OutputStream.Write(outBytes, 0, outBytes.Length);
            }
         }
      }

      // Override this method to do other processing that is not in response to a listener
      protected override async Task RunAsync(CancellationToken cancellationToken) {
         while (!cancellationToken.IsCancellationRequested)
            await Task.Delay(TimeSpan.FromSeconds(10)); // Put breakpoint here to break into debugger
      }


      #region Other internal helper methods
      private async Task<String> PrimeAsync(String selfUrl, CancellationToken cancellationToken) {
         const String imageUrl = "images/";
         DateTime now = DateTime.UtcNow;
         var proxy = new ServiceOperations(m_partitionEndpointResolver, new Uri(@"fabric:/SFAuction/AuctionSvcInstance"));
         const String Jeff = "Jeff@Microsoft.com", Chacko = "Chacko@Microsoft.com";

         try {
            await proxy.CreateUserAsync(Jeff, cancellationToken);
         }
         catch (Exception ex) { ex.GetType(); }

         try {
            await proxy.CreateItemAsync(Jeff,
               "Microsoft XBox One",
               imageUrl + "xbox-one.png",
               now.AddDays(5),
               259.00M, cancellationToken);
         }
         catch (Exception ex) { ex.GetType(); }

         try {
            await proxy.CreateItemAsync(Jeff,
                  "Cushion cut diamond engagement ring set in platinum",
                  imageUrl + "diamond-ring.jpg",
                   now.AddDays(5),
                  1000.12M, cancellationToken);
         }
         catch (Exception ex) { ex.GetType(); }

         try {
            await proxy.CreateUserAsync(Chacko, cancellationToken);
         }
         catch (Exception ex) { ex.GetType(); }

         try {
            await proxy.CreateItemAsync(Chacko,
                  "Child bicycle with training wheels and basket - PINK",
                  imageUrl + "child-bicycle.jpg",
                   now.AddDays(6), // Expired
                  45.54M, cancellationToken);
         }
         catch (Exception ex) { ex.GetType(); }

         try {
            await proxy.CreateItemAsync(Chacko,
               "Dining Table Set with 6 chairs - Rustic Wood",
               imageUrl + "rustic-dining-sets.jpg",
                now.AddDays(4), // Expired
               400.34M, cancellationToken);
         }
         catch (Exception ex) { ex.GetType(); }

         try {
            await proxy.CreateItemAsync(Chacko,
               "Microsoft Lumia 950 XL Dual SIM - 32 GB ",
               imageUrl + "Lumia-950-XL-hero-jpg.jpg",
               now.AddDays(7),
               500.00M, cancellationToken);
         }
         catch (Exception ex) { ex.GetType(); }

         try {
            await proxy.CreateItemAsync(Chacko,
       "Microsoft Band 2 - Medium ",
       imageUrl + "band-2.jpg",
           now.AddHours(5),
       200.00M, cancellationToken);
         }
         catch (Exception ex) { ex.GetType(); }

         try {
            await proxy.CreateItemAsync(Chacko,
               "Contoso All Expense paid Trip to Hawaii for 2 ",
               imageUrl + "hawaii.jpg",
                now.AddDays(10),
               1500.00M, cancellationToken);
         }
         catch (Exception ex) { ex.GetType(); }

         try {
            await proxy.CreateItemAsync(Chacko,
                  "Microsoft Surface Pro 3 256GB SSD Intel I5 1.9GHZ",
                  imageUrl + "MicrosoftSurface.jpg",
                   now.AddDays(10),
                  973.00M, cancellationToken);
         }
         catch (Exception ex) { ex.GetType(); }

         retry:
         try {
            var items = await proxy.GetAuctionItemsAsync(cancellationToken);
         }
         catch (Exception ex) { ex.GetType(); goto retry; }

         return "Primed.";
      }
      #endregion
   }
}
