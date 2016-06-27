using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using Richter.Utilities;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace SFAuction.Svc.Auction {

   public sealed class ReliableList<TKey> where TKey : IComparable<TKey>, IEquatable<TKey> {
      private readonly IReliableDictionary<TKey, Object> m_dictionary;
      public static async Task<ReliableList<TKey>> CreateAsync(IReliableStateManager stateManager, String name) {
         return new ReliableList<TKey>(await stateManager.GetOrAddAsync<IReliableDictionary<TKey, Object>>(name));
      }
      private ReliableList(IReliableDictionary<TKey, Object> dictionary) { m_dictionary = dictionary; }

      public Task ClearAsync(TimeSpan timeout = default(TimeSpan), CancellationToken cancellationToken = default(CancellationToken))
               => m_dictionary.ClearAsync(timeout.DefaultToInfinite(), cancellationToken);
      public Task AddAsync(ITransaction tx, TKey key, TimeSpan timeout = default(TimeSpan), CancellationToken cancellationToken = default(CancellationToken))
         => m_dictionary.AddAsync(tx, key, null, timeout.DefaultToInfinite(), cancellationToken);

      public Task<bool> ContainsAsync(ITransaction tx, TKey key, LockMode lockMode = LockMode.Default, TimeSpan timeout = default(TimeSpan), CancellationToken cancellationToken = default(CancellationToken))
         => m_dictionary.ContainsKeyAsync(tx, key, lockMode, timeout.DefaultToInfinite(), cancellationToken);
      public Task<bool> TryAddAsync(ITransaction tx, TKey key, TimeSpan timeout = default(TimeSpan), CancellationToken cancellationToken = default(CancellationToken))
         => m_dictionary.TryAddAsync(tx, key, null, timeout.DefaultToInfinite(), cancellationToken);
      public Task TryRemoveAsync(ITransaction tx, TKey key, TimeSpan timeout = default(TimeSpan), CancellationToken cancellationToken = default(CancellationToken))
               => m_dictionary.TryRemoveAsync(tx, key, timeout.DefaultToInfinite(), cancellationToken);
      public async Task<IAsyncEnumerable<TKey>> CreateEnumerableAsync(ITransaction tx, EnumerationMode enumerationMode = EnumerationMode.Unordered, Func<TKey, Boolean> filter = null) {
         IAsyncEnumerable<KeyValuePair<TKey, Object>> enumerable = await m_dictionary.CreateEnumerableAsync(tx, filter ?? (k => true), enumerationMode);
         return new ReliableListEnumerable<TKey>(enumerable);
      }
   }

   internal sealed class ReliableListEnumerable<TKey> : IAsyncEnumerable<TKey> {
      private readonly IAsyncEnumerable<KeyValuePair<TKey, Object>> m_inner;
      public ReliableListEnumerable(IAsyncEnumerable<KeyValuePair<TKey, Object>> inner) {
         m_inner = inner;
      }
      public IAsyncEnumerator<TKey> GetAsyncEnumerator() =>
         new ReliableListAsyncEnumerator<TKey>(m_inner.GetAsyncEnumerator());
   }

   internal sealed class ReliableListAsyncEnumerator<TKey> : IAsyncEnumerator<TKey> {
      private readonly IAsyncEnumerator<KeyValuePair<TKey, Object>> m_inner;
      public ReliableListAsyncEnumerator(IAsyncEnumerator<KeyValuePair<TKey, Object>> inner) {
         m_inner = inner;
      }
      public TKey Current => m_inner.Current.Key;

      public void Dispose() => m_inner.Dispose();

      public Task<bool> MoveNextAsync(CancellationToken cancellationToken) => m_inner.MoveNextAsync(cancellationToken);

      public void Reset() => m_inner.Reset();
   }
}