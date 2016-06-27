using JsonRpc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static System.Console;

public static class Program {
   public static void Main() {
      var warning = JsonRpcHttpIO.StartServerAsync("8080", 1000,
         request => request.Invoke(new ServerSide.Calc()),
         CancellationToken.None);
      //const String pipeName = "AppTypeInstanceName_ServiceTypeInstanceName";
      //var warning = JsonRpcPipeIO.StartServerAsync(pipeName, 1000, ServiceJsonRequestAsync, CancellationToken.None);

      Task.Run(async () => {
         String serverName = "localHost"; // JMR: Replace with service's IP
         //var client = new JsonRpcPipeIO(serverName, pipeName, 1000);
         var client = new JsonRpcHttpIO(serverName, "8080", 1000);
         var proxy = new ClientSide.CalcProxy(client);
         WriteLine(await proxy.AddAsync(1, 2));
         WriteLine(await proxy.SubtractAsync(5, 3));
      }).Wait();
   }
}

namespace ClientSide {
   internal sealed class CalcProxy {
      private readonly JsonRpcHttpIO m_jsonRpcHttpIO;
      public CalcProxy(JsonRpcHttpIO jsonRpcHttpIO) {
         m_jsonRpcHttpIO = jsonRpcHttpIO;
      }
      public async Task<Int32> AddAsync(Int32 arg1, Int32 arg2) {
         var parameters = new Dictionary<String, Object> {
            [nameof(arg1)] = arg1,[nameof(arg2)] = arg2
         };
         return await SendAsync(nameof(AddAsync), parameters);
      }
      public async Task<Int32> SubtractAsync(Int32 arg1, Int32 arg2) {
         var parameters = new Object[] { arg1, arg2 };
         return await SendAsync(nameof(SubtractAsync), parameters);
      }
      private async Task<dynamic> SendAsync(String method, IDictionary<String, Object> parameters) {
         var request = new JsonRpcRequest(Guid.NewGuid().ToString(), method, parameters);
         var r = await m_jsonRpcHttpIO.SendAsync(request);
         return r.Result;
      }
      private async Task<dynamic> SendAsync(String method, IList<Object> parameters) {
         var request = new JsonRpcRequest(Guid.NewGuid().ToString(), method, parameters);
         var r = await m_jsonRpcHttpIO.SendAsync(request);
         return r.Result;
      }
   }
}

namespace ServerSide {
   internal sealed class Calc {
      public Task<Int32> AddAsync(Int32 arg1, Int32 arg2) => Task.FromResult(arg1 + arg2);
      public Task<Int32> SubtractAsync(Int32 arg1, Int32 arg2) => Task.FromResult(arg1 - arg2);
      public Task<Int32> MultipleAsync(Int32 arg1, Int32 arg2) => Task.FromResult(arg1 * arg2);
      public Task<Int32> DivideAsync(Int32 arg1, Int32 arg2) => Task.FromResult(arg1 / arg2);
   }
}
