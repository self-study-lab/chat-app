using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CleanArchitecture.Application.Repository;
using MongoDB.Driver;
using Microsoft.Extensions.Options;
using CleanArchitecture.Domain.Model;
using CleanArchitecture.Domain.Model.Player;
using CleanArchitecture.Infrastructure.Security;
using CleanArchitecture.Domain.Model.VerificationCode;

namespace CleanArchitecture.Infrastructure.Repository
{

    public class PlayerRepository : IPlayerRepository
    {
        private readonly IMongoCollection<Player> _playersCollection;
        private readonly IMongoCollection<VerificationCode> _verificationCollection;
        private readonly SecurityUtility securityUtility;
        public PlayerRepository(
           IOptions<DatabaseSettings> playerStoreDatabaseSettings, SecurityUtility securityUtility)
        {
            securityUtility = securityUtility ?? throw new ArgumentNullException(nameof(securityUtility));
            if (playerStoreDatabaseSettings == null || playerStoreDatabaseSettings.Value == null)
            {
                throw new ArgumentNullException(nameof(playerStoreDatabaseSettings), "Database settings are null.");
            }


            var mongoClient = new MongoClient(
                playerStoreDatabaseSettings.Value.ConnectionString);

            var mongoDatabase = mongoClient.GetDatabase(
                playerStoreDatabaseSettings.Value.DatabaseName);

            _verificationCollection = mongoDatabase.GetCollection<VerificationCode>(
                playerStoreDatabaseSettings.Value.VerificationCodesCollectionName);
            CreateTTLIndex().Wait();

            _playersCollection = mongoDatabase.GetCollection<Player>(
                playerStoreDatabaseSettings.Value.PlayersCollectionName);

            this.securityUtility = securityUtility;
        }
        private async Task CreateTTLIndex()
        {
            try
            {
                // Thử xóa index cũ nếu tồn tại
                var existingIndex = await _verificationCollection.Indexes.ListAsync();
                var indexes = await existingIndex.ToListAsync();
                var oldTtlIndex = indexes.FirstOrDefault(i => i["name"] == "createdAt_1");
                if (oldTtlIndex != null)
                {
                    await _verificationCollection.Indexes.DropOneAsync(oldTtlIndex["name"].AsString);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error while dropping existing TTL index", ex);
            }

            // Tạo TTL index mới
            var createdAtField = new StringFieldDefinition<VerificationCode>("CreatedAt");
            var ttlIndexDefinition = new IndexKeysDefinitionBuilder<VerificationCode>().Ascending(createdAtField);
            var ttlIndexOptions = new CreateIndexOptions { ExpireAfter = TimeSpan.FromMinutes(30) };
            var ttlIndexModel = new CreateIndexModel<VerificationCode>(ttlIndexDefinition, ttlIndexOptions);

            try
            {
                await _verificationCollection.Indexes.CreateOneAsync(ttlIndexModel);
            }
            catch (Exception ex)
            {
                throw new Exception("Error while creating new TTL index", ex);
            }
        }
        public async Task<List<Player>> GetMembers() =>
          await _playersCollection.Find(_ => true).ToListAsync();

        public async Task<Player?> GetMemberById(string id) =>
            await _playersCollection.Find(x => x.Id == id).FirstOrDefaultAsync();
        public async Task<Player?> LoginMember(string email, string password)
        {
            try
            {
                IEnumerable<Player> members = await GetMembers();
                Player member = members.SingleOrDefault(mb => mb.Username.Equals(email) && securityUtility.GenerateHashedPassword(password, mb.SaltPassword) == mb.HashedPassword);
                if (member != null)
                {
                    if (member.IsVerified == true)
                    {

                        if (member.IsActive == false)
                        {
                            throw new Exception("Account is not Active");
                        }
                        else
                        {
                            return member;
                        }
                    }
                    else
                    {
                        throw new Exception("Account is not Verified");
                    }
                }
                else
                {
                    throw new Exception("Username or Password is correct");
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        public async Task DeleteMember(string id) =>
           await _playersCollection.DeleteOneAsync(x => x.Id == id);
        public async Task UpdateMember(Player updatedplayer)
        {
            try
            {
                var existingPlayer = await _playersCollection.Find(x => x.Username == updatedplayer.Username && x.Id != updatedplayer.Id).FirstOrDefaultAsync();
                if (existingPlayer == null)
                {
                    await _playersCollection.ReplaceOneAsync(x => x.Id == updatedplayer.Id, updatedplayer);
                }
                else
                {
                    throw new Exception("Username is Exits");
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
        public async Task AddMember(Player newplayer)
        {
            try
            {
                var existingPlayer = await _playersCollection.Find(x => x.Username == newplayer.Username).FirstOrDefaultAsync();
                if (existingPlayer == null)
                {
                    await _playersCollection.InsertOneAsync(newplayer);
                }
                else
                {
                    throw new Exception("Username is Exits");
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
        public async Task<VerificationCode> GetVerificationCodeByUsername(string username)
        {
            try
            {
                return await _verificationCollection.Find(x => x.Email == username).FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
        public async Task AddVerificationCode(VerificationCode newCode)
        {
            try
            {
                var existingPlayer = await _verificationCollection.Find(x => x.Code == newCode.Code).FirstOrDefaultAsync();
                if (existingPlayer == null)
                {
                    await _verificationCollection.InsertOneAsync(newCode);
                }
                else
                {
                    throw new Exception("newCode is Exits");
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
        public async Task RefreshVerificationCode(VerificationCode newVerify)
        {
            try
            {
                var existingPlayer = await _playersCollection.Find(x => x.Username == newVerify.Email).FirstOrDefaultAsync();
                var existingCode = await _verificationCollection.Find(x => x.Email == newVerify.Email).FirstOrDefaultAsync();
                if (existingPlayer == null)
                {
                    throw new Exception("Username is not exit");
                }
                if (existingCode == null)
                {
                    await _verificationCollection.InsertOneAsync(newVerify);
                }
                else
                {

                    var updateDefinition = Builders<VerificationCode>.Update
                        .Set(x => x.Code, newVerify.Code)
                        .Set(x => x.CreatedAt, DateTime.UtcNow);

                    await _verificationCollection.UpdateOneAsync(
                        Builders<VerificationCode>.Filter.Eq(x => x.Email, newVerify.Email),
                        updateDefinition);

                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
        public async Task VerifyAccount(VerificationCode newVerify)
        {
            try
            {
                var existingPlayer = await _playersCollection.Find(x => x.Username == newVerify.Email).FirstOrDefaultAsync();
                var existingCode = await _verificationCollection.Find(x => x.Email == newVerify.Email).FirstOrDefaultAsync();
                var currentTime = DateTime.UtcNow;
                var timeDifference = currentTime - existingCode.CreatedAt;

                if (existingPlayer == null)
                {
                    throw new Exception("Username is not exist");
                }
                if (existingCode.Code == newVerify.Code)
                {
                    if (timeDifference.TotalMinutes <= 10)
                    {
                        var updateDefinition = Builders<Player>.Update
                        .Set(x => x.IsVerified, true);


                        await _playersCollection.UpdateOneAsync(
                              Builders<Player>.Filter.Eq(x => x.Id, existingPlayer.Id),
                              updateDefinition);

                        await _verificationCollection.DeleteOneAsync(x => x.Id == newVerify.Id);
                    }
                    else
                    {
                        throw new Exception("Expired code");
                    }

                }
                else
                {
                    throw new Exception("Incorrect code");
                }
            }
            catch (Exception ex)
            {

                throw new Exception(ex.Message);
            }
        }

        public async Task ChangePassword(string id, string hashedPassword, string saltPassword)
        {
            try
            {
                var existingPlayer = await _playersCollection.Find(x => x.Id == id).FirstOrDefaultAsync();
                if (existingPlayer != null)
                {
                    existingPlayer.HashedPassword = hashedPassword;
                    existingPlayer.SaltPassword = saltPassword;


                    await _playersCollection.ReplaceOneAsync(x => x.Id == id, existingPlayer);
                }
                else
                {

                    throw new Exception("Player not found");
                }
            }
            catch (Exception ex)
            {

                throw new Exception(ex.Message);
            }
        }
    }
}

