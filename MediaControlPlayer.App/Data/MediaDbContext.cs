using MediaControlPlayer.App.Models;
using Microsoft.EntityFrameworkCore;

namespace MediaControlPlayer.App.Data;

public sealed class MediaDbContext : DbContext
{
    public MediaDbContext(DbContextOptions<MediaDbContext> options)
        : base(options)
    {
    }

    public DbSet<MediaContent> MediaContents => Set<MediaContent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<MediaContent>(entity =>
        {
            entity.ToTable("MediaContents");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Name)
                  .IsRequired()
                  .HasMaxLength(200);

            entity.Property(x => x.Type)
                  .IsRequired();

            entity.Property(x => x.VideoPath)
                  .HasMaxLength(1024);

            entity.Property(x => x.ImagePath)
                  .HasMaxLength(1024);

            entity.Property(x => x.AudioPath)
                  .HasMaxLength(1024);

            entity.Property(x => x.Description)
                  .HasMaxLength(512);

            entity.Property(x => x.IsEnabled)
                  .IsRequired()
                  .HasDefaultValue(true);

            entity.Property(x => x.PlayOrder)
                  .IsRequired()
                  .HasDefaultValue(0);
        });
    }

    /// <summary>为已有数据库添加 IsEnabled、PlayOrder 列（若不存在）</summary>
    public void MigrateAddPlaylistColumns()
    {
        try { Database.ExecuteSqlRaw("ALTER TABLE MediaContents ADD COLUMN IsEnabled INTEGER NOT NULL DEFAULT 1"); }
        catch { /* 列已存在或新库，忽略 */ }
        try { Database.ExecuteSqlRaw("ALTER TABLE MediaContents ADD COLUMN PlayOrder INTEGER NOT NULL DEFAULT 0"); }
        catch { /* 列已存在或新库，忽略 */ }
    }
}

