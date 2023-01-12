using System.Text.Json.Serialization;
using Amazon.Lambda.APIGatewayEvents;
//using native_AOT_lambda.Shared.Models;
using serverless_native_AOT_Test;

namespace native_AOT_lambda;

[JsonSerializable(typeof(APIGatewayProxyRequest))]
[JsonSerializable(typeof(APIGatewayProxyResponse))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(Todo))]
[JsonSerializable(typeof(List<Todo>))]
//[JsonSerializable(typeof(ProductWrapper))]
public partial class CustomJsonSerializerContext : JsonSerializerContext
{
}

