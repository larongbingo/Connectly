using Connectly.Application.Follower;
using Connectly.Application.Identity;
using Connectly.Application.Posts;

using Microsoft.EntityFrameworkCore;

namespace Connectly.Infrastructure.Data;

public class ConnectlyDbContext : DbContext
{
#pragma warning disable S1144
    public DbSet<User> Users { get; private set; }
    public DbSet<Follower> Followers { get; private set; }
    public DbSet<Post> Posts { get; private set; }
#pragma warning restore S1144
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