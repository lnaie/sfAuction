using System;
using System.Runtime.Serialization;
using Richter.Utilities;

namespace SFAuction.Common {
   /// <summary>ItemId is case preserved but case insensitive.</summary>
   [DataContract]
   public struct ItemId : IEquatable<ItemId>, IComparable<ItemId> {
      #region Static members
      private const Int32 c_MaxCharacters = 100;
      private const String c_delimiter = "~";
      public static readonly ItemId Empty = new ItemId(Email.Empty, String.Empty);

      private static Boolean IsValidFormat(String itemName) => true;

      public static ItemId Parse(Email seller, String itemName, Boolean trustInput = false) {
         if (!trustInput) {
            if (itemName != null) itemName = itemName.Trim();
            if (itemName.IsNullOrWhiteSpace() || itemName.Length > c_MaxCharacters || !IsValidFormat(itemName))
               throw new InvalidItemIdFormatException(itemName);
         }
         return new ItemId(seller, itemName);
      }

      public static Boolean operator ==(ItemId itemId1, ItemId itemId2) => itemId1.Equals(itemId2);
      public static Boolean operator !=(ItemId ItemId1, ItemId itemId2) => !ItemId1.Equals(itemId2);
      #endregion

      [DataMember] public readonly Email Seller;
      [DataMember] public readonly String ItemName;
      public ItemId(Email seller, String itemName) {
         if (itemName == null) throw new ArgumentNullException(nameof(itemName));
         Seller = seller;
         ItemName = itemName;
      }
      public Boolean IsEmpty => ItemName.IsNullOrWhiteSpace();
      public override String ToString() => $"Seller={Seller}, Name={ItemName}";
      public String Key => (Seller + c_delimiter + ItemName).ToLowerInvariant();
      public override Boolean Equals(object obj) => (obj is ItemId) && Equals((ItemId)obj);
      public Boolean Equals(ItemId other) => CompareTo(other) == 0;
      public override Int32 GetHashCode() => Key.GetHashCode();
      public int CompareTo(ItemId other) {
         if (other == null) return 1;
         var n = Seller.CompareTo(other.Seller);
         return (n != 0) ? n : String.Compare(ItemName, other.ItemName, StringComparison.OrdinalIgnoreCase);
      }
   }

   public sealed class InvalidItemIdFormatException : Exception {
      public InvalidItemIdFormatException(String attemptedItemId) { AttemptedItemId = attemptedItemId; }
      public String AttemptedItemId { get; private set; }
   }
}