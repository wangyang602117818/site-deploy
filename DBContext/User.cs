using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace SiteDeploy.Document.DBContext
{
    public class User
    {
        [BsonId]
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
        public string Email { get; set; } = string.Empty;
        public string? AuthCode { get; set; }
        public DateTime? AuthExpireAt { get; set; }
        public DateTime? LoginAt { get; set; }
    }
}
