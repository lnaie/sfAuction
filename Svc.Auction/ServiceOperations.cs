using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Services.Client;
using Richter.Utilities;
using SFAuction.Common;
using SFAuction.OperationsProxy;

namespace SFAuction.Svc.Auction {
   internal sealed class ServiceOperations : IInternetOperations, IInternalOperations {
      #region Infrastructure
      private const String c_endpointName = "ReplicaEndpoint";
      private readonly Uri m_serviceNameUri;
      private readonly PartitionEndpointsResolver m_partitionEndpointResolver;

      internal ServiceOperations(PartitionEndpointsResolver partitionEndpointResolver, Uri serviceNameUri) {
         m_partitionEndpointResolver = partitionEndpointResolver;
         m_serviceNameUri = serviceNameUri;
      }
      private InternetAuctionOperationProxy GetProxy(String email) {
         // Maps email to a partition key
         Int64 partitionKey = Email.Parse(email).PartitionKey();
         var resolver = m_partitionEndpointResolver.CreateSpecific(m_serviceNameUri.ToString(), partitionKey, c_endpointName);
         return new InternetAuctionOperationProxy(resolver);
      }
      #endregion

      /// <summary>
      /// This method executes on the bidder's partition.
      /// Called by web: priority 0
      /// </summary>
      public Task<UserInfo> CreateUserAsync(String userEmail, CancellationToken cancellationToken) {
         var proxy = GetProxy(userEmail);
         return proxy.CreateUserAsync(userEmail, cancellationToken);
      }

      public Task<UserInfo> GetUserAsync(String userEmail, CancellationToken cancellationToken) {
         var proxy = GetProxy(userEmail);
         return proxy.GetUserAsync(userEmail, cancellationToken);
      }

      /// <summary>
      /// This method executes on the seller's partition.
      /// Called by web: priority 0
      /// </summary>
      public Task<ItemInfo> CreateItemAsync(String sellerEmail, String itemName, String imageUrl, DateTime expiration, Decimal startAmount, CancellationToken cancellationToken) {
         var proxy = GetProxy(sellerEmail);
         return proxy.CreateItemAsync(sellerEmail, itemName, imageUrl, expiration, startAmount, cancellationToken);
      }


      /// <summary>
      /// This method executes on the bidder's partition.
      /// Called by web: priority 0
      /// </summary>
      public Task<Bid[]> PlaceBidAsync(String bidderEmail, String sellerEmail, String itemName, Decimal bidAmount, CancellationToken cancellationToken) {
         var proxy = GetProxy(bidderEmail);
         return proxy.PlaceBidAsync(bidderEmail, sellerEmail, itemName, bidAmount, cancellationToken);
      }

      /// <summary>
      /// Called by web but priority 1
      /// </summary>
      /// <param name="userEmail"></param>
      /// <param name="cancellationToken"></param>
      /// <returns></returns>
      public Task<ItemInfo[]> GetItemsBiddingAsync(String userEmail, CancellationToken cancellationToken) {
         var proxy = GetProxy(userEmail);
         return proxy.GetItemsBiddingAsync(userEmail, cancellationToken);
      }

      //public async Task GetMyBidsAsync(Email bidderEmail, ItemId itemId, CancellationToken cancellationToken) {      }


      /// <summary>
      /// Called by web but priority 1
      /// </summary>
      /// <param name="userEmail"></param>
      /// <param name="cancellationToken"></param>
      /// <returns></returns>
      public Task<ItemInfo[]> GetItemsSellingAsync(String userEmail, CancellationToken cancellationToken) {
         var proxy = GetProxy(userEmail);
         return proxy.GetItemsSellingAsync(userEmail, cancellationToken);
      }

      /// <summary>
      /// Call by web home page a lot: priority 0
      /// </summary>
      /// <param name="cancellationToken"></param>
      /// <returns></returns>
      public async Task<ItemInfo[]> GetAuctionItemsAsync(CancellationToken cancellationToken) {
         var qm = new FabricClient().QueryManager;
         // Get this service's partitions
         var partitions = await qm.GetPartitionListAsync(m_serviceNameUri, null, TimeSpan.FromSeconds(4), cancellationToken);

         // Get each partition's low key
         var partitionKeys = from partition in partitions
                             select ((Int64RangePartitionInformation)partition.PartitionInformation).LowKey;

         // Simultaneously ask each partition for the auction items is has to sell
         var tasks = new List<Task<ItemInfo[]>>();
         foreach (var p in partitionKeys) {
            var resolver = m_partitionEndpointResolver.CreateSpecific(m_serviceNameUri.ToString(), p, c_endpointName);
            var proxy = new InternetAuctionOperationProxy(resolver);
            tasks.Add(proxy.GetAuctionItemsAsync(cancellationToken));
         }

         // Continue processing after every partition's auction items have come in
         ItemInfo[][] partitionsItemInfos = await Task.WhenAll(tasks);

         // Combine them all together sorting them by expiration
         var results = (from partitionItemInfos in partitionsItemInfos
                        from itemInfo in partitionItemInfos
                           // where itemInfo.Expiration >= DateTime.UtcNow   // Change this for Version 2
                        orderby itemInfo.Expiration
                        select itemInfo).ToArray();
         return results;   // Return the set of auction items
      }

      Task<Bid[]> IInternalOperations.PlaceBid2Async(string bidderEmail, string sellerEmail, string itemName, decimal bidAmount, CancellationToken cancellationToken) {
         Int64 partitionKey = Email.Parse(sellerEmail).PartitionKey();
         var resolver = m_partitionEndpointResolver.CreateSpecific(m_serviceNameUri.ToString(), partitionKey, c_endpointName);
         return new InternalAuctionOperationProxy(resolver).PlaceBid2Async(bidderEmail, sellerEmail, itemName, bidAmount, cancellationToken);
      }
   }
}