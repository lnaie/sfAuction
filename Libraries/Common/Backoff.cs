using System;
using System.Threading;
using System.Threading.Tasks;

namespace Richter.Utilities {
   public struct ExponentialBackoff {
      private readonly Int32 m_maxRetries, m_delayMilliseconds, m_maxDelayMilliseconds;
      private Int32 m_retries;
      public ExponentialBackoff(Int32 maxRetries, Int32 delayMilliseconds, Int32 maxDelayMilliseconds) {
         m_maxRetries = maxRetries;
         m_delayMilliseconds = delayMilliseconds;
         m_maxDelayMilliseconds = maxDelayMilliseconds;
         m_retries = 0;
      }
      public Task Delay(CancellationToken cancellationToken) {
         if (m_retries == m_maxRetries)
            throw new TimeoutException("Max retry attempts exceeded.");
         Int32 delay = Math.Min(m_delayMilliseconds * (Pow(2, ++m_retries) - 1) / 2, m_maxDelayMilliseconds);
         return Task.Delay(delay, cancellationToken);
      }
      private static Int32 Pow(Int32 number, Int32 exponent) {
         Int32 result = 1;
         for (int n = 0; n < exponent; n++) result *= number;
         return result;
      }
#if Usage
         ExponentialBackoff backoff = new ExponentialBackoff(3, 10, 100);
         retry:
         try {
            // ...
         }
         catch (TimeoutException) {
            await backoff.Delay(cancellationToken);
            goto retry;
         }
#endif
   }
}
