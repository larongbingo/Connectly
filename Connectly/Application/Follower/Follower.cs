namespace Connectly.Application.Follower;

#pragma warning disable CA1724
public class Follower
{
    public Follower(Guid userId, Guid followerId)
    {
        UserId = userId;
        FollowerId = followerId;
        Id = Guid.NewGuid();
    }

    public Guid Id { get; private set; }

    /// <summary>
    ///     ID of the user who is followed
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    ///     ID of the user who is following
    /// </summary>
    public Guid FollowerId { get; private set; }
}