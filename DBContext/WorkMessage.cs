using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace SiteDeploy.Document.DBContext
{
    public class WorkMessage
    {
        [BsonId]
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

        [Required]
        public string WorkId { get; set; } = string.Empty;

        [Required]
        public string SenderType { get; set; } = string.Empty;

        [Required]
        public string Content { get; set; } = string.Empty;

        public DateTime CreateAt { get; set; } = DateTime.UtcNow;
    }
}
