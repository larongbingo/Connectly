namespace Connectly.Application.Posts;

public class Post
{
    public Post(string content, Guid userId)
    {
        Content = content;
        UserId = userId;
        CreatedAt = DateTime.UtcNow;
        Id = Guid.NewGuid();
    }
    
    public Guid Id { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public string Content { get; private set; }
    public Guid UserId { get; private set; }
}