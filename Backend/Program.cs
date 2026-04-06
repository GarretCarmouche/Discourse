using System.Text.Json;
using DiscourseBackend;
using DiscourseBackend.DataModels;

// Initialize schema
await API.InitializeSchemaAsync();
await API.CreateDefaultUserAsync();

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/getFriends", API.GetFriendsAsync);
app.MapPost("/sendMessage", API.SendMessageAsync);
app.MapGet("/getMessages", API.GetMessagesAsync);
app.MapPost("/createUser", API.CreateUserAsync);
app.MapGet("/getToken", API.GetAccessTokenAsync);
app.MapGet("/getUserIdFromName", API.GetUserIdFromNameAsync);
app.MapGet("/doesMessageExistAfter", API.DoesMessageExistsAfterAsync);
app.MapGet("/getFriendsSorted", API.GetFriendsSortedAsync);
app.MapGet("/getUnreadMessageCount", API.GetUnreadMessageCountAsync);
app.MapPost("/markMessagesRead", API.MarkMessagesReadAsync);
app.MapPost("/sendMessageWithAttachments", API.SendMessageWithAttachmentsAsync);
app.MapGet("/downloadFile", API.DownloadFileAsync);
app.MapGet("/fileExists", API.FileExistsAsync);
app.MapPost("/changePassword", API.ChangePasswordAsync);
app.Run();