using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SfBayPounder {
   internal static class Program {
      private const int nUsers = 30;

      private const int nPerUser = 10;

      private const string DefaultBaseAddress = "http://localhost:8080/";

      private static SfBayHttpClient sfBayClient = null;

      private static List<User> users = new List<User>();

      private static readonly TimeSpan IntervalBetweenBids = TimeSpan.FromSeconds(1.0);

      private static Regex BidPattern = new Regex(@"^LastBid: User=(?<userName>\S+), Price=(?<price>\d+(\.\d+)?),.*$", RegexOptions.Compiled);

      private static readonly string userNamePrefix = "SfBayUser";
      private static readonly string itemNamePrefix = "Item";

      private static Dictionary<string, decimal> ItemPrice = new Dictionary<string, decimal>();
      private static object lockDictionaryLock;
      private static Dictionary<string, ReaderWriterLockSlim> lockDictionary;

      private static void Main(string[] args) {
         lockDictionaryLock = new object();
         lockDictionary = new Dictionary<string, ReaderWriterLockSlim>();

         string baseAddress = args.Length == 0 ? DefaultBaseAddress : args[0];

         sfBayClient = new SfBayHttpClient(baseAddress);

         InitializeUsersAsync().Wait();

         CancellationTokenSource ctkSource = new CancellationTokenSource(TimeSpan.FromMinutes(3.0));

         StartPounding(ctkSource.Token).Wait();
      }

      private static async Task InitializeUsersAsync() {
         for (int i = 0; i < nUsers; i++) {
            var u = new User(userNamePrefix + i);
            users.Add(u);
         }

         var createUserTasks = new List<Task>(nUsers);

         foreach (var u in users) {
            createUserTasks.Add(sfBayClient.SubmitRequestAsync(RequestBuilder.CreateUserRequest(u.Name)));
         }

         await Task.WhenAll(createUserTasks);

         await InitializeItemsAsync();
      }

      private static async Task InitializeItemsAsync() {
         int userIndex = 0;
         foreach (var user in users) {
            var others = users.Select(u => u).Where(u => u.Name != user.Name).ToList();

            string prefix = userNamePrefix + (userIndex++) + "-" + itemNamePrefix;

            var createRequestTasks = new List<Task>();
            for (int i = 0; i < nPerUser; i++) {
               string itemName = prefix + i;
               var exp = DateTime.UtcNow.Add(Item.DefaultBidInterval);
               string req = RequestBuilder.CreateItemRequest(
                   user.Name,
                   itemName,
                   exp,
                   Item.DefaultBidPrice);
               createRequestTasks.Add(sfBayClient.SubmitRequestAsync(req));

               ItemPrice[itemName] = Item.DefaultBidPrice;

               foreach (User other in others) {
                  var item = new Item(itemName);
                  item.ExpiryDate = exp;
                  item.LastBiddingPrice = Item.DefaultBidPrice;
                  other.BiddingItems.Add(item);
               }
            }

            await Task.WhenAll(createRequestTasks);
         }
      }

      private static async Task StartPounding(CancellationToken ctok) {
         var biddings = new List<Task>(nUsers);

         foreach (var u in users) {
            biddings.Add(PlaceBidsAsync(u, ctok));
         }

         await Task.WhenAll(biddings);
      }

      private static async Task PlaceBidsAsync(User user, CancellationToken ctok) {
         string result = String.Empty;

         while (!ctok.IsCancellationRequested) {
            if (user.BiddingItems.Count == 0) {
               Console.WriteLine(string.Format("User, {0}, is not interested in bidding.", user.Name));
            }

            int indexToChoose = ConcurrentRandom.Next(user.BiddingItems.Count);
            Item itemToBidOn = user.BiddingItems.ElementAt(indexToChoose);

            ReaderWriterLockSlim keyLock = GetKeyLock(itemToBidOn.Name);
            keyLock.EnterWriteLock();

            try {
               decimal priceToBidWith = ItemPrice[itemToBidOn.Name] + (decimal)ConcurrentRandom.NextDouble();
               string request = RequestBuilder.PlaceBidRequest(user.Name, itemToBidOn.Name, priceToBidWith);
               result = sfBayClient.SubmitRequestAsync(request).Result;
               ItemPrice[itemToBidOn.Name] = priceToBidWith;
               PrintBidResult(result, user, priceToBidWith.ToString(), itemToBidOn.Name);
            }
            finally {
               keyLock.ExitWriteLock();
            }

            await Task.Delay(IntervalBetweenBids);
         }

         //return sfBayClient.SubmitRequestAsync(RequestBuilder.CreateUserRequest("Jeffrey")).Result;
      }

      private static void PrintBidResult(string result, User user, string bidPrice, string itemName) {
         if (string.IsNullOrEmpty(result)) {
            return;
         }

         var match = BidPattern.Match(result);

         if (!match.Success) {
            Console.WriteLine("Invalid result from placing a bid (user = {0}, bidPrice = {1}, itemName = {2}): \n{3}",
                user.Name,
                bidPrice,
                itemName,
                result);
            return;
         }

         if (match.Groups["userName"].Value.Equals(user.Name, StringComparison.OrdinalIgnoreCase)
             && match.Groups["price"].Value.Equals(bidPrice, StringComparison.OrdinalIgnoreCase)) {
            Console.WriteLine("Bid succeeded\n");

            /*var itemToUpdate = user.BiddingItems.Find(i => i.Name == itemName);
            decimal price;
            if (decimal.TryParse(bidPrice, out price))
            {
                ItemPrice[itemName] = price;
            }
            else
            {
                throw  new ArgumentException("Could not parse bid price.");
            }*/
         } else {
            Console.WriteLine(string.Format("Bid failed[user={0}, price={1}, item={2}]\n{3}\n\n",
                user.Name,
                bidPrice,
                itemName,
                result));
         }
      }

      private static ReaderWriterLockSlim GetKeyLock(string key) {
         lock (lockDictionaryLock) {
            ReaderWriterLockSlim lockSlim;
            if (!lockDictionary.TryGetValue(key, out lockSlim)) {
               lockSlim = new ReaderWriterLockSlim();
               lockDictionary.Add(key, lockSlim);
            }

            return lockSlim;
         }
      }
   }
}
