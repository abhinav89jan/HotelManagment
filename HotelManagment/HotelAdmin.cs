using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.S3;
using HotelManagment.Models;
using HttpMultipartParser;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Text;
using System.Text.Json;

[assembly:LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]
namespace HotelManagment
{
    public class HotelAdmin
    {
        public async Task<APIGatewayProxyResponse> GetHotelList(APIGatewayProxyRequest request, ILambdaContext context)
        {
            var response = new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Headers = new Dictionary<string, string>()
            };

            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Headers", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "OPTIONS,GET");
            response.Headers.Add("Content-Type", "application/json");

            var token = request.QueryStringParameters["token"];
            var tokendetail = new JwtSecurityToken(token);
            var userId = tokendetail.Claims.FirstOrDefault(x => x.Type == "sub");

            var regionName = Environment.GetEnvironmentVariable("AWS_REGION");
            var dbclient = new AmazonDynamoDBClient(RegionEndpoint.GetBySystemName(regionName));

            using var dbcontext = new DynamoDBContext(dbclient);
            var hotels = await dbcontext.ScanAsync<Hotels>(new[] { new ScanCondition("userId", ScanOperator.Equal, userId) }).GetRemainingAsync();

            response.Body = JsonSerializer.Serialize(hotels);
            return response;
        }

        public async Task<APIGatewayProxyResponse> AddHotel(APIGatewayProxyRequest request, ILambdaContext context)
        {
            var response = new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Headers = new Dictionary<string, string>()
            };

            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Headers", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "OPTIONS,POST");

            var bodyContent = request.IsBase64Encoded ?
                Convert.FromBase64String(request.Body) : Encoding.UTF8.GetBytes(request.Body);

            using var memStream = new MemoryStream(bodyContent);
            var formData = MultipartFormDataParser.Parse(memStream);

            var hotelName = formData.GetParameterValue("hotelName");
            var hotelRating = formData.GetParameterValue("hotelRating");
            var hotelCity = formData.GetParameterValue("hotelCity");
            var hotelPrice = formData.GetParameterValue("hotelPrice");
            var userId = formData.GetParameterValue("userId");
            var idToken = formData.GetParameterValue("idToken");
            var file = formData.Files.FirstOrDefault();
            var fileName = file?.Name;

            await using var filecontent = new MemoryStream();
            await file.Data.CopyToAsync(filecontent);
            filecontent.Position = 0;

            var token = new JwtSecurityToken(idToken);
            var group = token.Claims.FirstOrDefault(x => x.Type == "cognito:groups");

            if (group == null || group.Value != "Admin")
            {
                response.StatusCode = (int)HttpStatusCode.Unauthorized;
                response.Body = JsonSerializer.Serialize(new { Error = "Unauthorized. Must be a Admin member" });
            }

            var regionName = Environment.GetEnvironmentVariable("AWS_REGION");
            var bucketName = Environment.GetEnvironmentVariable("bucketName");
            var client = new AmazonS3Client(RegionEndpoint.GetBySystemName(regionName));
            await client.PutObjectAsync(new Amazon.S3.Model.PutObjectRequest
            {
                BucketName = bucketName,
                Key = fileName,
                InputStream = filecontent,
                AutoCloseStream = true,
            });

            var hotel = new Hotels
            {
                Name = hotelName,
                Id = Guid.NewGuid().ToString(),
                Rating = int.Parse(hotelRating),
                Price = int.Parse(hotelPrice),
                City = hotelCity,
                UserID = userId,
                FileName = fileName

            };
            var dbclient = new AmazonDynamoDBClient(RegionEndpoint.GetBySystemName(regionName));
            using var dbcontext = new DynamoDBContext(dbclient);
            await dbcontext.SaveAsync(hotel);

            return response;
        }
    }
}
