using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.APIGatewayEvents;
using System.Text.Json.Serialization;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.DynamoDBv2.Model;
using System.Xml.Linq;
using Amazon.DynamoDBv2;
using native_AOT_lambda;
using Amazon.XRay.Recorder.Handlers.AwsSdk;

namespace serverless_native_AOT_Test;

public class Todo
{
    public Todo()
    {
        this.Id = string.Empty;
        this.Name = string.Empty;
        this.State = "Pending";
        this.time = string.Empty;
    }

    public Todo(string id,string name, string State, string time)
    {
        this.Id = id;
        this.Name = name;
        this.State = State;
        this.time = time;
    }

    public string Id { get; set; }

    public string Name { get; set; }
    public string State { get; set; }
    public string time { get; set; }
}


public class Functions
{   //APIGatewayProxyRequest //APIGatewayProxyResponse

    private static readonly AmazonDynamoDBClient? _dynamoDbClient;
    public static string PK = "PK";
    public static string NAME = "Name";
    public static string STATE = "State";
    public static string TIME = "time";

    static Functions()
    {
        Console.Write("In functionHandler constructor...");
        AWSSDKHandler.RegisterXRayForAllServices();
        _dynamoDbClient = new AmazonDynamoDBClient();
    }
    private static async Task Main()
    {
        Func<APIGatewayProxyRequest, ILambdaContext, Task<APIGatewayProxyResponse>> handler = FunctionHandler;
        await LambdaBootstrapBuilder.Create(handler, new SourceGeneratorLambdaJsonSerializer<CustomJsonSerializerContext>(options =>
        {
            options.PropertyNameCaseInsensitive = true;
        }))
            .Build()
            .RunAsync();
    }
    //Helper functions
    public static Dictionary<string, AttributeValue> ProductToDynamoDb(Todo todo)
    {
        Console.Write("in prodToDynamoDB func...");
        var id = Ulid.NewUlid();
        DateTime todo_dateTime = DateTime.Now;
        var todo_dt_str = todo_dateTime.ToString();
        Dictionary<string, AttributeValue> item = new Dictionary<string, AttributeValue>(2);
        item.Add(PK, new AttributeValue($"TodoId#{id}:TodoId#{id}"));
        item.Add(NAME, new AttributeValue(todo.Name));
        item.Add(STATE, new AttributeValue("Pending"));
        item.Add(TIME, new AttributeValue(todo_dt_str));
        return item;
    }

    public static Todo ProductFromDynamoDB(Dictionary<String, AttributeValue> items)
    {
        var todo = new Todo(items[PK].S, items[NAME].S, items[STATE].S, items[TIME].S);

        return todo;
    }

    public static async Task AddTodo(Todo todo)
    {
        Console.Write("in putProduct func...");
        await _dynamoDbClient.PutItemAsync("demoTable", ProductToDynamoDb(todo));
    }


    public static async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
    {
        var lContext = context;
        lContext.Logger.LogLine($"request context without serialization..., {request}");
        lContext.Logger.LogLine(JsonSerializer.Serialize(request, CustomJsonSerializerContext.Default.APIGatewayProxyRequest));
        var reqMethod = request.RequestContext.Path;
        var reqPath = request.RequestContext.HttpMethod;

        lContext.Logger.LogLine($"request specs... {reqMethod}, {reqPath}");
        return request.HttpMethod switch
        {
            "GET" => await GetFunctionHandler(request, context),

            "POST" => await PostFunctionHandler(request, context),
        };
    }

    private static async Task<APIGatewayProxyResponse> GetFunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
    {
        var data = await _dynamoDbClient.ScanAsync(new ScanRequest()
        {
            TableName = "demoTable"
        });

        var todos = new List<Todo>();
        var lContext = context;
        lContext.Logger.LogLine($"request context without serialization..., {data.Items}");
        foreach (var item in data.Items)
        {
            todos.Add(ProductFromDynamoDB(item));
        }

        var response = new APIGatewayProxyResponse
        {
            StatusCode = (int)HttpStatusCode.OK,
            Body = JsonSerializer.Serialize(todos, CustomJsonSerializerContext.Default.ListTodo),
            Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
        };

        return response;
    }

    private static async Task<APIGatewayProxyResponse> PostFunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
    {
        var todo = JsonSerializer.Deserialize(request.Body, CustomJsonSerializerContext.Default.Todo);
        context.Logger.Log($"test prod... {todo}");
        if (todo == null)
        {
            Console.Write("product is null...");
            return new APIGatewayProxyResponse
            {
                Body = "Product ID in the body does not match path parameter",
                StatusCode = (int)HttpStatusCode.BadRequest,
            };
        }

        await AddTodo(todo);

        return new APIGatewayProxyResponse
        {
            StatusCode = (int)HttpStatusCode.Created,
            Body = $"Created product with id {todo.Id}"
        };
    }

    //private static async Task<APIGatewayProxyResponse> UpdateFunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
    //{
    //    var todoId = request.QueryStringParameters["todoId"];   // todoId from requestUrl
    //    if (todoId == null || request.Body == null)
    //    {
    //        Console.Write("id is null...");
    //        return new APIGatewayProxyResponse
    //        {
    //            Body = "Please provide todoId and todoStatus.",
    //            StatusCode = (int)HttpStatusCode.BadRequest,
    //        };
    //    }

    //    var todoStat = JsonSerializer.Deserialize(request.Body, CustomJsonSerializerContext.Default.Todo);

    //    var updatedTodo = new Todo()
    //    return new APIGatewayProxyResponse
    //    {
    //        StatusCode = (int)HttpStatusCode.OK,
    //        Body = $"Created product with id {todoId}"
    //    };
    //}
}
