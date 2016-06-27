var JsonRpcHelperResult = function (retValue)
{
    this.retValue = retValue;
    this.isSuccess = false;
    this.errorMsg = null;
    this.returnResult = JSON.parse(retValue);
    if(retValue.indexOf("result") != -1)
    {
        this.isSuccess = true;
    }

    this.isSuccessResponse = function () {
        return this.isSuccess;
    };

    this.getErrorMsg = function() {
        return this.returnResult.message;
    };

    this.getResult = function () {
        return this.returnResult.result;
    }

    this.getRequestId = function() {
        return this.returnResult.id;
    };
}

var JsonRpcHelper = function (url) {
    var hostUrl = url;
    var requestId = Math.floor(Math.random() * 9999);
    var request = null;
    var versionNumber = "2.0";

    this.sendJsonRpcRequest = function (methodName, parameters,requestType,responseSuccessCallBack) {
        parameters = typeof parameters !== 'undefined' ? parameters : null;
        this.request = {
            'jsonrpc': versionNumber,
            'method': methodName,
            'id': requestId,
            'params': parameters
        };
        var requestString = JSON.stringify(this.request);
        var jsonRpcRequest = "jsonrpc=" + requestString;
        $.ajax({
            url: hostUrl,
            data: jsonRpcRequest,
            type: requestType,
            success: function (retValue) {
                var rpcResultHelper = new JsonRpcHelperResult(retValue);
                if (rpcResultHelper.isSuccessResponse())
                    responseSuccessCallBack(rpcResultHelper.getResult());
                else
                {
                    var error = rpcResultHelper.getErrorMsg();
                    alert(error);
                }
             }
        });
    }
}