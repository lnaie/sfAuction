using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Microsoft.CSharp.RuntimeBinder;

namespace SFAuction.JsonRpc {
   public struct JsonRpcMessageId {
      public readonly Boolean IsString; // false
      public readonly Int64 Number;  // 0
      public readonly String String; // null
      public JsonRpcMessageId(Int64 id) {
         IsString = false;
         Number = id;
         String = null;
      }
      public JsonRpcMessageId(String id) {
         IsString = true;
         String = id;
         Number = 0;
      }

      public static implicit operator JsonRpcMessageId(Int64 id) => new JsonRpcMessageId(id);
      public static implicit operator JsonRpcMessageId(String id) => new JsonRpcMessageId(id);
   }

   public abstract class JsonRpcMessage {
      // http://www.jsonrpc.org/specification
      public const String Version = "2.0";
      internal static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

      public readonly JsonRpcMessageId Id;
      protected JsonRpcMessage(JsonRpcMessageId id) { Id = id; }
   }

   public sealed class JsonRpcRequest : JsonRpcMessage {
      public JsonRpcRequest(JsonRpcMessageId id, String method, IDictionary<String, Object> parameters = null) : base(id) {
         Method = method;
         ParametersAreNamed = true;
         NamedParameters = parameters;
      }
      public JsonRpcRequest(JsonRpcMessageId id, String method, IList<Object> parameters = null) : base(id) {
         Method = method;
         ParametersAreNamed = false;
         PositionalParameters = parameters;
      }
      public readonly String Method = null;
      public readonly Boolean ParametersAreNamed;
      public readonly IDictionary<String, Object> NamedParameters = null;
      public readonly IList<Object> PositionalParameters = null;
      public override String ToString() {
         // JMR: throw if method is null
         StringBuilder json = new StringBuilder("{ \"jsonrpc\": \"" + Version + "\"")
            .AppendId(Id)
            .Append($", \"method\": \"{Method}\"");
         if (ParametersAreNamed) json.AppendParameters(NamedParameters); else json.AppendParameters(PositionalParameters);
         json.Append("}");
         return json.ToString();
      }

      public static JsonRpcRequest Parse(String json) {
         var jsonObject = (IDictionary<String, Object>)Serializer.DeserializeObject(json);
         jsonObject = new Dictionary<String, Object>(jsonObject, StringComparer.OrdinalIgnoreCase);

         if ((String)jsonObject["jsonrpc"] != Version)
            throw new ArgumentException($"JsonRpc must be \"{Version}\"");

         String method = (String)jsonObject["method"];
         Object paramsObject = !jsonObject.ContainsKey("params") ? null : jsonObject["params"];
         Object[] positionalParameters = paramsObject as Object[];
         if (positionalParameters != null) {
            if (!jsonObject.ContainsKey("id"))
               return new JsonRpcRequest(null, method, positionalParameters);

            if (jsonObject["id"].GetType() == typeof(String))
               return new JsonRpcRequest((String)jsonObject["id"], method, positionalParameters);

            return new JsonRpcRequest(Convert.ToInt64(jsonObject["id"]), method, positionalParameters);
         }

         var namedParameters = paramsObject as IDictionary<String, Object>;
         if (!jsonObject.ContainsKey("id"))
            return new JsonRpcRequest(null, method, namedParameters);

         if (jsonObject["id"].GetType() == typeof(String))
            return new JsonRpcRequest((String)jsonObject["id"], method, namedParameters);

         return new JsonRpcRequest(Convert.ToInt64(jsonObject["id"]), method, namedParameters);
      }

      public Task<JsonRpcResponse> InvokeAsync(JavaScriptSerializer jsSerializer, Type type, CancellationToken token = default(CancellationToken))
         => InvokeAndWrapAsync(jsSerializer, type, null, token);
      public Task<JsonRpcResponse> InvokeAsync(JavaScriptSerializer jsSerializer, Object @object, CancellationToken token = default(CancellationToken))
         => InvokeAndWrapAsync(jsSerializer, @object.GetType(), @object, token);
      private async Task<JsonRpcResponse> InvokeAndWrapAsync(JavaScriptSerializer jsSerializer,
         Type type, Object instance, CancellationToken token = default(CancellationToken)) {
         JsonRpcResponse response = null;
         try {
            var jsonResult = await InvokeAsync(jsSerializer, type, instance, token);
            response = new JsonRpcResultResponse(Id, jsonResult);
         }
         catch (Exception e) {
            var parameters = String.Join(", ", ParametersAreNamed
                  ? NamedParameters.Select(kvp => kvp.Key + "=" + kvp.Value)
                  : PositionalParameters.Select(p => p.ToString()));

            response = new JsonRpcErrorResponse(Id,
               (e is ArgumentException) ? JsonRpcError.InvalidParameters : JsonRpcError.ReservedLow,
               e.Message,
               $"Type={e.TargetSite.DeclaringType}, Method={e.TargetSite}, Params={parameters}");
         }
         return response;
      }

      private async Task<String> InvokeAsync(JavaScriptSerializer jsSerializer, Type type, Object instance, CancellationToken token) {
         // Throw if method not found, parameter counts don't match(?), method requires arg not specified. Arg specified but not used?
         var methodInfo = type.GetTypeInfo().GetDeclaredMethod(Method);
         if (methodInfo == null) // Method not found
            throw new JsonRpcResponseErrorException(new JsonRpcErrorResponse(Id, JsonRpcError.MethodNotFound, Method));

         ParameterInfo[] methodParams = methodInfo.GetParameters();
         Boolean lastArgIsCancellationToken = (methodParams.Length == 0) ? false
            : methodParams[methodParams.Length - 1].ParameterType == typeof(CancellationToken);

         Object[] arguments = new Object[methodParams.Length];// + (lastArgIsCancellationToken ? 1 : 0)];
         Int32 passedArgs = methodParams.Length - (lastArgIsCancellationToken ? 1 : 0);
         for (Int32 arg = 0; arg < passedArgs; arg++) {
            Object argValue;
            if (!ParametersAreNamed) {
               argValue = PositionalParameters[arg];
            } else {
               if (!NamedParameters.TryGetValue(methodParams[arg].Name, out argValue)) {
                  // Required argument not found; throw
                  throw new ArgumentException($"Missing required argument: {methodParams[arg].Name}");
               }
            }
            arguments[arg] = jsSerializer.ConvertToType(argValue, methodParams[arg].ParameterType);
         }
         if (lastArgIsCancellationToken) arguments[arguments.Length - 1] = token;  // Add the CancellationToken as the last parameter
                                                                                   // JMR: What if passed arg is not required by method? throw? flag to ignore?
         Object result = methodInfo.Invoke(instance, arguments);
         Task task = result as Task;
         if (task != null) {
            await task;
            try {
               result = ((dynamic)task).GetAwaiter().GetResult();
            }
            catch (RuntimeBinderException /* void-returning Task */) { result = null; }
         }
         return jsSerializer.Serialize(result);
      }
   }

   public abstract class JsonRpcResponse : JsonRpcMessage {
      protected JsonRpcResponse(JsonRpcMessageId id) : base(id) { }
      public static JsonRpcResponse Parse(String json) {
         var jsonObject = (IDictionary<String, Object>)Serializer.DeserializeObject(json);
         if ((String)jsonObject["jsonrpc"] != Version)
            throw new ArgumentException($"JsonRpc must be \"{Version}\"");

         if (jsonObject.ContainsKey("result")) {
            String jsonResult = Serializer.Serialize(jsonObject["result"]);   // Turn .NET objects back into JSON
            if (jsonObject["id"].GetType() == typeof(String))
               return new JsonRpcResultResponse((String)jsonObject["id"], jsonResult);
            return new JsonRpcResultResponse(Convert.ToInt64(jsonObject["id"]), jsonResult);
         }

         if (jsonObject.ContainsKey("error")) {
            Int32 error = (Int32)jsonObject["error"];
            String message = (String)jsonObject["message"];
            String data = !jsonObject.ContainsKey("data") ? null : (String)jsonObject["data"];
            if (jsonObject["id"].GetType() == typeof(String))
               return new JsonRpcErrorResponse((String)jsonObject["id"], error, message, data);
            return new JsonRpcErrorResponse(Convert.ToInt64(jsonObject["id"]), error, message, data);
         }
         throw new ArgumentException("Response must contain 'result' or 'error'");
      }
      public abstract String JsonResult { get; }
   }

   public sealed class JsonRpcResultResponse : JsonRpcResponse {
      private readonly String m_jsonResult;
      public JsonRpcResultResponse(JsonRpcMessageId id, String jsonResult) : base(id) {
         m_jsonResult = jsonResult;
      }
      public override String JsonResult => m_jsonResult;

      public override String ToString() {
         StringBuilder json = new StringBuilder($"{{\"jsonrpc\": \"{Version}\"")
            .AppendId(Id)
            .Append($", \"result\": {m_jsonResult}")
            .Append("}");
         return json.ToString();
      }
   }

   public enum JsonRpcError : Int32 {
      Parse = -32700,            // Parse error; Invalid JSON was received by the server.
      InvalidRequest = -32600,   // Invalid Request; The JSON sent is not a valid Request object.
      MethodNotFound = -32601,   // Method not found; The method does not exist / is not available.
      InvalidParameters = -32602,// Invalid params; Invalid method parameter(s).
      Internal = -32603,         // error; Internal JSON-RPC error.
      ReservedLow = -32099,      // -32000 to -32099; Server error; Reserved for implementation-defined server-errors.
      ReserverHigh = -32000
   }

   public sealed class JsonRpcErrorResponse : JsonRpcResponse {
      public JsonRpcErrorResponse(JsonRpcMessageId id, JsonRpcError error, String message, String data = null) : base(id) {
         Error = error;
         Message = message;
         Data = data;
      }
      public JsonRpcErrorResponse(JsonRpcMessageId id, Int32 error, String message, String data = null) : this(id, (JsonRpcError)error, message, data) { }

      public readonly JsonRpcError Error = 0;
      public readonly String Message;
      public readonly String Data;
      public override String ToString() {
         StringBuilder json = new StringBuilder($"{{\"jsonrpc\": \"{Version}\"")
            .AppendId(Id)
            .Append($", \"error\": {(Int32)Error}"); // 'error'?
         if (Message != null) json.Append($", \"message\": \"{Message}\"");
         if (Data != null) json.Append($", \"data\": \"{Data}\"");
         json.Append("}");
         return json.ToString();
      }
      public override String JsonResult { get { throw new JsonRpcResponseErrorException(this); } }
   }

   public sealed class JsonRpcResponseErrorException : Exception {
      public readonly JsonRpcErrorResponse Response;
      public JsonRpcResponseErrorException(JsonRpcErrorResponse response)
         : base(response.Message) { Response = response; }

   }


   internal static class JsonRpcExtensions {
      // JSON ECMA Specification (section 9): http://www.ecma-international.org/publications/files/ECMA-ST/ECMA-404.pdf
      public static String JsonEncode(this string stringValue) {
         var sb = new StringBuilder();
         for (int c = 0; c < stringValue.Length; c++) {
            // Escape special characters
            if (c_escapeChars.IndexOf(stringValue[c]) >= 0) sb.Append("\\");
            sb.Append(stringValue[c]);
         }
         return sb.ToString();
      }
      private const String c_escapeChars = "\"\\/\b\f\n\r\t";
      public static String JsonDecode(this String jsonString) {
         var sb = new StringBuilder();
         for (int c = 0; c < jsonString.Length; c++) {
            Char ch = jsonString[c];
            if (ch == '\\' && (c < jsonString.Length - 2) && c_escapeChars.IndexOf(jsonString[c + 1]) >= 0) continue;   // Skip the '\\'
            sb.Append(ch);
         }
         return sb.ToString();
      }

      internal static StringBuilder AppendParameters(this StringBuilder json, IEnumerable<KeyValuePair<String, Object>> parameters) {
         if (parameters != null) {
            json.Append(", \"params\": {");
            Boolean firstParam = true;
            foreach (var p in parameters) {
               if (!firstParam) json.Append(", "); else firstParam = false;
               json.Append($"\"{p.Key}\": ").AppendJson(p.Value);
            }
            json.Append("}"); // Close the parameters OBJECT
         }
         return json;
      }
      internal static StringBuilder AppendParameters(this StringBuilder json, IEnumerable<Object> parameters) {
         if (parameters != null) {
            json.Append(", \"params\": [");
            Boolean firstParam = true;
            foreach (var p in parameters) {
               if (!firstParam) json.Append(", "); else firstParam = false;
               json.AppendJson(p);
            }
            json.Append("]"); // Close the parameters ARRAY
         }
         return json;
      }
      internal static StringBuilder AppendId(this StringBuilder json, JsonRpcMessageId id) {
         if (id.IsString) {
            if (id.String == null) return json;
            return json.Append($", \"id\": \"{id.String}\"");
         }
         return json.Append($", \"id\": {id.Number.ToString()}");
      }
#if true
      internal static StringBuilder AppendJson(this StringBuilder json, Object o) {
         JsonRpc.JsonRpcMessage.Serializer.Serialize(o, json); return json;
      }
#endif
   }

#if false
   public sealed class JsonRpcHttpIO /*, IDisposable */{
      public static async Task StartServerAsync(String port, Int32 maxRequestSizeInBytes, Func<JsonRpcRequest, Task<JsonRpcResponse>> serviceClientRequestAsync, CancellationToken ct = default(CancellationToken)) {
         using (var server = new HttpListener()) {
            try {
               // From Admin CMD:
               // netsh http add urlacl url=http://*:8080/ user=MicrosoftAccount\JeffRichter@live.com listen=yes
               server.Prefixes.Add($"http://*:{port}/");
               server.Start();
            }
            catch (Exception e) {
               Console.WriteLine(e);
            }

            while (true) {
               ct.ThrowIfCancellationRequested();
               HttpListenerContext context = await server.GetContextAsync();

               // Read request from client:
               String jsonRpc = context.Request.QueryString["jsonrpc"];

               // Process request to get response:
               JsonRpcRequest request = JsonRpcRequest.Parse(jsonRpc);
               JsonRpcResponse response = await serviceClientRequestAsync(request);

               // Write response to client:
               var data = Encoding.UTF8.GetBytes(response.ToString());
               using (context.Response) {
                  await context.Response.OutputStream.WriteAsync(data, 0, data.Length);
               }
            }
         }
      }

      public readonly String Url;
      public JsonRpcHttpIO(String url) { Url = url; }
      public async Task<JsonRpcResponse> SendAsync(JsonRpcRequest request, CancellationToken cancellationToken) {
         using (var client = new HttpClient()) {
            // Send request to server:
            var response = await client.GetStringAsync(Url + $"?jsonrpc={request.ToString()}").WithCancellation(cancellationToken).ConfigureAwait(false);

            // Get response from server:
            return JsonRpcResponse.Parse(response);
         }
      }
   }
#endif

   public sealed class JsonRpcPipeIO /*, IDisposable */{
      public static async Task StartServerAsync(String pipeName, Int32 maxRequestSizeInBytes, Func<JsonRpcRequest, Task<JsonRpcResponse>> serviceClientRequestAsync, CancellationToken ct = default(CancellationToken)) {
         while (true) {
            ct.ThrowIfCancellationRequested();
            using (var pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, -1,
               PipeTransmissionMode.Message,
               PipeOptions.Asynchronous | PipeOptions.WriteThrough)) {
               await Task.Factory.FromAsync(pipe.BeginWaitForConnection, pipe.EndWaitForConnection, null);
               //await pipe.WaitForConnectionAsync();

               using (pipe) {
                  // Read request from client:
                  Byte[] data = new Byte[maxRequestSizeInBytes];
                  Int32 bytesRead = await pipe.ReadAsync(data, 0, data.Length);

                  // Process request to get response:
                  JsonRpcRequest request = JsonRpcRequest.Parse(Encoding.UTF8.GetString(data, 0, bytesRead));
                  JsonRpcResponse response = await serviceClientRequestAsync(request);

                  // Write response to client:
                  data = Encoding.UTF8.GetBytes(response.ToString());
                  await pipe.WriteAsync(data, 0, data.Length);
               }
            }
         }
      }

      public readonly String ServerName;
      public readonly String PipeName;
      public readonly Int32 DefaultMaxResponseSizeInBytes;
      public JsonRpcPipeIO(String serverName, String pipeName, Int32 defaultMaxResponseSizeInBytes) {
         ServerName = serverName;
         PipeName = pipeName;
         DefaultMaxResponseSizeInBytes = defaultMaxResponseSizeInBytes;
      }
#if false
         private NamedPipeClientStream m_pipe;
         //public void Dispose() { m_pipe.Dispose(); }
         public async Task ConnectAsync(String serverName) {
            m_pipe = new NamedPipeClientStream(serverName, PipeName,
               PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.WriteThrough);
//            await m_pipe.ConnectAsync(); // Must Connect before setting ReadMode
  //          m_pipe.ReadMode = PipeTransmissionMode.Message;
         }
#endif
      public async Task<JsonRpcResponse> SendAsync(JsonRpcRequest request, Int32 maxResponseSizeInBytes = 0) {
         using (var pipe = new NamedPipeClientStream(ServerName, PipeName,
            PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.WriteThrough)) {
            /*await */
            pipe.Connect/*Async*/(); // Must Connect before setting ReadMode
            pipe.ReadMode = PipeTransmissionMode.Message;

            // Send request to server:
            var data = Encoding.UTF8.GetBytes(request.ToString());
            await pipe.WriteAsync(data, 0, data.Length);

            // Get response from server:
            data = new Byte[maxResponseSizeInBytes == 0 ? DefaultMaxResponseSizeInBytes : maxResponseSizeInBytes];
            var bytesRead = await pipe.ReadAsync(data, 0, data.Length);
            return JsonRpcResponse.Parse(Encoding.UTF8.GetString(data, 0, bytesRead));
         }
      }
   }
}