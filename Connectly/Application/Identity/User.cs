namespace Connectly.Application.Identity;

public class User
{
    public User(string username, string externalId)
    {
        Username = username;
        ExternalId = externalId;
        Id = Guid.NewGuid();
    }

    public Guid Id { get; }
    public string Username { get; set; }
    public string ExternalId { get; private set; }

    public FilteredUser ToFilteredUser()
    {
        return new FilteredUser(Id, Username);
    }
}