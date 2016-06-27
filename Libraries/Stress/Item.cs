using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SfBayPounder {
   class Item {
      public static readonly TimeSpan DefaultBidInterval = TimeSpan.FromMinutes(10.0);
      public static readonly decimal DefaultBidPrice = (decimal)1.0;

      public Item(string name) {
         this.LastBiddingPrice = DefaultBidPrice;
         this.ExpiryDate = DateTime.UtcNow.Add(DefaultBidInterval);
         this.Name = name;
      }

      public decimal LastBiddingPrice { get; set; }
      public DateTime ExpiryDate { get; set; }
      public string Name { get; set; }
   }
}
