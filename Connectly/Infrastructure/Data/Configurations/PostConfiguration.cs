using Connectly.Application.Identity;
using Connectly.Application.Posts;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Connectly.Infrastructure.Data.Configurations;

public class PostConfiguration : IEntityTypeConfiguration<Post>
{
    public void Configure(EntityTypeBuilder<Post> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId);
        builder.Property(x => x.Content).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
    }
}