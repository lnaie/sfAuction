using System;
using System.Threading;
using System.Threading.Tasks;

namespace Richter.Utilities {
   public static class TaskExtensions {
      public static Task WithCancellation(this Task originalTask, CancellationToken cancellationToken) {
         if (originalTask.IsCompleted || !cancellationToken.CanBeCanceled) return originalTask;
         if (cancellationToken.IsCancellationRequested)
            return new Task(() => { }, cancellationToken);
         return originalTask.WithCancellationHelper(cancellationToken);
      }

      private static async Task WithCancellationHelper(this Task originalTask, CancellationToken cancellationToken) {
         // Create a Task that completes when the CancellationToken is canceled
         var cancelTask = new TaskCompletionSource<Boolean>();

         // When the CancellationToken is canceled, complete the Task
         using (cancellationToken.Register(() => cancelTask.TrySetResult(true))) {
            // Create another Task that completes when the original Task or when the CancellationToken's Task
            Task any = await Task.WhenAny(originalTask, cancelTask.Task);
            if (any == cancelTask.Task) throw new OperationCanceledException(cancellationToken);
         }
         // await original task (synchronously); if it failed, awaiting it 
         // throws 1st inner exception instead of AggregateException
         await originalTask;
      }


      public static Task<TResult> WithCancellation<TResult>(this Task<TResult> originalTask, CancellationToken cancellationToken) {
         if (originalTask.IsCompleted || !cancellationToken.CanBeCanceled) return originalTask;
         if (cancellationToken.IsCancellationRequested)
            return new Task<TResult>(() => default(TResult), cancellationToken);
         return originalTask.WithCancellationHelper(cancellationToken);
      }

      private static async Task<T> WithCancellationHelper<T>(this Task<T> originalTask, CancellationToken cancellationToken) {
         // Create a Task that completes when the CancellationToken is canceled
         var cancelTask = new TaskCompletionSource<Boolean>();

         // When the CancellationToken is canceled, complete the Task
         using (cancellationToken.Register(() => cancelTask.TrySetResult(true))) {
            // Create another Task that completes when the original Task or when the CancellationToken's Task
            Task any = await Task.WhenAny(originalTask, cancelTask.Task);
            if (any == cancelTask.Task) throw new OperationCanceledException(cancellationToken);
         }
         // await original task (synchronously); if it failed, awaiting it 
         // throws 1st inner exception instead of AggregateException
         return await originalTask;
      }
   }
}