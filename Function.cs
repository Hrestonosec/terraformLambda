using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using System.Net;
using System.Runtime.Intrinsics.X86;
using System.Text.Json;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace getChats;

public class Function
{
    private readonly AmazonDynamoDBClient _client;
    private readonly DynamoDBContext _context;

    public Function()
    {
        _client = new AmazonDynamoDBClient();
        _context = new DynamoDBContext(_client);
    }

    public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
    {
        var userId = request.QueryStringParameters["userId"];

        //Запит на отримання кількості результатів на сторінку
        request.QueryStringParameters.TryGetValue("pageSize", out var pageSizeString);
        int.TryParse(pageSizeString, out var pageSize);
        pageSize = pageSize == 0 ? 50 : pageSize;

        request.QueryStringParameters.TryGetValue("nextChatId", out var nextChatId);

        //Отримання всі чатів вказаного користувача і токена наступної сторінки
        (List<Chat> chats, string nextPageToken) = await GetAllChats(userId, pageSize, nextChatId);

        Console.WriteLine("Just testing CI/CD");
        Console.WriteLine("Just testing CI/CD");
        Console.WriteLine("Just testing CI/CD");

        var result = chats.Select(chat => new GetAllChatsResponseItem(chat)).ToList();

        return new APIGatewayProxyResponse
        {
            StatusCode = (int)HttpStatusCode.OK,
            Headers = new Dictionary<string, string>
        {
            { "Content-Type", "application/json" },
            { "Access-Control-Allow-Origin", "*" }
        },
            Body = JsonSerializer.Serialize(new
            {
                Chats = result,
                NextChatId = nextPageToken
            })
        };
    }

    private async Task<(List<Chat>, string)> GetAllChats(string userId, int pageSize, string nextChatId)
    {
        var user1Query = new QueryOperationConfig
        {
            IndexName = "user1-updatedDt-index",
            KeyExpression = new Expression
            {
                ExpressionStatement = "user1 = :user" + (string.IsNullOrEmpty(nextChatId) ? "" : " AND chatId > :nextChatId"),
                ExpressionAttributeValues = new Dictionary<string, DynamoDBEntry>
            {
                { ":user", userId }
            }
            },
            Limit = pageSize
        };

        if (!string.IsNullOrEmpty(nextChatId))
        {
            user1Query.KeyExpression.ExpressionAttributeValues[":nextChatId"] = nextChatId;
        }

        var user1Search = _context.FromQueryAsync<Chat>(user1Query);
        var user1Results = await user1Search.GetNextSetAsync();

        var user2Query = new QueryOperationConfig
        {
            IndexName = "user2-updatedDt-index",
            KeyExpression = new Expression
            {
                ExpressionStatement = "user2 = :user" + (string.IsNullOrEmpty(nextChatId) ? "" : " AND chatId > :nextChatId"),
                ExpressionAttributeValues = new Dictionary<string, DynamoDBEntry>
            {
                { ":user", userId }
            }
            },
            Limit = pageSize
        };

        if (!string.IsNullOrEmpty(nextChatId))
        {
            user2Query.KeyExpression.ExpressionAttributeValues[":nextChatId"] = nextChatId;
        }

        var user2Search = _context.FromQueryAsync<Chat>(user2Query);
        var user2Results = await user2Search.GetNextSetAsync();

        //Поєднання результатів із запитів по user 1 і 2, і обрізання за розміром сторінки
        var combinedResults = user1Results.Concat(user2Results)
                                          .OrderBy(chat => chat.UpdateDt)
                                          .Take(pageSize)
                                          .ToList();

        //Отримання токена наступної сторінки
        string nextPageToken = combinedResults.LastOrDefault()?.ChatId;

        return (combinedResults, nextPageToken);
    }



    [DynamoDBTable("chats-db")]
    public class Chat
    {
        [DynamoDBHashKey("chatId")]
        public string ChatId { get; set; }

        [DynamoDBRangeKey("updatedDt")]
        public long UpdateDt { get; set; }

        [DynamoDBProperty("user1")]
        public string User1 { get; set; }

        [DynamoDBProperty("user2")]
        public string User2 { get; set; }

        public override string? ToString()
        {
            return $"{ChatId} {UpdateDt} {User1} {User2}";
        }
    }

    public class GetAllChatsResponseItem
    {
        public string ChatId { get; set; }
        public long UpdateDt { get; set; }
        public string User1 { get; set; }
        public string User2 { get; set; }

        public GetAllChatsResponseItem()
        {

        }
        public GetAllChatsResponseItem(Chat chat)
        {
            this.ChatId = chat.ChatId;
            this.UpdateDt = chat.UpdateDt;
            this.User1 = chat.User1;
            this.User2 = chat.User2;
        }
    }
}