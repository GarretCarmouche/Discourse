using DiscourseBackend.DataModels;
using Npgsql;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc; // Extension for JSON support

namespace DiscourseBackend;

public class API
{
    static readonly string ConnString = $"Host=db;Username=postgres;Password={Environment.GetEnvironmentVariable("DB_PASSWORD")};Database=postgres";
    private static readonly string fileUploadLocation = Environment.GetEnvironmentVariable("FILE_UPLOAD_LOCATION")!;
    private static Dictionary<string, string> _authTokens = new();
    private static Dictionary<string, int> _authTokensById = new();

    private static bool VerifyAuthToken(string token, int userId)
    {
        return _authTokensById.ContainsKey(token) && _authTokensById[token] == userId;
    }
    
    private static bool VerifyAuthToken(string token, string? username = null)
    {
        if(username != null)
            return _authTokens.ContainsKey(token) && _authTokens[token] == username;
        
        return _authTokens.ContainsKey(token);
    }

    public static async Task<int> GetUserIdFromNameAsync(string username, string authToken)
    {
        if (!VerifyAuthToken(authToken))
        {
            return -1;
        }
        
        string query = "SELECT id FROM USERS WHERE username = @username";
        await using var conn = new NpgsqlConnection(ConnString);
        await conn.OpenAsync();
        await using (var cmd = new NpgsqlCommand(query, conn))
        {
            cmd.Parameters.AddWithValue("@username", username);
            NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
            if (!reader.HasRows)
            {
                return -1;
            }
                
            
            reader.Read();
            return reader.GetInt32(0);
        }
    }

    public static async Task CreateDefaultUserAsync()
    {
        string username = "admin";
        string password = "admin";
        PasswordHasher<string>  passwordHasher = new PasswordHasher<string>();
        string passwordHash = passwordHasher.HashPassword(username, password);
        string sql = "INSERT INTO USERS(username, passwordHash) " +
                     "SELECT @username, @passwordHash " +
                     "WHERE NOT EXISTS (SELECT 1 FROM USERS WHERE username = @username)";
        
        await using var conn = new NpgsqlConnection(ConnString);
        await conn.OpenAsync();
        NpgsqlCommand cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@username", username);
        cmd.Parameters.AddWithValue("@passwordHash", passwordHash);
        await cmd.ExecuteNonQueryAsync();
        Console.WriteLine("Created default user");
    }
    
    public static async Task CreateUserAsync(string username, string password, string authToken)
    {
        if (!VerifyAuthToken(authToken, "admin"))
            return;
        
        PasswordHasher<string>  passwordHasher = new PasswordHasher<string>();
        string passwordHash = passwordHasher.HashPassword(username, password);
        string sql = "INSERT INTO USERS(username, passwordHash) " +
                       "VALUES (@username, @passwordHash)";
        
        await using var conn = new NpgsqlConnection(ConnString);
        await conn.OpenAsync();
        NpgsqlCommand cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@username", username);
        cmd.Parameters.AddWithValue("@passwordHash", passwordHash);
        await cmd.ExecuteNonQueryAsync();
        Console.WriteLine("Created user");
    }
    
    public static async Task<string?> GetAccessTokenAsync(string username, string password)
    {
        PasswordHasher<string> passwordHasher = new PasswordHasher<string>();
        string query = $"SELECT passwordHash, id FROM USERS " +
                       $"WHERE username = @username";
        await using var conn = new NpgsqlConnection(ConnString);
        await conn.OpenAsync();
        await using (var cmd = new NpgsqlCommand(query, conn))
        {
            cmd.Parameters.AddWithValue("@username", username);
            NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
            if (!reader.HasRows)
                return "";
            reader.Read();
            string passwordHash = reader.GetString(0);
            int userId = reader.GetInt32(1);

            PasswordVerificationResult result = passwordHasher.VerifyHashedPassword(username, passwordHash, password);
            if (result == PasswordVerificationResult.Failed)
                return "";

            string authToken = Guid.NewGuid().ToString();
            _authTokens.Add(authToken, username);
            _authTokensById.Add(authToken, userId);
            return authToken;
        }
    }
    
    public static async Task InitializeSchemaAsync()
    {
        string seed = File.ReadAllText("DatabaseSeed.sql");
        await API.ExecuteNonQueryAsync(seed);
        Console.WriteLine("DB Initialized");
    }

    public static async Task<bool> DoesMessageExistsAfterAsync(int user1Id, int user2Id, int messageId, string authToken)
    {
        if (!VerifyAuthToken(authToken,user1Id) && !VerifyAuthToken(authToken, user2Id))
            return false;

        string query = "SELECT 1 FROM MESSAGES " +
                       "WHERE ((senderId = @user1Id AND recipientId = @user2Id) " +
                       "OR (senderId =  @user2Id AND recipientId = @user1Id)) " +
                       "AND id > @messageId " +
                       "LIMIT 1";
        
        await using var conn = new NpgsqlConnection(ConnString);
        await conn.OpenAsync();
        await using (var cmd = new NpgsqlCommand(query, conn))
        {
            cmd.Parameters.AddWithValue("@user1Id", user1Id);
            cmd.Parameters.AddWithValue("@user2Id", user2Id);
            cmd.Parameters.AddWithValue("@messageId", messageId);
            NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();

            return reader.HasRows;
        }
    }

    public static async Task MarkMessagesReadAsync(int senderId, int recipientId, string authToken)
    {
        if (!VerifyAuthToken(authToken, senderId) && !VerifyAuthToken(authToken, recipientId))
            return;

        string query = "UPDATE MESSAGES SET unread = '0' " +
                       "WHERE (senderId = @senderId AND recipientId = @recipientId)";
        await using var conn = new NpgsqlConnection(ConnString);
        await conn.OpenAsync();
        NpgsqlCommand cmd = new NpgsqlCommand(query, conn);
        cmd.Parameters.AddWithValue("@senderId", senderId);
        cmd.Parameters.AddWithValue("@recipientId", recipientId);
        await cmd.ExecuteNonQueryAsync();
    }
    public static async Task<int> GetUnreadMessageCountAsync(int senderId, int recipientId, string authToken)
    {
        if (!VerifyAuthToken(authToken, senderId) && !VerifyAuthToken(authToken, recipientId))
            return 0;

        string query = $"SELECT COUNT(1) FROM MESSAGES " +
                       $"WHERE (senderId = @senderId AND recipientId = @recipientId) " +
                       $"AND unread = '1'";
        await using var conn = new NpgsqlConnection(ConnString);
        await conn.OpenAsync();
        await using (var cmd = new NpgsqlCommand(query, conn))
        {
            cmd.Parameters.AddWithValue("@senderId", senderId);
            cmd.Parameters.AddWithValue("@recipientId", recipientId);
            NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
            if (reader.Read())
            {
                return reader.HasRows ? reader.GetInt32(0) : 0;
            }

            return 0;
        }
    }

    public static async Task<bool> FileExistsAsync(string filepath, string authToken)
    {
        if (!VerifyAuthToken(authToken))
            return false;

        string path = "/uploads/" + filepath;
        return File.Exists(path);
    }
    
    public static async Task<IResult> DownloadFileAsync(string filename, string filepath, string authToken)
    {
        if (!VerifyAuthToken(authToken))
            return Results.Problem();

        string path = "/uploads/"+filepath;
        if (System.IO.File.Exists(path))
        {
            return Results.File(path, "application/octet-stream", filename); 
        }

        return Results.NotFound($"File not found: {filename}");
    }

    public static async Task<List<Message>> GetMessagesAsync(int user1Id,  int user2Id, string authToken)
    {
        if (!VerifyAuthToken(authToken, user1Id) && !VerifyAuthToken(authToken, user2Id))
            return new();
        
        List<Message> messages = new List<Message>();
        string query = $"SELECT MESSAGES.id, senderId, message, senttime, USERS.username FROM MESSAGES " +
                       $"INNER JOIN USERS ON USERS.Id = MESSAGES.senderId " +
                       $"WHERE (senderId = @user1Id AND recipientId = @user2Id) " +
                       $"OR (senderId =  @user2Id AND recipientId = @user1Id) " +
                       $"ORDER BY senttime DESC";
        await using var conn = new NpgsqlConnection(ConnString);
        await conn.OpenAsync();
        await using (var cmd = new NpgsqlCommand(query, conn))
        {
            cmd.Parameters.AddWithValue("@user1Id", user1Id);
            cmd.Parameters.AddWithValue("@user2Id", user2Id);
            NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (reader.Read())
            {
                Message message = new Message();
                message.Id = reader.GetInt32(0);
                message.UserId = reader.GetInt32(1);
                message.Content = reader.GetString(2);
                message.Date = reader.GetDateTime(3);
                message.Username = reader.GetString(4);
                messages.Add(message);
                
                string attachmentsQuery = $"SELECT filename, filepath FROM ATTACHMENTS " +
                                          $"WHERE messageid = {message.Id}";
                await using var attachmentcon = new NpgsqlConnection(ConnString);
                await attachmentcon.OpenAsync();
                await using (var attachmentcmd = new NpgsqlCommand(attachmentsQuery, attachmentcon))
                {
                    NpgsqlDataReader attachmentReader = await attachmentcmd.ExecuteReaderAsync();
                    while (attachmentReader.Read())
                    {
                        message.Attachments.Add(attachmentReader.GetString(0), attachmentReader.GetString(1));
                    } 
                }
            }
            
            return messages;
        }
    }

    public static async Task<bool> ChangePasswordAsync(string username, string oldPassword, string newPassword, string authToken)
    {
        Console.WriteLine("Change password");
        if (!VerifyAuthToken(authToken, username))
        {
            Console.WriteLine("Bad auth");
            return false;
        }
        
        PasswordHasher<string> passwordHasher = new PasswordHasher<string>();
        string query = $"SELECT passwordHash FROM USERS " +
                       $"WHERE username = @username";
        await using var conn = new NpgsqlConnection(ConnString);
        await conn.OpenAsync();
        await using (var cmd = new NpgsqlCommand(query, conn))
        {
            cmd.Parameters.AddWithValue("@username", username);
            NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
            if (!reader.HasRows)
            {
                Console.WriteLine($"No user {username}");
                return false;
            }
                
            reader.Read();
            string passwordHash = reader.GetString(0);

            PasswordVerificationResult result = passwordHasher.VerifyHashedPassword(username, passwordHash, oldPassword);
            if (result == PasswordVerificationResult.Failed)
            {
                Console.WriteLine($"Bad old password {oldPassword}");
                return false;
            }
                

            string newPasswordhash = passwordHasher.HashPassword(username, newPassword);
            string setPasswordQuery = $"UPDATE USERS SET passwordHash = @passwordHash WHERE username = @username";
            await using var setPasswordCon = new NpgsqlConnection(ConnString);
            await setPasswordCon.OpenAsync();
            await using (var setPasswordCmd = new NpgsqlCommand(setPasswordQuery, setPasswordCon))
            {
                setPasswordCmd.Parameters.AddWithValue("@passwordHash", newPasswordhash);
                setPasswordCmd.Parameters.AddWithValue("@username", username);
                await setPasswordCmd.ExecuteNonQueryAsync();

                Console.WriteLine($"Changed password to {newPassword}");
                return true;
            }
        }
    }
    
    public static async Task SendMessageWithAttachmentsAsync(int senderId, int recipientId, string message,
        string fileNamePathsJson, string authToken)
    {
        if (!VerifyAuthToken(authToken, senderId))
            return;

        Dictionary<string, string> fileNamePaths = JsonSerializer.Deserialize<Dictionary<string, string>>(fileNamePathsJson)!;
        string query = $"INSERT INTO MESSAGES (senderId, recipientId, message, senttime, unread) " +
                       $"VALUES (@senderId, @recipientId, @message, @senttime, '1') " +
                       $"RETURNING id";
        await using var conn = new NpgsqlConnection(ConnString);
        await conn.OpenAsync();
        await using (var cmd = new NpgsqlCommand(query, conn))
        {
            cmd.Parameters.AddWithValue("@senderId", senderId);
            cmd.Parameters.AddWithValue("@recipientId", recipientId);
            cmd.Parameters.AddWithValue("@message", message);
            cmd.Parameters.AddWithValue("@senttime", DateTime.UtcNow);
            NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
            if (reader.HasRows && reader.Read())
            {
                int messageId = reader.GetInt32(0);

                foreach (KeyValuePair<string, string> fileNamePath in fileNamePaths)
                {
                    string attachmentQuery = "INSERT INTO ATTACHMENTS(messageid, filename, filepath) " +
                                             "VALUES (@messageid, @filename, @filepath)";
                    await using var attachmentcon = new NpgsqlConnection(ConnString);
                    await attachmentcon.OpenAsync();
                    NpgsqlCommand messagecmd = new NpgsqlCommand(attachmentQuery, attachmentcon);
                    messagecmd.Parameters.AddWithValue("@messageid", messageId);
                    messagecmd.Parameters.AddWithValue("@filename", fileNamePath.Key);
                    messagecmd.Parameters.AddWithValue("@filepath", fileNamePath.Value);
                    await messagecmd.ExecuteNonQueryAsync();
                }
            }
        }
    }
    
    public static async Task SendMessageAsync(int senderId, int recipientId, string message, string authToken)
    {
        if (!VerifyAuthToken(authToken, senderId))
            return;
        
        string sql = $"INSERT INTO MESSAGES (senderId, recipientId, message, senttime, unread)" +
                       $"VALUES (@senderId, @recipientId, @message, @senttime, '1')";
        await using var conn = new NpgsqlConnection(ConnString);
        await conn.OpenAsync();
        NpgsqlCommand cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@senderId", senderId);
        cmd.Parameters.AddWithValue("@recipientId", recipientId);
        cmd.Parameters.AddWithValue("@message", message);
        cmd.Parameters.AddWithValue("@senttime", DateTime.UtcNow);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task<List<User>> GetFriendsSortedAsync(int userId, string authToken)
    {
        if (!VerifyAuthToken(authToken, userId))
            return new();

        List<User> users = new();
        string query = "select users.id, users.UserName, COALESCE(MAX(messages.id), 0) AS maxMessage from users " +
                       "LEFT JOIN messages ON (messages.senderid = @userId AND messages.recipientid = users.id) OR (messages.senderid = users.id AND messages.recipientid = @userId) " +
                       "WHERE users.UserName != 'admin' " +
                       "GROUP BY(users.id, users.username) " +
                       "ORDER BY maxMessage desc";
        await using var conn = new NpgsqlConnection(ConnString);
        await conn.OpenAsync();
        await using (var cmd = new NpgsqlCommand(query, conn))
        {
            cmd.Parameters.AddWithValue("@userId", userId);
            NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (reader.Read())
            {
                int id = reader.GetInt32(0);
                string username = reader.GetString(1);
                User user = new User();
                user.Id = id;
                user.Username = username;
                users.Add(user);
            }
            return users;
        }
    }

    public static async Task<List<User>> GetFriendsAsync(string authToken)
    {
        if (!VerifyAuthToken(authToken))
            return new();
        
        List<User> users = new List<User>();
        string query = "SELECT ID, UserName FROM USERS";
        await using var conn = new NpgsqlConnection(ConnString);
        await conn.OpenAsync();
        await using (var cmd = new NpgsqlCommand(query, conn))
        {
            NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (reader.Read())
            {
                int id = reader.GetInt32(0);
                string username = reader.GetString(1);
                User user = new User();
                user.Id = id;
                user.Username = username;
                users.Add(user);
            }
            return users;
        }
    }

    private static async Task ExecuteNonQueryAsync(string query)
    {
        await using var conn = new NpgsqlConnection(ConnString);
        await conn.OpenAsync();
        await using (var cmd = new NpgsqlCommand(query, conn))
        {
            await cmd.ExecuteNonQueryAsync();
        };
    }
}