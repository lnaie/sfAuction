//#define Version1
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Client;
using SFAuction.Common;
using Richter.Utilities;
using SFAuction.OperationsProxy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SFAuction.Svc.Auction {
   internal sealed class PartitionOperations : IInternetOperations, IInternalOperations {
      private static readonly Uri AuctionServiceNameUri = new Uri(@"fabric:/SFAuction/AuctionSvcInstance");
      #region Infrastructure
      private static readonly PartitionEndpointsResolver s_partitionEndpointResolver
         = new PartitionEndpointsResolver();
      private readonly IReliableStateManager m_stateMgr;
      private readonly IReliableDictionary<Email, UserInfo> m_users;
      private readonly ReliableList<ItemId> m_unexpiredItems;
      private Task<IReliableDictionary<ItemId, ItemInfo>> GetSellerItemsAsync(Email sellerEmail) =>
         m_stateMgr.GetOrAddAsync<IReliableDictionary<ItemId, ItemInfo>>("UserItems-" + sellerEmail.Key);

      internal static async Task<PartitionOperations> CreateAsync(IReliableStateManager stateManager) {
         var partitionUsers = await stateManager.GetOrAddAsync<IReliableDictionary<Email, UserInfo>>("PartitionUsers");
         var partitionUnexpiredItems = await ReliableList<ItemId>.CreateAsync(stateManager, "PartitionUnexpiredItems");
         return new PartitionOperations(stateManager, partitionUsers, partitionUnexpiredItems);
      }

      private PartitionOperations(IReliableStateManager stateManager,
         IReliableDictionary<Email, UserInfo> partitionUsers,
         ReliableList<ItemId> partitionUnexpiredItems) {
         m_stateMgr = stateManager;
         m_users = partitionUsers;
         m_unexpiredItems = partitionUnexpiredItems;
      }

      private ITransaction CreateTransaction() => m_stateMgr.CreateTransaction();
      #endregion

      /// <summary>
      /// This method executes on the bidder's partition.
      /// Called by web: priority 0
      /// </summary>
      public async Task<UserInfo> CreateUserAsync(String userEmail, CancellationToken cancellationToken) {
         // Create user and add to dictionary; fail if user already exists
         Email _userEmail = Email.Parse(userEmail);
         using (var tx = CreateTransaction()) {
            var userInfo = new UserInfo(_userEmail);
            try {
               await m_users.AddAsync(tx, _userEmail, userInfo);
               await tx.CommitAsync();
               return userInfo;
            }
            catch (Exception ex) {  // Change to what if already exists
               throw new InvalidOperationException($"User '{_userEmail}' already exists.", ex);
            }
         }
      }

      public async Task<UserInfo> GetUserAsync(String userEmail, CancellationToken cancellationToken) {
         Email _userEmail = Email.Parse(userEmail);
         using (var tx = CreateTransaction()) {
            var userInfo = await m_users.TryGetValueAsync(tx, _userEmail);
            if (userInfo.HasValue) return userInfo.Value;
            throw new InvalidOperationException($"User '{_userEmail}' doesn't exist.");
         }
      }

      /// <summary>
      /// This method executes on the bidder's partition.
      /// Called by web: priority 0
      /// </summary>
      public async Task<ItemInfo> CreateItemAsync(String sellerEmail, String itemName, String imageUrl, DateTime expiration, Decimal startAmount, CancellationToken cancellationToken) {
         // NOTE: If items gets large, old item value (but not key) can move to warm storage

         // If user exists, create item & transactionally and it to user's items dictionary & unexpired items dictionary
         Email _sellerEmail = Email.Parse(sellerEmail);
         using (var tx = CreateTransaction()) {
            var cr = await m_users.TryGetValueAsync(tx, _sellerEmail);
            if (!cr.HasValue)
               throw new InvalidOperationException($"Seller '{_sellerEmail}' doesn't exist.");

            // Look up user's (seller's) auction item dictionary & add the new item to it:
            IReliableDictionary<ItemId, ItemInfo> userItems = await GetSellerItemsAsync(_sellerEmail);

            ItemId itemId = ItemId.Parse(_sellerEmail, itemName);
            ItemInfo item = new ItemInfo(itemId, imageUrl, expiration, new[] { new Bid(_sellerEmail, startAmount) });   // Seller places first bid
            try {
               await userItems.AddAsync(tx, itemId, item); // TODO: If already exists
            }
            catch (Exception ex) {
               throw new InvalidOperationException($"Seller '{itemId.Seller}' is already selling '{itemId.ItemName}'.", ex);
            }
            await m_unexpiredItems.AddAsync(tx, itemId);
            await tx.CommitAsync();
            return item;
         }
      }


      /// <summary>
      /// This method executes on the bidder's partition.
      /// Called by web: priority 0
      /// </summary>
      public async Task<Bid[]> PlaceBidAsync(String bidderEmail, String sellerEmail, String itemName, Decimal bidAmount, CancellationToken ct) {
         Email _sellerEmail = Email.Parse(sellerEmail);
         using (var tx = CreateTransaction()) {
            Email _bidderEmail = Email.Parse(bidderEmail);
            ItemId itemId = ItemId.Parse(_sellerEmail, itemName);

            var cr = await m_users.TryGetValueAsync(tx, _bidderEmail);
            if (!cr.HasValue) throw new InvalidOperationException($"Bidder '{_bidderEmail}' doesn't exist.");

            // Create new User object identical to current with new itemId added to it (if not already in collection [idempotent])
            UserInfo userInfo = cr.Value;
            if (!userInfo.ItemsBidding.Contains(itemId)) {
               userInfo = userInfo.AddItemBidding(itemId);
               await m_users.SetAsync(tx, _bidderEmail, userInfo);
               await tx.CommitAsync();
            }
            // NOTE: If we fail here, the bidder thinks they're bidding on an item but the 
            // item doesn't know about the bidder. If so, the bidder will not see their latest bid.
            // The bidder could bid again (which is why adding the item is idempotent). 
         }
         // Tell seller's partition to place a bid
         var proxy = (IInternalOperations)new ServiceOperations(s_partitionEndpointResolver, AuctionServiceNameUri);
         return await proxy.PlaceBid2Async(bidderEmail, sellerEmail, itemName, bidAmount, ct);
      }

      /// <summary>
      /// This method executes on the seller's partition. This method is only ever called by PlaceBidAsync; not via Internet
      /// Not called by web at all
      /// </summary>
      public async Task<Bid[]> PlaceBid2Async(String bidderEmail, String sellerEmail, String itemName, Decimal bidAmount, CancellationToken ct) {
         // This method executes on the seller's partition
         Email _sellerEmail = Email.Parse(sellerEmail);
         using (var tx = CreateTransaction()) {
            Email _bidderEmail = Email.Parse(bidderEmail);
            ItemId itemId = ItemId.Parse(_sellerEmail, itemName);
            Boolean isUnexpiredItem = await m_unexpiredItems.ContainsAsync(tx, itemId);
            if (!isUnexpiredItem)
               throw new InvalidOperationException("Item's auction expired or item doesn't exist.");

            var sellersItems = await GetSellerItemsAsync(itemId.Seller);
            var conditionalItemInfo = await sellersItems.TryGetValueAsync(tx, itemId);
            if (!conditionalItemInfo.HasValue) throw new InvalidOperationException("Item doesn't exist.");  // We should never get here

            var itemInfo = conditionalItemInfo.Value;
            var bids = itemInfo.Bids;
            var lastBid = bids.Last(); // Get current last bid

            //var cr = await m_users.TryGetValueAsync(tx2, itemId.Seller);
            if (DateTime.UtcNow > itemInfo.Expiration) throw new InvalidOperationException("The action for this item has expired.");
            if (_bidderEmail == lastBid.Bidder) throw new InvalidOperationException("You cannot outbid yourself.");
            if (bidAmount <= lastBid.Amount) throw new InvalidOperationException("Your bid must be greater than the highest bid.");

            // Create a new item copied from the original with the new bid
            itemInfo = itemInfo.AddBid(new Bid(_bidderEmail, bidAmount));
            await sellersItems.SetAsync(tx, itemId, itemInfo);
            await tx.CommitAsync();
            return itemInfo.Bids.ToArray();
         }
      }

      /// <summary>
      /// Called by web but priority 1
      /// </summary>
      /// <param name="userEmail"></param>
      /// <param name="cancellationToken"></param>
      /// <returns></returns>
      public Task<ItemInfo[]> GetItemsBiddingAsync(String userEmail, CancellationToken cancellationToken) {
         // If user doesn’t exist, fail
         // For each item bidding, look up item & return bids
         // Note: user may not have a bid due to network failure or bid didn’t pass tests
         return null;
      }

      //public async Task GetMyBidsAsync(Email bidderEmail, ItemId itemId, CancellationToken cancellationToken) {      }


      /// <summary>
      /// Called by web but priority 1
      /// </summary>
      /// <param name="userEmail"></param>
      /// <param name="cancellationToken"></param>
      /// <returns></returns>
      public Task<ItemInfo[]> GetItemsSellingAsync(String userEmail, CancellationToken cancellationToken) {
         // If user doesn’t exist, fail
         // For each item in per-user dictionary, return items
         return null;
      }

      /// <summary>
      /// Call by web home page a lot: priority 0
      /// </summary>
      /// <param name="cancellationToken"></param>
      /// <returns></returns>
      public async Task<ItemInfo[]> GetAuctionItemsAsync(CancellationToken cancellationToken) {
         // Always shows unexpired items
         // Return items in each partition’s unexpired dictionary
         // Note: Some may be expired; can purge now or wait for GC, or UI can filter         
         var itemInfos = new List<ItemInfo>();
         using (var tx = CreateTransaction()) {
            var unexpiredItems = await m_unexpiredItems.CreateEnumerableAsync(tx);
            using (var enumerator = unexpiredItems.GetAsyncEnumerator()) {
               while (await enumerator.MoveNextAsync(cancellationToken)) {
                  ItemId itemId = enumerator.Current;
                  IReliableDictionary<ItemId, ItemInfo> sellerItems = await GetSellerItemsAsync(itemId.Seller);
                  ConditionalValue<ItemInfo> cr = await sellerItems.TryGetValueAsync(tx, itemId);
                  if (cr.HasValue && cr.Value.Expiration > DateTime.UtcNow)
                     itemInfos.Add(cr.Value);
               }
            }
         }
         return itemInfos.ToArray();
      }

      /// <summary>
      /// Periodically scans per-partition unexpired items and delete old ones.
      /// Not called be web
      /// </summary>
      /// <returns></returns>
      public Task GarbageCollectExpiredItemsAsync(CancellationToken cancellationToken) {
         return null;
      }
   }
}


#if false
UserId: Users have an Id which consists of their email address
ItemId: Items have an Id which consists of the seller's email/item name (string)

User's are partitioned to scale out the number of users and the items they are selling
Each partition has users: ReliableDictionary<UserId, UserInfo>
	UserInfo has static user info & growing list of items being bid upon
	Each user has ReliableDictionary<ItemId, ItemInfo> for item's they're selling
		ItemInfo has static item info & growing list of bids
			Bid has UserId (email), time, amount

With the structure above, it is hard to get a list of unexpired items currently up for auction
So, we also keep a collection of unexpired auction item Ids: ReliableList<ItemId>
#endif


#if false
Users\email
	Create - Post
	Get - Get

Users\sellerEmail\Items
	Create { itemName, imageUrl, expiration, startAmount}
	returns 201 & location header

users\bidderEmail
	placebid - Post { sellerEmail, itemName, bidAmount }

Items[\seller[\item]]


Site creates new user   POST /Users/email { Name:name }
Site logs in user       If user not in dictionary, fail
User creates new Item   POST /Users/email/ItemsSelling/  { name : n, imageUrl: u, Expiration: e, StartAmount: a }
User bids on other user’s item   POST /Users/bidderEmail/ItemsBidding/Item
Users checks items they’re bidding  GET /Users/email/ItemsBidding/[item]
User checks items they’re selling   GET /Users/email/ItemsSelling/[item/]
Garbage Collect expired items 
Show items for auction  GET /Items/[item]
#endif
