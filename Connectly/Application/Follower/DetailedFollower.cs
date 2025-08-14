namespace Connectly.Application.Follower;

public record DetailedFollower(Guid UserId, Guid FollowerId, string FollowerUsername);