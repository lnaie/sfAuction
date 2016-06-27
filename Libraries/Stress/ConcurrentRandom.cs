//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace SfBayPounder {
   using System;

   /// <summary>
   /// The intention of this class is to (efficiently) behave like Random, even when
   /// multiple threads are calling its methods concurrently
   /// </summary>
   public static class ConcurrentRandom {
      /// <summary>
      /// This is an app-domain wide Random, used to seed the thread-local Random's
      /// </summary>
      private static Random Global = new Random();

      /// <summary>
      /// This Random is local to a thread
      /// </summary>
      [ThreadStatic]
      private static Random Local;

      /// <summary>
      /// Returns an int between 0 and maxValue-1, inclusive.
      /// </summary>
      /// <param name="maxValue">The number of different types of outputs</param>
      /// <returns>An int</returns>
      public static int Next(int maxValue) {
         if (Local == null) {
            lock (Global) {
               int seed = Global.Next();
               Local = new Random(seed);
            }
         }

         return Local.Next(maxValue);
      }

      /// <summary>
      /// Returns an uniform random number between 0.0 and 1.0
      /// </summary>
      /// <returns>A double</returns>
      public static double NextDouble() {
         if (Local == null) {
            lock (Global) {
               int seed = Global.Next();
               Local = new Random(seed);
            }
         }
         return Local.NextDouble();
      }
   }
}