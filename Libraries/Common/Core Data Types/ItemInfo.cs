using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.Serialization;

namespace SFAuction.Common {
   [DataContract]
   public struct ItemInfo : IEquatable<ItemInfo>, IComparable<ItemInfo> {
      public ItemInfo(ItemId itemId, String imageUrl, DateTime expiration, IEnumerable<Bid> bids) {
         ItemId = itemId;
         ImageUrl = imageUrl;
         Expiration = expiration;
         Bids = bids.ToImmutableList();
      }

      [OnDeserialized]
      private void OnDeserialized(StreamingContext context) {
         Bids = Bids.ToImmutableList();
      }

      [DataMember]
      public readonly ItemId ItemId;

      [DataMember]
      public readonly String ImageUrl;

      [DataMember]
      public readonly DateTime Expiration;

      [DataMember]
      public IEnumerable<Bid> Bids { get; private set; }
      public override string ToString() => $"ItemId={ItemId}, Expiration={Expiration}";

      public static Boolean operator ==(ItemInfo itemInfo1, ItemInfo itemInfo2)
         => Object.ReferenceEquals(itemInfo1, null)
            ? Object.ReferenceEquals(itemInfo2, null)
            : itemInfo1.Equals(itemInfo2);

      public static Boolean operator !=(ItemInfo ItemInfo1, ItemInfo itemInfo2) => !(ItemInfo1 == itemInfo2);
      public override Boolean Equals(object obj) => (obj is ItemInfo) && Equals((ItemInfo)obj);
      public Boolean Equals(ItemInfo other) => CompareTo(other) == 0;
      public override Int32 GetHashCode() => ItemId.GetHashCode();
      public Int32 CompareTo(ItemInfo other) => ItemId.CompareTo(other.ItemId);
      public ItemInfo AddBid(Bid bid) {
         return new ItemInfo(ItemId, ImageUrl, Expiration, ((ImmutableList<Bid>)Bids).Add(bid));
      }
   }

   [DataContract]
   public sealed class Bid {
      public Bid(Email bidder, Decimal amount, DateTime? time = null) {
         Bidder = bidder;
         Amount = amount;
         Time = time ?? DateTime.UtcNow;
      }
      [DataMember]
      public readonly Email Bidder;
      [DataMember]
      public readonly Decimal Amount;
      [DataMember]
      public readonly DateTime Time;
      public override String ToString() => $"Bidder={Bidder}, Time={Time}, Amount={Amount}";
   }
}
