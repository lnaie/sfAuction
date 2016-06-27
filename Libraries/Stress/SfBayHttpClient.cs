using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SfBayPounder {
   internal sealed class SfBayHttpClient {
      private readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30.0);
      private readonly HttpClient hc;

      public SfBayHttpClient(string baseAddress) {
         BaseAddress = baseAddress;
         this.hc = new HttpClient { BaseAddress = new Uri(baseAddress) };
      }

      public static string BaseAddress { get; set; }

      public async Task<string> SubmitRequestAsync(string request) {
         var timeoutHelper = new TimeoutHelper(DefaultTimeout);
         string res = string.Empty;
         bool success = false;

         while (!success || !timeoutHelper.IsExpired) {
            try {
               var response = await hc.GetAsync(request);
               var content = response.Content;
               if (content != null) {
                  res = await content.ReadAsStringAsync();
               }

               success = true;
            }
            catch (Exception) {
               // swallow
            }
         }
         Console.WriteLine("Response = {0}", res);
         return res;
      }
   }
}
