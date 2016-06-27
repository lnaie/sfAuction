using System;
using System.Threading;
using System.Threading.Tasks;

namespace SFAuction.Common {
   /// <summary>
   /// This interface ensures that SFAuction.Svc.Auction.PartitionOperations and 
   /// SFAuction.OperationsProxy.AuctionOperationsProxy expose the same operations
   /// with the same signatures.
   /// </summary>
   public interface IInternetOperations {
      Task<UserInfo> CreateUserAsync(String _userEmail, CancellationToken cancellationToken);
      Task<UserInfo> GetUserAsync(String userEmail, CancellationToken cancellationToken);
      Task<ItemInfo> CreateItemAsync(String _sellerEmail, String _itemName, String _imageUrl, DateTime _expiration, Decimal _startAmount, CancellationToken cancellationToken);
      Task<Bid[]> PlaceBidAsync(String _bidderEmail, String _sellerEmail, String _itemName, Decimal _bidAmount, CancellationToken cancellationToken);
      Task<ItemInfo[]> GetItemsBiddingAsync(String _userEmail, CancellationToken cancellationToken);
      Task<ItemInfo[]> GetItemsSellingAsync(String _userEmail, CancellationToken cancellationToken);
      Task<ItemInfo[]> GetAuctionItemsAsync(CancellationToken cancellationToken);
   }
   public interface IInternalOperations {
      Task<Bid[]> PlaceBid2Async(String bidderEmail, String sellerEmail, String itemName, Decimal bidAmount, CancellationToken cancellationToken);
   }
}