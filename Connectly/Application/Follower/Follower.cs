namespace Connectly.Application.Follower;

public class Follower
{
    public Follower(Guid userId, Guid followerId)
    {
        UserId = userId;
        FollowerId = followerId;
        Id = Guid.NewGuid();
    }
    
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid FollowerId { get; private set; }
}