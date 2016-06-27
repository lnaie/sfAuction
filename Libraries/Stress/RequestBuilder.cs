using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SfBayPounder {
   internal static class RequestBuilder {
      public static string CreateUserRequest(string userName) {
         return $"{SfBayHttpClient.BaseAddress}?user={userName}&method=CreateUser";
      }

      public static string CreateItemRequest(string ownerName, string itemName, DateTime expiryDate, decimal basePrice) {
         return $"?user={ownerName}&method=CreateItem&itemName={itemName}&expiration={expiryDate.ToString()}&startPrice={basePrice.ToString()}";
      }

      public static string PlaceBidRequest(string bidderName, string itemName, decimal bidPrice) {
         return $"?user={bidderName}&method=PlaceBid&item={itemName}&price={bidPrice}";
      }
   }
}
