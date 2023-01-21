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
        this.SK = string.Empty;
        this.Name = string.Empty;
        this.State = "Pending";
        this.time = string.Empty;
        this.PK = $"OrgID#54321:UserID#12345";

    }

    public Todo(string id,string name, string State, string time, string userCred)
    {
        this.SK = id;
        this.Name = name;
        this.State = State;
        this.time = time;
        this.PK = $"OrgID#54321:UserID#12345";
    }

    public string SK { get; set; }

    public string Name { get; set; }
    public string State { get; set; }
    public string time { get; set; }
    public string PK { get; set; }

}
//public class User
//{
//    public User()
//    {
//        this.userId = string.Empty;
//        this.orgId = string.Empty;
//    }

//    public User(string id, string orgId)
//    {
//        this.userId = id;
//        this.orgId = orgId;
//    }

//    public string userId { get; set; }

//    public string orgId { get; set; }
//}


public class Functions
{   
    private static readonly AmazonDynamoDBClient _dynamoDbClient;

    public static string PK = "PK";
    public static string SK = "SK";
    public static string NAME = "Name";
    public static string STATE = "State";
    public static string TIME = "time";

    static Functions()
    {
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
    {   if(todo.SK != "" && todo.State != "")   //record to update
        {
            Dictionary<string, AttributeValue> todoAtt = new Dictionary<string, AttributeValue>(2);
            todoAtt.Add(PK, new AttributeValue(todo.PK));
            todoAtt.Add(SK, new AttributeValue(todo.SK));
            todoAtt.Add(STATE, new AttributeValue(todo.State));
            todoAtt.Add(NAME, new AttributeValue(todo.Name));
            todoAtt.Add(TIME, new AttributeValue(todo.time));
            return todoAtt;
        }
        Console.Write("in prodToDynamoDB func...");
        var todoId = Ulid.NewUlid();
        DateTime todo_dateTime = DateTime.Now;
        var todo_dt_str = todo_dateTime.ToString();
        Dictionary<string, AttributeValue> item = new Dictionary<string, AttributeValue>(2);
        item.Add(PK, new AttributeValue(todo.PK));
        item.Add(SK, new AttributeValue($"TodoId#{todoId}:TodoId#{todoId}"));
        item.Add(NAME, new AttributeValue(todo.Name));
        item.Add(STATE, new AttributeValue(todo.State));
        item.Add(TIME, new AttributeValue(todo_dt_str));
        return item;
    }

    public static Todo ProductFromDynamoDB(Dictionary<String, AttributeValue> items)
    {
        var todo = new Todo(items[SK].S, items[NAME].S, items[STATE].S, items[TIME].S, items[PK].S);

        return todo;
    }

    public static async Task AddTodo(Todo todo)
    {
        Console.Write("in AddTodo func, for child record creation...");
        await _dynamoDbClient.PutItemAsync("demoTable", ProductToDynamoDb(todo));
    }

    public static async Task UpdateTodo(Todo todo)
    {
        Console.Write("in updateTodo func for parent func updation...");
        
        await _dynamoDbClient.PutItemAsync("demoTable", ProductToDynamoDb(todo));
    }


    public static async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
    {
        var lContext = context;
        lContext.Logger.LogLine($"request context without serialization..., {request}");
        lContext.Logger.LogLine(JsonSerializableAttribute.GetCustomAttribute(request, CustomJsonSerializerContext.Default.APIGatewayProxyRequest));
        var reqMethod = request.RequestContext.Path;
        var reqPath = request.RequestContext.HttpMethod;

        lContext.Logger.LogLine($"request specs... {reqMethod}, {reqPath}");
        return request.HttpMethod switch
        {
            "GET" => await GetTodoHistoryHandler(request, context),

            "POST" => await PostFunctionHandler(request, context),

            "PUT" => await Functions.UpdateFunctionHandler(request, context),

            "DELETE" => await DeleteFunctionHandler(request, context),
        };
    }
    //get all todos, needs to be implemented when cognito is integrated
    private static async Task<APIGatewayProxyResponse> GetTodoHistoryHandler(APIGatewayProxyRequest request, ILambdaContext context)
    {
        var todoId = request?.PathParameters["todo"];

        if (todoId == null)
        {
            Console.Write("todoId is not provided...");
            return new APIGatewayProxyResponse
            {
                Body = "todoId is not provided.",
                StatusCode = (int)HttpStatusCode.BadRequest,
            };
        }
        var data = await _dynamoDbClient.ScanAsync(new ScanRequest()
        {
            TableName = "demoTable",
            FilterExpression = "PK = :PK and begins_with (SK , :SK)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":PK" , new AttributeValue { S = $"OrgID#54321:UserID#12345" } },
                { ":SK" , new AttributeValue { S = $"TodoId#{todoId}" } }
            }
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

    private static async Task<List<WriteRequest>> GetTodosById(ILambdaContext context, string todoId)
    {
        List<WriteRequest> todosPK = new List<WriteRequest>();

        var lContext = context;
        var data = await _dynamoDbClient.ScanAsync(new ScanRequest()
        {
            TableName = "demoTable",
            FilterExpression = "PK = :PK and begins_with (SK , :SK)",
            ExpressionAttributeValues= new Dictionary<string, AttributeValue>
            { 
                { ":PK" , new AttributeValue { S = $"OrgID#54321:UserID#12345" } },
                { ":SK" , new AttributeValue { S = $"TodoId#{todoId}" } }
            }
        });
        lContext.Logger.Log(JsonSerializer.Serialize(data.Items, CustomJsonSerializerContext.Default.ListDictionaryStringAttributeValue));
        foreach (Dictionary<string, AttributeValue> todo in data.Items)
        {
            //var todoPK = new Dictionary<string, AttributeValue>
            //{
            //    {"PK", new AttributeValue { S = todo["PK"].S } }
            //};
            //todosPK.Add(todoPK);

            var req = new WriteRequest
            {
                DeleteRequest = new DeleteRequest
                {
                    Key = new Dictionary<string, AttributeValue>
                    {
                        {"PK", new AttributeValue { S = todo[PK].S } },
                        {"SK", new AttributeValue { S = todo[SK].S } }
                    }
                }
            };
            
            todosPK.Add(req);
        }
        
        return todosPK;
    }
    //get a todo
    private static async Task<Todo> GetFunctionHandler(ILambdaContext context, string todoId)
    {
        var lContext = context;
        Console.Write($"in get funtionhandler to get targetTodo..., {todoId}");
        var data = await _dynamoDbClient.ScanAsync(new ScanRequest()
        {
            TableName = "demoTable",
            FilterExpression = "begins_with (PK,:PK) and SK = :SK",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue> {
            {":PK", new AttributeValue { S = $"OrgID#" }},
            {":SK", new AttributeValue { S = $"TodoId#{todoId}:TodoId#{todoId}" }}
            }
        });

        var todo = new Todo();
        
        
        lContext.Logger.LogLine($"request context without serialization..., {data.Items}");
        todo.SK = data.Items[0]["SK"].S;
        todo.Name = data.Items[0]["Name"].S;
        todo.State = data.Items[0]["State"].S;
        todo.time = data.Items[0]["time"].S;
        todo.PK = data.Items[0]["PK"].S;
        lContext.Logger.Log(JsonSerializer.Serialize(todo, CustomJsonSerializerContext.Default.Todo));
        return todo;
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
        //var userId = Ulid.NewUlid();
        //var orgId = Ulid.NewUlid(); //just dummy id's
        var newTodo = new Todo();
        newTodo.Name = todo.Name;
        //newTodo.userCred = $"OrgID#{orgId}:UserID#{userId}";
        await AddTodo(newTodo);

        return new APIGatewayProxyResponse
        {
            StatusCode = (int)HttpStatusCode.Created,
            Body = $"Created product with id {todo.SK}"
        };
    }

    private static async Task<APIGatewayProxyResponse> UpdateFunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
    {
        
        Console.Write("in update todo func");
        var todoId = request?.PathParameters["todo"];  // todoId from requestUrl
        //var lContext = context;
        Console.Write("in update todo func");
        if (todoId == null || request?.Body == null)
        {
            Console.Write("id is null...");
            return new APIGatewayProxyResponse
            {
                Body = "Please provide todoId and todoStatus.",
                StatusCode = (int)HttpStatusCode.BadRequest,
            };
        }
        //first update the parent record    
        Todo? todoStat = JsonSerializer.Deserialize(request.Body, CustomJsonSerializerContext.Default.Todo);

        if (todoStat != null)
        {
            Console.Write("updation todo not null...");
            var targetTodo = await GetFunctionHandler(context, todoId);

            //lContext.Logger.Log($"targetTodo from db... {targetTodo.Name}, {targetTodo.State}, {targetTodo.time}");
            var updationTodo = new Todo();
            updationTodo.SK = targetTodo.SK;
            updationTodo.State = todoStat.State;
            updationTodo.Name = targetTodo.Name;
            updationTodo.time = targetTodo.time;
            updationTodo.PK = targetTodo.PK;
            //lContext.Logger.LogLine(JsonSerializer.Serialize(updationTodo, CustomJsonSerializerContext.Default.Todo));
            await UpdateTodo(updationTodo);


            //then, update the child record
            //lContext.Logger.Log($"parent updation done, now going for child record creation...");

            updationTodo.SK = $"TodoId#{todoId}:State#{todoStat.State}";
            updationTodo.State = todoStat.State;
            updationTodo.Name = targetTodo.Name;
            updationTodo.time = DateTime.UtcNow.ToString();
            updationTodo.PK = targetTodo.PK;
            //lContext.Logger.LogLine(JsonSerializer.Serialize(updationTodo, CustomJsonSerializerContext.Default.Todo));
            await AddTodo(updationTodo);

        }

        return new APIGatewayProxyResponse
        {
            StatusCode = (int)HttpStatusCode.OK,
            Body = $"todo updated successfully."
        };
    }
    private static async Task<BatchWriteItemResponse> deleteBatch(ILambdaContext context, List<WriteRequest> todosPK)
    {   
        var lContext = context;
        lContext.Logger.Log("in batchDelete func with batchWrite requests for todos deletion...");
        
        lContext.Logger.Log(JsonSerializer.Serialize(todosPK, CustomJsonSerializerContext.Default.ListWriteRequest));


        lContext.Logger.Log($"dynamoDBClient err...{_dynamoDbClient}");
        var batchRes = await _dynamoDbClient.BatchWriteItemAsync(new BatchWriteItemRequest
        {
            RequestItems = new Dictionary<string, List<WriteRequest>>
            {
                {
                    "demoTable", todosPK
                },
            }
        });
        lContext.Logger.Log(JsonSerializer.Serialize(batchRes, CustomJsonSerializerContext.Default.BatchWriteItemResponse));

        return batchRes;
    }
    private static async Task<APIGatewayProxyResponse> DeleteFunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
    {
        var todoId = request?.PathParameters["todo"];   // todoId from requestUrl
        var lContext = context;
        if (todoId == null)
        {
            Console.Write("id is null...");
            return new APIGatewayProxyResponse
            {
                Body = "Please provide todoId.",
                StatusCode = (int)HttpStatusCode.BadRequest,
            };
        }
        //first get all the records with dedicated todoId    

        
        var targetTodos = await GetTodosById(context, todoId);

        var batchRes = await deleteBatch(context,targetTodos);
        lContext.Logger.Log(JsonSerializer.Serialize(batchRes, CustomJsonSerializerContext.Default.BatchWriteItemResponse));
        if ( ((int)batchRes.HttpStatusCode) == ((int)HttpStatusCode.OK))
        {
            return new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Body = $"todo deleted successfully."
            };
        }
        return new APIGatewayProxyResponse
        {
            StatusCode = (int)HttpStatusCode.InternalServerError,
            Body = $"todo deletion Failed."
        };
    }
}
