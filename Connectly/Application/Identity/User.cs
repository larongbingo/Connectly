namespace Connectly.Application.Identity;

public class User
{
    public User(string username, string externalId)
    {
        Username = username;
        ExternalId = externalId;
        Id = Guid.NewGuid();
    }
    
    public Guid Id { get; private set; }
    public string Username { get; set; }
    public string ExternalId { get; set; }
    
    public FilteredUser ToFilteredUser() => new(Id, Username);
}