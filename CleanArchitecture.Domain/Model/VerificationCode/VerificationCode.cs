using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace CleanArchitecture.Domain.Model.VerificationCode
{
    public class VerificationCode
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonElement("email")]
        public string Email { get; set; }

        [BsonElement("verificationCode")]
        public string Code { get; set; }

        [BsonElement("createdAt")]
        [BsonRepresentation(BsonType.DateTime)]
        public DateTime CreatedAt { get; set; }
    }
}
