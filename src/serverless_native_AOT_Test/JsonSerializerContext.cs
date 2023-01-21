using System.Text.Json.Serialization;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.APIGatewayEvents;
//using native_AOT_lambda.Shared.Models;
using serverless_native_AOT_Test;

namespace native_AOT_lambda;

[JsonSerializable(typeof(APIGatewayProxyRequest))]
[JsonSerializable(typeof(APIGatewayProxyResponse))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(List<Dictionary<string, AttributeValue>>))]
[JsonSerializable(typeof(BatchWriteItemResponse))]
[JsonSerializable(typeof(List<BatchWriteItemRequest>))]
[JsonSerializable(typeof(Todo))]
[JsonSerializable(typeof(List<Todo>))]      //JsonSerializableAttribute
//[JsonSerializable(typeof(ProductWrapper))]
public partial class CustomJsonSerializerContext : JsonSerializerContext
{
}

