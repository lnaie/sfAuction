using System;
using System.Collections.Generic;
using System.Fabric;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using Richter.Utilities;
using SFAuction.Common;
using SFAuction.JsonRpc;

namespace SFAuction.Svc.Auction {
   /// <summary>
   /// The FabricRuntime creates an instance of this class for each service type instance.
   /// </summary>
   internal sealed class AuctionSvc : StatefulService {
      private const String c_replicaEndpoint = "ReplicaEndpoint";
      private static readonly JavaScriptSerializer s_jsSerializer = new JavaScriptSerializer();
      private readonly String m_nodeIP = FabricRuntime.GetNodeContext().IPAddressOrFQDN;
      private PartitionOperations m_operations;
      static AuctionSvc() { s_jsSerializer.RegisterConverters(new[] { new DataTypeJsonConverter() }); }
      public AuctionSvc(StatefulServiceContext context) : base(context) { }

      /// <summary>
      /// This is the main entry point for your service's partition replica. 
      /// RunAsync executes when the primary replica for this partition has write status.
      /// </summary>
      /// <param name="cancelServicePartitionReplica">Canceled when Service Fabric terminates this partition's replica.</param>
      protected override async Task RunAsync(CancellationToken cancellationToken) {
         while (!cancellationToken.IsCancellationRequested)
            await Task.Delay(TimeSpan.FromSeconds(10)); // Put breakpoint here to break into debugger
      }

      /// <summary>
      /// Optional override to create listeners (like tcp, http) for this service replica.
      /// </summary>
      /// <returns>The collection of listeners.</returns>
      protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners() {
         return new[] { new ServiceReplicaListener(CreateInternalListener, c_replicaEndpoint, false) };
      }

      private ICommunicationListener CreateInternalListener(StatefulServiceContext context) {
         var erd = context.GetEndpointResourceDescription(c_replicaEndpoint);
         var uriPrefix = $"{erd.Protocol}://+:{erd.Port}/" + context.CalcUriSuffix();
         return new HttpCommunicationListener(uriPrefix, ProcessRequest);
      }

      private async Task ProcessRequest(HttpListenerContext context, CancellationToken cancelRequest) {
         if (m_operations == null)
            Interlocked.CompareExchange(ref m_operations, await PartitionOperations.CreateAsync(StateManager), null);

         String output = null;
         try {
            HttpListenerRequest request = context.Request;
            if (request.HttpMethod == "GET") {
               // Read request from client:
               String jsonRpc = context.Request.QueryString["jsonrpc"];

               // Process request to get response:
               JsonRpcRequest jsonRequest = JsonRpcRequest.Parse(jsonRpc);
               JsonRpcResponse jsonResponse = await jsonRequest.InvokeAsync(s_jsSerializer, m_operations, cancelRequest);
               output = jsonResponse.ToString();
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
   }
}


#if false
Resources:
   User
      Create a User:       POST http://localhost/users/
      Get a User:          GET  http://localhost/users/userEmail
      Create Item to Sell  POST http://localhost/users/sellerEmail
      Bid on Item:         POST http://localhost/users/bidderEmail
      Get items selling    GET  http://localhost/users/userEmail?Items=Selling
      Get items bidding    GET  http://localhost/users/userEmail?Items=Bidding

   Item
      Get item             GET http://localhost/items/ItemId
      Get unexpired items  GET http://localhost/items
#endif