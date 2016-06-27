using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Collections.Immutable;

namespace SFAuction.Common {
   [DataContract]
   // If you don’t seal, derived classes must also be immutable
   public sealed class UserInfo {
   private static readonly IEnumerable<ItemId> NoBids = ImmutableList<ItemId>.Empty;

      public UserInfo(Email email, IEnumerable<ItemId> itemsBidding = null) {
         Email = email;
         ItemsBidding = (itemsBidding == null) ? NoBids : itemsBidding.ToImmutableList();
      }

      [OnDeserialized]
      private void OnDeserialized(StreamingContext context) {
         // Convert the deserialized collection to an immutable collection
         ItemsBidding = ItemsBidding.ToImmutableList();
      }

      [DataMember]
      public readonly Email Email;

      // Ideally, this would be a readonly field but it can't be because OnDeserialized 
      // has to set it. So instead, the getter is public and the setter is private.
      [DataMember]
      public IEnumerable<ItemId> ItemsBidding { get; private set; }
      public override string ToString() => $"Email={Email}, ItemsBidding={ItemsBidding.Count()}";

      // Since each UserInfo object is immutable, we add a new ItemId to the ItemsBidding
      // collection by creating a new immutable UserInfo object with the added ItemId.
      public UserInfo AddItemBidding(ItemId itemId) {
         return new UserInfo(Email, ((ImmutableList<ItemId>)ItemsBidding).Add(itemId));
      }
   }
}