using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SfBayPounder {
   internal sealed class User {
      public User(string name) {
         this.Name = name;
         this.BiddingItems = new List<Item>();
      }

      public string Name { get; set; }
      public List<Item> BiddingItems { get; set; }
   }
}
