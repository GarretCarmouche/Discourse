using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;

namespace BlazorApp2;
using System.Net.Http.Json;
    
public class API
{
    private User? CurrentUser;
    public string authToken = "";
    
    public User? GetCurrentUser()
    {
        return CurrentUser;
    }

    public async Task<bool> DoesMessageExistAfterAsync(User user, int messageId)
    {
        var client = new HttpClient();
        client.BaseAddress = new Uri("http://discoursebackend:8080/");
        HttpResponseMessage response = await client.GetAsync($"doesMessageExistAfter?user1Id={CurrentUser.Id}&user2Id={user.Id}&messageId={messageId}&authToken={authToken}");
        response.EnsureSuccessStatusCode();
    
        var jsonResponse = await response.Content.ReadAsStringAsync();
        
        if (response.IsSuccessStatusCode)
        {
            return bool.Parse(await response.Content.ReadAsStringAsync());
        }

        return false;
    }
    
    public async Task<int> GetUserIdFromNameAsync(string username)
    {
        var client = new HttpClient();
        client.BaseAddress = new Uri("http://discoursebackend:8080/");
        HttpResponseMessage response = await client.GetAsync($"getUserIdFromName?username={username}&authToken={authToken}");
        response.EnsureSuccessStatusCode();
    
        var jsonResponse = await response.Content.ReadAsStringAsync();
        
        if (response.IsSuccessStatusCode)
        {
            return int.Parse(await response.Content.ReadAsStringAsync());
        }

        return -1;
    }

    public async Task CreateUserAsync(string userName, string password)
    {
        var client = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Post, $"http://discoursebackend:8080/createUser?username={userName}&password={password}&authToken={authToken}");
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }
    
    public async Task<bool> LoginAsync(string userName, string password)
    {
        var client = new HttpClient();
        client.BaseAddress = new Uri("http://discoursebackend:8080/");
        HttpResponseMessage response = await client.GetAsync($"getToken?username={userName}&password={password}&authToken={authToken}");
        response.EnsureSuccessStatusCode();
        
        if (response.IsSuccessStatusCode)
        {
            authToken = await response.Content.ReadAsStringAsync();
            if (authToken.Length > 0)
            {
                CurrentUser = new User();
                CurrentUser.Id = await GetUserIdFromNameAsync(userName);
                CurrentUser.UserName = userName;
            }
            else
            {
                CurrentUser = null;
            }
            
            return authToken.Length > 0;
        }
        
        return false;
    }
    
    public async Task<List<User>> GetFriendsSortedAsync()
    {
        var client = new HttpClient();
        client.BaseAddress = new Uri("http://discoursebackend:8080/");
        HttpResponseMessage response = await client.GetAsync($"getFriendsSorted?userId={GetCurrentUser()!.Id}&authToken={authToken}");
        response.EnsureSuccessStatusCode();
        
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<List<User>>() ?? [];
        }
        
        return [];
    }
    
    public async Task<List<User>> GetFriendsAsync()
    {
        var client = new HttpClient();
        client.BaseAddress = new Uri("http://discoursebackend:8080/");
        HttpResponseMessage response = await client.GetAsync($"getFriends?authToken={authToken}");
        response.EnsureSuccessStatusCode();
        
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<List<User>>() ?? [];
        }
        
        return [];
    }

    public async Task<bool> ChangePasswordAsync(string oldPassword, string newPassword)
    {
        Console.WriteLine($"Change password {oldPassword} {newPassword}");
        var client = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Post, $"http://discoursebackend:8080/changePassword?username={CurrentUser!.UserName}&oldPassword={oldPassword}&newPassword={newPassword}&authToken={authToken}");
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<bool>();
        }

        return false;
    }

    public async Task<bool> FileExistsAsync(string filePath)
    {
        HttpClient client = new HttpClient();
        client.BaseAddress = new Uri("http://discoursebackend:8080/");
        HttpResponseMessage response = await client.GetAsync($"fileExists?filepath={filePath}&authToken={authToken}");
        response.EnsureSuccessStatusCode();
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<bool>();
        }

        return false;
    }
    
    public async Task SendMessageWithAttachmentsAsync(User user, string message,
        Dictionary<string, string> fileNamePaths)
    {
        var client = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Post, $"http://discoursebackend:8080/sendMessageWithAttachments?senderId={GetCurrentUser().Id}&authToken={authToken}" +
                                                              $"&recipientId={user.Id}&message={message}&fileNamePathsJson={JsonSerializer.Serialize(fileNamePaths)}");
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }
    
    public async Task SendMessageAsync(User user, string message)
    {
        var client = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Post, $"http://discoursebackend:8080/sendMessage?senderId={GetCurrentUser().Id}&authToken={authToken}" +
                                                              $"&recipientId={user.Id}&message={message}");
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    public async Task MarkMessagesReadAsync(User user)
    {
        var client = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Post, $"http://discoursebackend:8080/markMessagesRead?senderId={user.Id}&recipientId={GetCurrentUser().Id}&authToken={authToken}");
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }
    
    public async Task<int> GetUnreadMessageCount(User user)
    {
        var client = new HttpClient();
        client.BaseAddress = new Uri("http://discoursebackend:8080/");
        HttpResponseMessage response = await client.GetAsync($"getUnreadMessageCount?senderId={user.Id}&recipientId={GetCurrentUser().Id}&authToken={authToken}");
        response.EnsureSuccessStatusCode();
        
        if (response.IsSuccessStatusCode)
        {
            return int.Parse(await response.Content.ReadAsStringAsync());
        }

        return 0;
    }
    
    public async Task<List<Message>> GetMessagesAsync(User user)
    {
        var client = new HttpClient();
        client.BaseAddress = new Uri("http://discoursebackend:8080/");
        HttpResponseMessage response = await client.GetAsync($"getMessages?user1Id={user.Id}&user2Id={GetCurrentUser().Id}&authToken={authToken}");
        response.EnsureSuccessStatusCode();
        
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<List<Message>>() ?? [];
        }
        
        return [];
    }
}