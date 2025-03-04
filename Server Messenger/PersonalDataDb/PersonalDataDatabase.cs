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
        public partial Regex NpgsqlExceptionKeyRegex();

        private readonly string _connectionString = ReadConnString();
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
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex);
                return new NpgsqlExceptionInfos(NpgsqlExceptions.UnexpectedEx);
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
            catch (Exception ex)
            {
                Logger.LogError(ex);
                return -1;
            }
        }

        #endregion

        #region CheckLoginData

        public async Task<(User? user, NpgsqlExceptionInfos npgsqlExceptionInfos)> CheckLoginDataAsync(LoginRequest loginRequest)
        {
            try
            {
                (string email, string password, bool stayLoggedIn) = loginRequest;
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
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex);
                return (null, new NpgsqlExceptionInfos(NpgsqlExceptions.UnexpectedEx));
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
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex);
                return (null, new NpgsqlExceptionInfos(NpgsqlExceptions.UnexpectedEx));
            }
        }

        #endregion

        #endregion

        #region PastLogin

        public async Task<(NpgsqlExceptionInfos, Relationship?)> UpdateRelationshipAsync(RelationshipUpdate relationshipUpdate)
        {
            try
            {
                Relationship affectedRelationship = relationshipUpdate.Relationship;
                (long userID, Relationship? relationship, RelationshipState requestedRelationshipState) = relationshipUpdate;

                if (relationship.Id == -1)
                {
                    User? affectedUser = await GetUser(affectedRelationship.Username, affectedRelationship.HashTag);
                    if (affectedUser is null)
                        return (new NpgsqlExceptionInfos(NpgsqlExceptions.UserNotFound), null);

                    relationship = (Relationship)affectedUser;
                    relationship.RelationshipState = requestedRelationshipState;
                }

                long affectedID = relationship.Id;

                Relationships? blocked = await _dbContext.Relationships
                    .FirstOrDefaultAsync(x => x.RelationshipState == RelationshipState.Blocked && x.SenderId == affectedID && x.ReceiverId == userID);
    
                if (blocked != null)
                    return (new NpgsqlExceptionInfos(NpgsqlExceptions.RequestedUserIsBlocked), null);

                switch (relationshipUpdate.RequestedRelationshipState)
                {
                    case RelationshipState.Pending:
                        Relationships? dbRelationship = await _dbContext.Relationships
                            .FirstOrDefaultAsync(r => r.SenderId == userID && r.ReceiverId == affectedID);

                        if (dbRelationship is null)
                        {
                            dbRelationship = new()
                            {
                                SenderId = userID,
                                ReceiverId = affectedID,
                                RelationshipState = RelationshipState.Pending
                            };
                            await _dbContext.Relationships.AddAsync(dbRelationship);
                        }
                        else
                        {
                            dbRelationship.RelationshipState = RelationshipState.Pending;
                        }
                        //so the sender doesnt receive the pending request
                        relationship = null;

                        await _dbContext.SaveChangesAsync();
                        break;

                    case RelationshipState.Friend:
                        await _dbContext.Relationships
                            .Where(x => (x.SenderId == userID && x.ReceiverId == affectedID) || (x.SenderId == affectedID && x.ReceiverId == userID))
                            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.RelationshipState, relationshipUpdate.RequestedRelationshipState));
                        break;

                    case RelationshipState.Blocked:
                        Relationships? relationships = await _dbContext.Relationships
                            .FirstOrDefaultAsync(x => (x.SenderId == userID && x.ReceiverId == affectedID) || (x.SenderId == affectedID && x.ReceiverId == userID));

                        if (relationships == null)
                            return (new NpgsqlExceptionInfos(NpgsqlExceptions.NoDataEntrys), null);

                        _dbContext.Relationships.Remove(relationships);
                        await _dbContext.SaveChangesAsync();

                        relationships.RelationshipState = RelationshipState.Blocked;
                        relationships.SenderId = userID;
                        relationships.ReceiverId = affectedID;

                        _dbContext.Relationships.Add(relationships);
                        await _dbContext.SaveChangesAsync();
                        break;

                    case RelationshipState.None:
                        _dbContext.Relationships.Where(x => (x.SenderId == userID && x.ReceiverId == affectedID) || (x.SenderId == affectedID && x.ReceiverId == userID))
                            .ExecuteDelete();
                        await _dbContext.SaveChangesAsync();
                        break;

                    default:
                        throw new NotImplementedException();
                }

                return (new NpgsqlExceptionInfos(), relationship);
            }
            catch (NpgsqlException ex)
            {
                return (await HandleNpgsqlExceptionAsync(ex), null);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
                return (new NpgsqlExceptionInfos(NpgsqlExceptions.UnknownError), null);
            }
        }

        private async Task<User?> GetUser(string username, string hashTag)
        {
            try
            {
                string encryptedUsername = Security.EncryptAesDatabase<string, string>(username);
                string encryptedHashTag = Security.EncryptAesDatabase<string, string>(hashTag);

                User? user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Username == encryptedUsername && x.HashTag == encryptedHashTag);
                return Security.DecryptAesDatabase(user);
            }
            catch (NpgsqlException ex)
            {
                await HandleNpgsqlExceptionAsync(ex);
                return null;
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex);
                return null;
            }
        }

        public async Task<(NpgsqlExceptionInfos, HashSet<Relationship>?)> GetUsersRelationships(long id)
        {
            try
            {
                HashSet<Relationships> relations = [.._dbContext.Relationships
                    .Where(x => x.SenderId == id && x.RelationshipState != RelationshipState.Pending
                    || x.ReceiverId == id && x.RelationshipState != RelationshipState.Blocked)];

                HashSet<Relationship> relationships = [];
                foreach (Relationships relation in relations)
                {
                    long searchedUserId = id == relation.SenderId
                        ? relation.ReceiverId
                        : relation.SenderId;

                    User? searchedUser = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == searchedUserId);
                    searchedUser = Security.DecryptAesDatabase(searchedUser);

                    if (searchedUser == null)
                        return (new NpgsqlExceptionInfos(NpgsqlExceptions.NoDataEntrys), null);

                    var relationship = (Relationship)searchedUser;
                    relationship.RelationshipState = relation.RelationshipState;

                    relationships.Add(relationship);
                }

                return (new NpgsqlExceptionInfos(), relationships);
            }
            catch (NpgsqlException ex)
            {
                return (await HandleNpgsqlExceptionAsync(ex), null);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex);
                return (new NpgsqlExceptionInfos(NpgsqlExceptions.UnexpectedEx), null);
            }
        }

        #endregion

        #region Tests

        public async Task AddTestUsersToDb()
        {
            await RemoveTestUsers();

            User user = new()
            {
                Biography = Security.EncryptAesDatabase<string, string>(""),
                Birthday = DateOnly.MaxValue,
                Email = Security.EncryptAesDatabase<string, string>("Cris@cris.com"),
                Password = Security.EncryptAesDatabase<string, string>("CrisCris"),
                FaEnabled = false,
                HashTag = Security.EncryptAesDatabase<string, string>("#Cris"),
                Username = Security.EncryptAesDatabase<string, string>("Cris"),
                ProfilePicture = []
            };

            for (int i = 10; i < 15; i++)
            {
                string decryptedEmail = Security.DecryptAesDatabase<string, string>(user.Email);
                string decryptedUsername = Security.DecryptAesDatabase<string, string>(user.Username);

                string str = $"{i}";
                user.Email = Security.EncryptAesDatabase<string, string>($"{decryptedEmail}{str}");
                user.Username = Security.EncryptAesDatabase<string, string>($"{decryptedUsername}{str}");
                user.Token = Security.EncryptAesDatabase<string, string>($"{i}");
                user.Id = i;

                await _dbContext.Users.AddAsync(user);
                await _dbContext.SaveChangesAsync();

                user.Email = Security.EncryptAesDatabase<string, string>(decryptedEmail);
                user.Username = Security.EncryptAesDatabase<string, string>(decryptedUsername);
            }
        }


        private async Task RemoveTestUsers()
        {
            IQueryable<User> testUsers = _dbContext.Users.Where(x => x.Username != Security.EncryptAesDatabase<string, string>("Cris"));

            foreach (User user in testUsers)
            {
                _dbContext.Users.Remove(user);
            }

            await _dbContext.SaveChangesAsync();
        }

        #endregion

        public async Task RemoveUserAsync(string email)
        {
            try
            {
                string encryptedEmail = Security.EncryptAesDatabase<string, string>(email);
                User? userToRemove = _dbContext.Users.FirstOrDefault(x => x.Email == encryptedEmail);

                if (userToRemove != null)
                {
                    _dbContext.Users.Remove(userToRemove);
                    await _dbContext.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex);
            }
        }

        /// <summary>
        /// Logs and handles the exception
        /// </summary>
        /// <param name="ex"></param>
        /// <returns></returns>
        private async Task<NpgsqlExceptionInfos> HandleNpgsqlExceptionAsync(NpgsqlException ex)
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

        private static async Task HandleExceptionAsync(Exception ex)
        {
            Logger.LogError(ex);
            await Server.ShutdownAsync();
        }
    }
}
