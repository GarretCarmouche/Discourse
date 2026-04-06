namespace DiscourseBackend.DataModels;

public class Message
{
    public int Id { get; set; }
    public string Content { get; set; } = "!";
    public int UserId { get; set; }
    public string Username { get; set; } = "!";
    public DateTime Date { get; set; }
    public Dictionary<string, string> Attachments { get; set; } = new Dictionary<string, string>();
}