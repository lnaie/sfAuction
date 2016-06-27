namespace SfBayPounder {
   using System;
   internal sealed class TimeoutHelper {
      private readonly DateTime deadline;
      private readonly string timeoutMessage = "Operation timed out";

      public TimeoutHelper(TimeSpan timeout) {
         deadline = (timeout == TimeSpan.MaxValue) ? DateTime.MaxValue : DateTime.Now.Add(timeout);
      }

      public bool IsExpired => this.GetRemainingTime() > TimeSpan.Zero;

      public TimeSpan GetRemainingTime() {
         if (deadline == DateTime.MaxValue) {
            return TimeSpan.MaxValue;
         }

         TimeSpan remaining = deadline - DateTime.Now;
         return remaining <= TimeSpan.Zero ? TimeSpan.Zero : remaining;
      }

      public void ThrowIfExpired() {
         if (this.GetRemainingTime() == TimeSpan.Zero) {
            throw new TimeoutException(timeoutMessage);
         }
      }
   }
}
