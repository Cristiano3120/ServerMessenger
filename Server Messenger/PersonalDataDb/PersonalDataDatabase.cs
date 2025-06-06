using System.Data;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Server_Messenger.PersonalDataDb
{
    internal partial class PersonalDataDatabase
    {
        [GeneratedRegex(@"Schlüssel »\(([^)]+)\)")]
        public partial Regex NpgsqlExceptionKeyRegex();

        private readonly TimeSpan _usernameChangeCooldown = TimeSpan.FromDays(3);
        private readonly PersonalDataDbContext _dbContext;

        #region Init

        public PersonalDataDatabase()
        {
            DbContextOptions<PersonalDataDbContext> options = new DbContextOptionsBuilder<PersonalDataDbContext>()
                .UseNpgsql(ReadConnString()).Options;

            _dbContext = new PersonalDataDbContext(options);

            if (_dbContext.Database.GetPendingMigrations().Any())
                _dbContext.Database.Migrate();
        }

        private static string ReadConnString()
            => Server.Config.GetProperty("ConnectionStrings").GetProperty("PersonalDataDatabase").GetString()!;
        

        #endregion

        #region Pre Login

        #region CreateAccount

        public async Task<(NpgsqlExceptionInfos npgsqlExceptionInfos, long userId)> CreateAccountAsync(User user)
        {
            try
            {
                if (user.Id == -1)
                    user.Id = await GetHighestIDAsync();

                user.Token = GenerateToken(user);

                User encryptedUser = await Security.EncryptAesDatabaseAsync(user);

                await _dbContext.Users.AddAsync(encryptedUser);
                await _dbContext.SaveChangesAsync();

                return (new NpgsqlExceptionInfos(), user.Id);
            }
            catch (NpgsqlException ex)
            {
                NpgsqlExceptionInfos exception = await HandleNpgsqlExceptionAsync(ex);
                if (exception.ColumnName == nameof(user.Id).ToCamelCase())
                    await CreateAccountAsync(user);

                return (exception, -1);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex);
                return (new NpgsqlExceptionInfos(NpgsqlExceptions.UnexpectedEx), -1);
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
                string encryptedEmail = await Security.EncryptAesDatabaseAsync<string, string>(email);
                string encryptedPassword = await Security.EncryptAesDatabaseAsync<string, string>(password);

                User? user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Email == encryptedEmail 
                    && x.Password == encryptedPassword);

                if (user is null)
                    return (null, new NpgsqlExceptionInfos(NpgsqlExceptions.WrongLoginData));

                if (!stayLoggedIn)
                    user.Token = "";

                return (await Security.DecryptAesDatabaseAsync(user), new NpgsqlExceptionInfos());
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
                string encryptedToken = await Security.EncryptAesDatabaseAsync<string, string>(token);
                User? user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Token == encryptedToken);

                return user is null
                    ? (null, new NpgsqlExceptionInfos(NpgsqlExceptions.TokenInvalid))
                    : (await Security.DecryptAesDatabaseAsync(user), new NpgsqlExceptionInfos());
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

        public async Task<NpgsqlExceptionInfos> UpdateRelationshipAsync(RelationshipUpdate relationshipUpdate)
        {
            try
            {
                (User? user, Relationship? affectedRelationship, RelationshipState requestedRelationshipState) = relationshipUpdate;
                if (affectedRelationship is null || user is null)
                {
                    return new NpgsqlExceptionInfos(NpgsqlExceptions.PayloadDataMissing);
                }

                if (affectedRelationship.Id == -1)
                {
                    User? affectedUser = await GetUserAsync(affectedRelationship.Username, affectedRelationship.Hashtag);
                    if (affectedUser is null)
                        return new NpgsqlExceptionInfos(NpgsqlExceptions.UserNotFound);

                    affectedRelationship = (Relationship)affectedUser;
                    affectedRelationship.RelationshipState = requestedRelationshipState;
                }

                long affectedID = affectedRelationship.Id;

                Relationships? blocked = await _dbContext.Relationships
                    .FirstOrDefaultAsync(x => x.RelationshipState == RelationshipState.Blocked && x.SenderId == affectedID && x.ReceiverId == user.Id);

                if (blocked is not null)
                    return new NpgsqlExceptionInfos(NpgsqlExceptions.RequestedUserIsBlocked);

                switch (relationshipUpdate.RequestedRelationshipState)
                {
                    case RelationshipState.Pending:
                        Relationships? dbRelationship = await _dbContext.Relationships
                            .FirstOrDefaultAsync(x => x.SenderId == user.Id && x.ReceiverId == affectedID);

                        if (dbRelationship is null)
                        {
                            dbRelationship = new()
                            {
                                SenderId = user.Id,
                                ReceiverId = affectedID,
                                RelationshipState = RelationshipState.Pending
                            };
                            await _dbContext.Relationships.AddAsync(dbRelationship);
                        }
                        else
                        {
                            dbRelationship.RelationshipState = RelationshipState.Pending;
                        }

                        await _dbContext.SaveChangesAsync();
                        break;

                    case RelationshipState.Friend:
                        await _dbContext.Relationships
                            .Where(x => (x.SenderId == user.Id && x.ReceiverId == affectedID) || (x.SenderId == affectedID && x.ReceiverId == user.Id))
                            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.RelationshipState, relationshipUpdate.RequestedRelationshipState));
                        break;

                    case RelationshipState.Blocked:
                        Relationships? relationships = await _dbContext.Relationships
                            .FirstOrDefaultAsync(x => (x.SenderId == user.Id && x.ReceiverId == affectedID) || (x.SenderId == affectedID && x.ReceiverId == user.Id));

                        if (relationships is null)
                            return new NpgsqlExceptionInfos(NpgsqlExceptions.NoDataEntrys);

                        _dbContext.Relationships.Remove(relationships);
                        await _dbContext.SaveChangesAsync();

                        relationships.RelationshipState = RelationshipState.Blocked;
                        relationships.SenderId = user.Id;
                        relationships.ReceiverId = affectedID;

                        _dbContext.Relationships.Add(relationships);
                        await _dbContext.SaveChangesAsync();
                        break;

                    case RelationshipState.None:
                        _dbContext.Relationships.Where(x => (x.SenderId == user.Id && x.ReceiverId == affectedID) || (x.SenderId == affectedID && x.ReceiverId == user.Id))
                            .ExecuteDelete();
                        await _dbContext.SaveChangesAsync();
                        break;

                    default:
                        throw new NotImplementedException();
                }

                return new NpgsqlExceptionInfos();
            }
            catch (NpgsqlException ex)
            {
                return await HandleNpgsqlExceptionAsync(ex);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
                return new NpgsqlExceptionInfos(NpgsqlExceptions.UnknownError);
            }
        }

        public async Task<User?> GetUserAsync(string username, string Hashtag)
        {
            try
            {
                string encryptedUsername = await Security.EncryptAesDatabaseAsync<string, string>(username);
                string encryptedHashtag = await Security.EncryptAesDatabaseAsync<string, string>(Hashtag);

                User? user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Username == encryptedUsername && x.Hashtag == encryptedHashtag);
                return await Security.DecryptAesDatabaseAsync(user);
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

        public async Task<(NpgsqlExceptionInfos, HashSet<Relationship>?)> GetUsersRelationshipsAsync(long id)
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
                    searchedUser = await Security.DecryptAesDatabaseAsync(searchedUser);

                    if (searchedUser is null)
                        return (new NpgsqlExceptionInfos(NpgsqlExceptions.NoDataEntrys), null);

                    Relationship relationship = (Relationship)searchedUser;
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

        #region Settings

        public async Task ChangeProfilePictureAsync(ProfilePictureUpdate profilePictureUpdate)
        {
            byte[] encryptedProfilePicture = await Security.EncryptAesDatabaseAsync<byte[], byte[]>(profilePictureUpdate.NewProfilePicture);

            await _dbContext.Users.Where(x => x.Id == profilePictureUpdate.Id)
                .ExecuteUpdateAsync(x => x.SetProperty(x => x.ProfilePicture, encryptedProfilePicture));
        }

        public async Task<UsernameUpdateResult> ChangeUsernameAsync(UsernameUpdate usernameUpdate)
        {
            string encryptedUsername = await Security.EncryptAesDatabaseAsync<string, string>(usernameUpdate.Username);
            string encryptedHashtag = await Security.EncryptAesDatabaseAsync<string, string>(usernameUpdate.Hashtag);

            if (await _dbContext.Users.AnyAsync(x => x.Username == encryptedUsername && x.Hashtag == encryptedHashtag))
            {
                return UsernameUpdateResult.NameTaken;
            }

            User user = await _dbContext.Users.FirstAsync(x => x.Id == usernameUpdate.UserId);
            if (user.LastUsernameChange is null || DateTime.UtcNow > user.LastUsernameChange + _usernameChangeCooldown)
            {
                user.LastUsernameChange = DateTime.UtcNow;
                user.Username = encryptedUsername;
                user.Hashtag = encryptedHashtag;

                await _dbContext.SaveChangesAsync();
                return UsernameUpdateResult.Successful;
            }

            return UsernameUpdateResult.OnCooldown;
        }

        #endregion

        #region Tests

        public async Task AddTestUsersToDbAsync()
        {
            await RemoveTestUsersAsync();

            User user = new()
            {
                Biography = await Security.EncryptAesDatabaseAsync<string, string>(""),
                Birthday = DateOnly.MaxValue,
                Email = await Security.EncryptAesDatabaseAsync<string, string>("Cris@cris.com"),
                Password = await Security.EncryptAesDatabaseAsync<string, string>("CrisCris"),
                FaEnabled = false,
                Hashtag = await Security.EncryptAesDatabaseAsync<string, string>("#Cris"),
                Username = await Security.EncryptAesDatabaseAsync<string, string>("Cris"),
                ProfilePicture = []
            };

            for (int i = 10; i < 15; i++)
            {
                string decryptedEmail = await Security.DecryptAesDatabaseAsync<string, string>(user.Email);
                string decryptedUsername = await Security.DecryptAesDatabaseAsync<string, string>(user.Username);

                string str = $"{i}";
                user.Email = await Security.EncryptAesDatabaseAsync<string, string>(decryptedEmail.Insert(4, str));
                user.Username = await Security.EncryptAesDatabaseAsync<string, string>($"{decryptedUsername}{str}");
                user.Token = await Security.EncryptAesDatabaseAsync<string, string>($"{i}");
                user.Id = i;

                await _dbContext.Users.AddAsync(user);
                await _dbContext.SaveChangesAsync();

                user.Email = await Security.EncryptAesDatabaseAsync<string, string>(decryptedEmail);
                user.Username = await Security.EncryptAesDatabaseAsync<string, string>(decryptedUsername);
            }
        }

        private async Task RemoveTestUsersAsync()
        {
            IQueryable<User> testUsers = _dbContext.Users.Where(x => x.Id >= 10);

            foreach (User user in testUsers)
            {
                _dbContext.Users.Remove(user);
            }

            await _dbContext.SaveChangesAsync();
        }

        private async Task RemoveTestAccsAsync()
        {
            IQueryable<User> testUsers = _dbContext.Users.Where(x => x.Id == 1 || x.Id == 2);
            foreach (User user in testUsers)
            {
                _dbContext.Users.Remove(user);
            }
            await _dbContext.SaveChangesAsync();
        }

        public async Task AddTwoTestAccs()
        {
            await RemoveTestAccsAsync();

            User user = new()
            {
                Biography = "",
                Birthday = DateOnly.MaxValue,
                Email = "Test@Test.com",
                Password = "TestTest",
                FaEnabled = false,
                Username = "Test1",
                Hashtag = "#Test",
                Id = 1,
                Token = "TestToken1",
            };

            User user2 = new()
            {
                Biography = "",
                Birthday = DateOnly.MaxValue,
                Email = "Test2@Test.com",
                Password = "TestTest",
                FaEnabled = false,
                Username = "Test2",
                Hashtag = "#Test",
                Id = 2,
                Token = "TestToken2",
            };

            Logger.LogWarning($"Added test user1. Token: {user.Token}");
            Logger.LogWarning($"Added test user2. Token: {user2.Token}");

            user = await Security.EncryptAesDatabaseAsync(user);
            user2 = await Security.EncryptAesDatabaseAsync(user2);

            _dbContext.Users.Add(user);
            _dbContext.Users.Add(user2);

            await _dbContext.SaveChangesAsync();
        }

        #endregion

        #region Handle Exceptions

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

        #endregion

        public async Task RemoveUserAsync(long userId)
        {
            try
            {
                User? userToRemove = _dbContext.Users.FirstOrDefault(x => x.Id == userId);

                if (userToRemove is not null)
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
    }
}
