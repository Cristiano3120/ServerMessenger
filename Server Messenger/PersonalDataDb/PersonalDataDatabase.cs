using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Data;
using System.Text;
using System.Text.RegularExpressions;

namespace Server_Messenger.PersonalDataDb
{
    internal partial class PersonalDataDatabase
    {
        [GeneratedRegex(@"Schlüssel »\(([^)]+)\)")]
        public static partial Regex NpgsqlExceptionKeyRegex();

        private static readonly string _connectionString = ReadConnString();
        private readonly PersonalDataDbContext _dbContext;

        #region Init

        public PersonalDataDatabase()
        {
            DbContextOptions<PersonalDataDbContext> options = new DbContextOptionsBuilder<PersonalDataDbContext>()
                .UseNpgsql(_connectionString).Options;

            _dbContext = new PersonalDataDbContext(options);

            if (_dbContext.Database.GetPendingMigrations().Any())
                _dbContext.Database.Migrate();
        }

        private static string ReadConnString()
        {
            return Server.Config.GetProperty("ConnectionStrings").GetProperty("PersonalDataDatabase").GetString()!;
        }

        #endregion

        #region Pre Login

        #region CreateAccount

        public async Task<NpgsqlExceptionInfos> CreateAccountAsync(User user)
        {
            try
            {
                if (user.Id == -1)
                    user.Id = await GetHighestIDAsync();

                user.Token = GenerateToken(user);

                User encryptedUser = Security.EncryptAesDatabase(user);

                await _dbContext.Users.AddAsync(encryptedUser);
                await _dbContext.SaveChangesAsync();

                return new NpgsqlExceptionInfos();
            }
            catch (NpgsqlException ex)
            {
                NpgsqlExceptionInfos exception = await HandleNpgsqlExceptionAsync(ex);
                if (exception.ColumnName == "id")
                    await CreateAccountAsync(user);

                return exception;
            }
        }

        private static string GenerateToken(User user)
        {
            byte[] emailBytes = Encoding.UTF8.GetBytes(user.Email);
            string hashedPassword = Security.Hash(user.Password);

            return Convert.ToBase64String(emailBytes) + hashedPassword;
        }

        private async Task<long> GetHighestIDAsync()
        {
            try
            {
                return await _dbContext.Users.AnyAsync() 
                    ? await _dbContext.Users.MaxAsync(x => x.Id) + 1 
                    : 1;
            }
            catch (NpgsqlException ex)
            {
                Logger.LogError(ex);
                return -1;
            }
        }

        #endregion

        #region CheckLoginData

        public async Task<(User? user, NpgsqlExceptionInfos npgsqlExceptionInfos)> CheckLoginDataAsync(string email, string password, bool stayLoggedIn)
        {
            try
            {
                string encryptedEmail = Security.EncryptAesDatabase<string, string>(email);
                string encryptedPassword = Security.EncryptAesDatabase<string, string>(password);
                User? user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Email == encryptedEmail && x.Password == encryptedPassword);

                if (user == null)
                    return (null, new NpgsqlExceptionInfos(NpgsqlExceptions.WrongLoginData));

                if (!stayLoggedIn)
                    user.Token = "";

                return (Security.DecryptAesDatabase(user), new NpgsqlExceptionInfos());
            }
            catch (NpgsqlException ex)
            {
                NpgsqlExceptionInfos exceptionInfos = await HandleNpgsqlExceptionAsync(ex);
                return (null, exceptionInfos);
            }
        }

        public async Task<(User? user, NpgsqlExceptionInfos npgsqlExceptionInfos)> CheckLoginDataAsync(string token)
        {
            try
            {
                string encryptedToken = Security.EncryptAesDatabase<string, string>(token);
                User? user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Token == encryptedToken);

                return user == null
                    ? (null, new NpgsqlExceptionInfos(NpgsqlExceptions.TokenInvalid))
                    : (Security.DecryptAesDatabase(user), new NpgsqlExceptionInfos());
            }
            catch (NpgsqlException ex)
            {
                NpgsqlExceptionInfos exceptionInfos = await HandleNpgsqlExceptionAsync(ex);
                return (null, exceptionInfos);
            }
        }

        #endregion

        #endregion

        #region PastLogin

        public async Task<NpgsqlExceptionInfos> UpdateRelationshipAsync(RelationshipUpdate relationshipUpdate)
        {
            try
            {
                Relationship affectedUser = relationshipUpdate.Relationship;
                long userID = relationshipUpdate.User.Id;
                long affectedUserID = affectedUser.Id;
                Relationships relationship;

                if (affectedUserID == -1)
                    affectedUserID = await GetIDByNameAsync(affectedUser.Username, affectedUser.HashTag);

                if (affectedUserID == -1)
                    return new NpgsqlExceptionInfos(NpgsqlExceptions.UserNotFound);

                switch (relationshipUpdate.RequestedRelationshipstate)
                {
                    case Relationshipstate.Pending:
                        relationship = new()
                        {
                            SenderId = userID,
                            ReceiverId = affectedUserID,
                            Relationshipstate = Relationshipstate.Pending
                        };

                        await _dbContext.Relationships.AddAsync(relationship);
                        await _dbContext.SaveChangesAsync();

                        return new NpgsqlExceptionInfos();

                    case Relationshipstate.Friend or Relationshipstate.Blocked:
                        await _dbContext.Relationships.Where(x => x.SenderId == affectedUserID && x.ReceiverId == userID || x.SenderId == userID && x.ReceiverId == affectedUserID)
                            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Relationshipstate, relationshipUpdate.RequestedRelationshipstate));
                        return new NpgsqlExceptionInfos();
                    default:
                        throw new NotImplementedException();
                }
            }
            catch (NpgsqlException ex)
            {
                return await HandleNpgsqlExceptionAsync(ex);
            }
        }

        private async Task<long> GetIDByNameAsync(string username, string hashTag)
        {
            string encryptedUsername = Security.EncryptAesDatabase<string, string>(username);
            string encryptedHashTag = Security.EncryptAesDatabase<string, string>(hashTag);

            User? user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Username == encryptedUsername && x.HashTag == encryptedHashTag);

            return user != null
                ? user.Id
                : -1;
        }

        #endregion

        public async Task RemoveUserAsync(string email)
        {
            string encryptedEmail = Security.EncryptAesDatabase<string, string>(email);
            User? userToRemove = _dbContext.Users.FirstOrDefault(x => x.Email == encryptedEmail);

            if (userToRemove != null)
            {
                _dbContext.Users.Remove(userToRemove);
                await _dbContext.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Logs and handles the exception
        /// </summary>
        /// <param name="ex"></param>
        /// <returns></returns>
        private static async Task<NpgsqlExceptionInfos> HandleNpgsqlExceptionAsync(NpgsqlException ex)
        {
            Logger.LogError(ex);
            var errorCode = ex.SqlState;

            switch (errorCode)
            {
                case "08001" or "08006" or "08003":
                    await Server.ShutdownAsync();
                    return new NpgsqlExceptionInfos(NpgsqlExceptions.ConnectionError);
                case "23505":
                    var message = ex.Message;
                    string columnName = "";

                    if (!string.IsNullOrEmpty(message))
                    {
                        Match match = NpgsqlExceptionKeyRegex().Match(message);

                        if (match.Success)
                        {
                            columnName = match.Groups[1].Value;
                        }
                    }

                    return new NpgsqlExceptionInfos(NpgsqlExceptions.AccCreationError, columnName);
                default:
                    return new NpgsqlExceptionInfos(NpgsqlExceptions.UnknownError);
            }
        }
    }
}
