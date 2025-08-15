using Connectly.Application.Follower;
using Connectly.Application.Identity;
using Connectly.Application.Posts;

using Microsoft.EntityFrameworkCore;

namespace Connectly.Infrastructure.Data;

public class ConnectlyDbContext : DbContext
{
    public DbSet<User> Users { get; private set; }
    public DbSet<Follower> Followers { get; private set; }
    public DbSet<Post> Posts { get; private set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.UseNpgsql(Environment.GetEnvironmentVariable("CONNECTLY_DB_CONNECTION_STRING"));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ConnectlyDbContext).Assembly);
    }
}