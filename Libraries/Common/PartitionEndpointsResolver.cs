using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;                      // System.dll
using System.Net.Http;                 // System.Net.Http.dll
using System.Runtime.Caching;          // System.Runtime.Caching.dll
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization; // System.Web.Extensions.dll

namespace Richter.Utilities {
   public sealed class PartitionEndpointsResolver : IDisposable {
      private static readonly JavaScriptSerializer s_javaScriptSerializer = new JavaScriptSerializer();

      private readonly HttpClient m_httpClient;
      private readonly String m_clusterEndpoint;
      private readonly Cache<PartitionInfo> m_clusterPartitionEndpointCache;

      private sealed class PartitionInfo {
         public PartitionInfo(String previousRspVersion, IDictionary<String, String> endpoints) {
            PreviousRspVersion = previousRspVersion;
            Endpoints = new ReadOnlyDictionary<String, String>(endpoints);
         }
         public readonly String PreviousRspVersion;
         public readonly IReadOnlyDictionary<String, String> Endpoints;
      }

      public PartitionEndpointsResolver(String clusterEndpoint = "localhost", TimeSpan endpointTtl = default(TimeSpan), HttpClient httpClient = null) {
         m_httpClient = httpClient ?? new HttpClient();
         m_clusterEndpoint = clusterEndpoint;
         endpointTtl = (endpointTtl == default(TimeSpan)) ? TimeSpan.FromMinutes(5) : endpointTtl;
         m_clusterPartitionEndpointCache = new Cache<PartitionInfo>(m_clusterEndpoint, endpointTtl);
      }
      public void Dispose() => m_clusterPartitionEndpointCache.Dispose();

      private async Task<PartitionInfo> ResolvePartitionEndpointsAsync(String serviceName, Int64? partitionKey, String previousRspVersion, CancellationToken cancellationToken = default(CancellationToken)) {
         serviceName = serviceName.Replace("fabric:/", String.Empty);

         String uri = $"http://{m_clusterEndpoint}:19080/Services/{serviceName}/$/ResolvePartition?api-version=1.0";
         if (partitionKey != null)
            uri += $"&PartitionKeyType=2&PartitionKeyValue={partitionKey.Value}";
         if (previousRspVersion != null)
            uri += $"&PreviousRspVersion={previousRspVersion}";

         String partitionJson = await m_httpClient.GetStringAsync(new Uri(uri)); // Fix to take CancellationToken
         IDictionary<String, Object> partitionObject = (IDictionary<String, Object>)s_javaScriptSerializer.DeserializeObject(partitionJson);
         previousRspVersion = (String)partitionObject["Version"];

         String partitionAddress = (String)
            ((IDictionary<String, Object>)((Object[])partitionObject["Endpoints"])[0])["Address"];

         IDictionary<String, String> partitionEndpoints =
            s_javaScriptSerializer.Deserialize<EndpointsCollection>(partitionAddress).Endpoints;
         return new PartitionInfo(previousRspVersion, partitionEndpoints);
      }
      private sealed class EndpointsCollection {
         public Dictionary<String, String> Endpoints = null;
      }

      public PartitionEndpointResolver CreateSpecific(String serviceName, Int64 partitionKey, String endpointName)
         => new PartitionEndpointResolver(this, serviceName, partitionKey, endpointName);
      public PartitionEndpointResolver CreateSpecific(String serviceName, String endpointName)
         => new PartitionEndpointResolver(this, serviceName, null, endpointName);

      public Task<TResult> CallAsync<TResult>(String serviceName, CancellationToken cancellationToken,
         Func<IReadOnlyDictionary<String, String>, CancellationToken, Task<TResult>> func)
         => CallAsync(serviceName, null, cancellationToken, func);

      public Task<TResult> CallAsync<TResult>(String serviceName, Int64 partitionKey, CancellationToken cancellationToken,
         Func<IReadOnlyDictionary<String, String>, CancellationToken, Task<TResult>> func)
         => CallAsync(serviceName, (Int64?) partitionKey, cancellationToken, func);

      private async Task<TResult> CallAsync<TResult>(String serviceName, Int64? partitionKey, CancellationToken cancellationToken,
         Func<IReadOnlyDictionary<String, String>, CancellationToken, Task<TResult>> func) {
         var serviceNameAndPartition = serviceName + ";" + partitionKey?.ToString();
         for (;;) {
            PartitionInfo servicePartitionInfo = m_clusterPartitionEndpointCache.Get(serviceNameAndPartition);
            try {
               // We do not have endpoints, get them using https://msdn.microsoft.com/en-us/library/azure/dn707638.aspx
               if (servicePartitionInfo == null) {
                  servicePartitionInfo = await ResolvePartitionEndpointsAsync(serviceName, partitionKey, servicePartitionInfo?.PreviousRspVersion, cancellationToken);
                  m_clusterPartitionEndpointCache.Add(serviceNameAndPartition, servicePartitionInfo);
               }
               return await func(servicePartitionInfo.Endpoints, cancellationToken);
            }
            catch (HttpRequestException ex) when ((ex.InnerException as WebException)?.Status == WebExceptionStatus.ConnectFailure) {
               // Force update of latest endpoints from naming service
               servicePartitionInfo = await ResolvePartitionEndpointsAsync(serviceName, partitionKey, servicePartitionInfo?.PreviousRspVersion, cancellationToken);
               m_clusterPartitionEndpointCache.Set(serviceNameAndPartition, servicePartitionInfo);
            }
         }
      }


      private sealed class Cache<TValue> : IDisposable where TValue : class {
         private readonly MemoryCache m_cache;
         private readonly CacheItemPolicy m_cacheItemPolicy;
         public Cache(String name, TimeSpan slidingExpiration) {
            System.Collections.Specialized.NameValueCollection config = null;
            /*new System.Collections.Specialized.NameValueCollection {
               { "CacheMemoryLimitMegabytes", "50942361600" },
               { "PhysicalMemoryLimitPercentage", "99" },
               { "PollingInterval", "00:02:00" } };*/
            m_cache = new MemoryCache(name, config);
            m_cacheItemPolicy = new CacheItemPolicy { SlidingExpiration = slidingExpiration };
         }
         public void Dispose() => m_cache.Dispose();
         public TValue Get(String key) => (TValue)m_cache.Get(key);
         public TValue AddOrGetExisting(String key, TValue value)
            => (TValue)m_cache.AddOrGetExisting(key, value, m_cacheItemPolicy);
         public Boolean Add(String key, TValue value)
            => m_cache.Add(key, value, m_cacheItemPolicy);
         public void Set(String key, TValue value)
            => m_cache.Set(key, value, m_cacheItemPolicy);
         public TValue Remove(String key) => (TValue)m_cache.Remove(key);
      }
   }

   public sealed class PartitionEndpointResolver {
      public readonly String ServiceName;
      public readonly Int64? PartitionKey;
      public readonly String EndpointName;
      private readonly PartitionEndpointsResolver m_partitionEndpointsResolver;
      internal PartitionEndpointResolver(PartitionEndpointsResolver partitionEndpointResolver, String serviceName, Int64? partitionKey, String endpointName) {
         m_partitionEndpointsResolver = partitionEndpointResolver;
         ServiceName = serviceName;
         PartitionKey = partitionKey;
         EndpointName = endpointName;
      }
      public Task<TResult> CallAsync<TResult>(CancellationToken cancellationToken,
         Func<String, CancellationToken, Task<TResult>> func)
         => PartitionKey.HasValue
            ? m_partitionEndpointsResolver.CallAsync(ServiceName, PartitionKey.Value, cancellationToken,
               (ep, ct) => func(ep[EndpointName], ct))
            : m_partitionEndpointsResolver.CallAsync(ServiceName, cancellationToken,
               (ep, ct) => func(ep[EndpointName], ct));
   }
}