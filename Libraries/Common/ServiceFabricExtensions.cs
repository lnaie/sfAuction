using System;
using System.Collections.Generic;
using System.Fabric;
using System.Fabric.Description;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization; // System.Web.Extensions.dll
using Microsoft.ServiceFabric.Services.Client;

namespace Richter.Utilities {
   public static class ServiceFabricExtensions {
      public static EndpointResourceDescription GetEndpointResourceDescription(this ServiceContext context, String endpointName)
            => context.CodePackageActivationContext.GetEndpoint(endpointName);

      public static String CalcUriSuffix(this StatelessServiceContext context)
         => context.CalcUriSuffix(context.InstanceId);

      public static String CalcUriSuffix(this StatefulServiceContext context)
         => context.CalcUriSuffix(context.ReplicaId);

      private static String CalcUriSuffix(this ServiceContext context, Int64 instanceOrReplicaId)
         => $"{context.PartitionId}/{instanceOrReplicaId}" +
            $"/{Guid.NewGuid().ToByteArray().ToBase32String()}/";   // Uniqueness
   }
}