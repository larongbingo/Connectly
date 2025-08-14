using Connectly.Application.Follower;
using Connectly.Application.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Connectly.Infrastructure.Data.Configurations;

public class FollowersConfiguration : IEntityTypeConfiguration<Follower>
{
    public void Configure(EntityTypeBuilder<Follower> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.FollowerId);
    }
}