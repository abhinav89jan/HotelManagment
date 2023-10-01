using Amazon.DynamoDBv2.DataModel;

namespace HotelManagment.Models
{
    [DynamoDBTable("prac-hotels")]
    public class Hotels
    {
        [DynamoDBHashKey("userid")]
        public string UserID { get; set; }
        [DynamoDBRangeKey("id")]
        public string Id { get; set; }
        public string Name { get; set; }
        public int Rating { get; set; }
        public int Price { get; set; }
        public string City { get; set; }
        public string FileName { get; set; }
    }
}
