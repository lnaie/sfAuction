// http://stephenwalther.com/archive/2015/02/07/asp-net-5-deep-dive-routing
// http://blog.scottlogic.com/2016/01/20/restful-api-with-aspnet50.html
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using UserInfo = System.String;
using ItemInfo = System.String;
using Bid = System.String;

namespace Web1.Controllers {
   [Route("api/[controller]")]
   public sealed class AuctionController : Controller {
      public AuctionController() { }

      /*
      public Task<UserInfo> CreateUserAsync(String userEmail, CancellationToken cancellationToken) {
      public Task<UserInfo> GetUserAsync(String userEmail, CancellationToken cancellationToken) {
      public Task<ItemInfo> CreateItemAsync(String sellerEmail, String itemName, String imageUrl, DateTime expiration, Decimal startAmount, CancellationToken cancellationToken) {
      public Task<Bid[]> PlaceBidAsync (String bidderEmail, String sellerEmail, String itemName, Decimal bidAmount, CancellationToken cancellationToken) {
      public Task<Bid>   PlaceBid2Async(String bidderEmail, String sellerEmail, String itemName, Decimal bidAmount, CancellationToken cancellationToken) {
      public Task<ItemInfo[]> GetItemsBiddingAsync(String userEmail, CancellationToken cancellationToken) {
      public Task<ItemInfo[]> GetItemsSellingAsync(String userEmail, CancellationToken cancellationToken) {
      public Task<ItemInfo[]> GetAuctionItemsAsync(CancellationToken cancellationToken) {
      */
      [HttpPut("PlaceBid/{bidderEmail, sellerEmail, itemName, bidAmount}")]
      public Bid[] PlaceBidAsync(String bidderEmail, String sellerEmail, String itemName, Decimal bidAmount)
         => new Bid[0];

      [HttpPut("PlaceBid2/{bidderEmail, sellerEmail, itemName, bidAmount}")]
      public Bid PlaceBid2Async(String bidderEmail, String sellerEmail, String itemName, Decimal bidAmount)
         => default(Bid);



      [HttpPost("Users/{userEmail}")]
      public UserInfo CreateUser(String userEmail) => "Jeff";

      [HttpGet("Users/{userEmail}")]   // http://localhost:8080/api/Auction/Users/Jeff
      public UserInfo GetUser(String userEmail) => "Richter";

      [HttpPost("Items/{sellerEmail, itemName, imageUrl, expiration, startAmount}")]
      public ItemInfo CreateItem(String userEmail, String itemName, String imageUrl, DateTime expiration, Decimal startAmount)
         => "ItemInfo";

      [HttpGet("ItemsBidding/{userEmail}")] // http://localhost:8080/api/Auction/ItemsBidding/marc
      public IEnumerable<ItemInfo> GetItemsBidding(String userEmail) => new[] { "i1,", "i2" };


      [HttpGet("ItemsSelling/{userEmail}")]  // http://localhost:8080/api/Auction/ItemsSelling/marc
      public IEnumerable<ItemInfo> GetItemsSelling(String userEmail) => new[] { "iiii1,", "iiii2" };


      [HttpGet("AuctionItems")]   // http://localhost:8080/api/Auction/AuctionItems/
      public IEnumerable<ItemInfo> GetAuctionItems() => new[] { "isfddsf1,", "iksjdhf2" };

#if false

      // GET: api/values
      [HttpGet, Route("", Name = "", Order = 1)]
      public IEnumerable<string> Get() {
         return new string[] { "value1", "value2" };
      }

      // GET api/values/5
      [HttpGet("{id}")]
      public string Get(int id) {
         return "value";
      }

      // POST api/values
      [HttpPost]
      public void Post([FromBody]string value) {
      }

      // PUT api/values/5
      [HttpPut("{id}")]
      public void Put(int id, [FromBody]string value) {
      }

      // DELETE api/values/5
      [HttpDelete("{id}")]
      public void Delete(int id) {
      }
#endif
   }
}
