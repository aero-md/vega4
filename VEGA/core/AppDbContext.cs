using Models.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Models.Entities;
using Npgsql.EntityFrameworkCore.PostgreSQL;

namespace Core;

public class AppDbContext : DbContext
{
    // Guild settings and triggers
    public DbSet<GuildSettings> GuildSettings { get; set; }
    public DbSet<Trigger> Triggers { get; set; }

    // Reminders
    //public DbSet<Reminder> Reminders { get; set; }

    // Feeds properties
    private const string FEED_TABLE_NAME = "feeds";
    public DbSet<FeedProperties> FeedProperties { get; set; }

    // Feeds recents posts history
    private const string FEED_HISTORY_TABLE_NAME = "feeds_recent_posts";
    public DbSet<FeedPostReceit> FeedHistory { get; set; }


    private VegaConfiguration _config { get; }


    public AppDbContext(VegaConfiguration config)
    {
        _config = config;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSnakeCaseNamingConvention()
                      .UseNpgsql(_config.DbConnexionString);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        /* Table names are derived from Entity name, using Snake Case convention */

        // Define Guidsettings entityId
        modelBuilder.Entity<GuildSettings>()
                    .HasKey(g => g.GuildId);

        // Define Trigger entityId
        modelBuilder.Entity<Trigger>()
                    .HasKey(t => t.TriggerId);

        // Define TriggerId default value : new GUID
        modelBuilder.Entity<Trigger>()
                    .Property(t => t.TriggerId)
                    .HasDefaultValueSql("gen_random_uuid()")  // Default value : new Guid 
                    .ValueGeneratedOnAdd();

        // Link Triggers to GuildSettings
        modelBuilder.Entity<GuildSettings>()
                    .HasMany(g => g.Triggers)           // Trigger list
                    .WithOne()                          // Navigation property not needed
                    .HasForeignKey(t => t.GuildId)      // Foreign Key
                    .OnDelete(DeleteBehavior.Cascade);  // On delete cascade

        /*
        // Define Reminder entityId
        modelBuilder.Entity<Reminder>()
                    .HasKey(r => r.ReminderId);

        // Define ReminderId default value : new GUID
        modelBuilder.Entity<Reminder>()
                    .Property(r => r.ReminderId)
                    .HasDefaultValueSql("gen_random_uuid()")  // Default value : new Guid 
                    .ValueGeneratedOnAdd();
        */
        
        // Define FeedProperties entityId with explicit column name
        modelBuilder.Entity<FeedProperties>()
                    .ToTable("feeds") // Set custom table name
                    .HasKey(t => t.FeedId);

        // Define Feedproperty default value : new GUID
        modelBuilder.Entity<FeedProperties>()
                    .Property(t => t.FeedId)
                    .HasDefaultValueSql("gen_random_uuid()")  // Default value : new Guid 
                    .ValueGeneratedOnAdd();
        
        // Define FeedHistoryPost composite key (FeedId + PostId)
        modelBuilder.Entity<FeedPostReceit>()
                    .ToTable("feeds_recent_posts") // Set custom table name
                    .HasKey(t => new { t.FeedId, t.PostId });
    }
}