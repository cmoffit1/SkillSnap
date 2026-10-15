using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using SkillSnap.Api.Models;
using SkillSnap.Api.Auth;

namespace SkillSnap.Api.Data;

public class SkillSnapContext : IdentityDbContext<ApplicationUser>
{
    public SkillSnapContext(DbContextOptions<SkillSnapContext> options) : base(options)
    {
    }

    public DbSet<PortfolioUser> PortfolioUsers { get; set; } = null!;
    public DbSet<Project> Projects { get; set; } = null!;
    public DbSet<Skill> Skills { get; set; } = null!;
    public DbSet<SkillLevel> SkillLevels { get; set; } = null!;

    public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<SkillLevel>(entity =>
        {
            entity.HasIndex(level => level.Name).IsUnique();
            entity.HasData(
                new SkillLevel { Id = 1, Name = "Beginner", SortOrder = 1 },
                new SkillLevel { Id = 2, Name = "Intermediate", SortOrder = 2 },
                new SkillLevel { Id = 3, Name = "Advanced", SortOrder = 3 },
                new SkillLevel { Id = 4, Name = "Expert", SortOrder = 4 });
        });

        builder.Entity<Skill>()
            .HasOne(skill => skill.SkillLevel)
            .WithMany()
            .HasForeignKey(skill => skill.SkillLevelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.HasIndex(user => user.PortfolioUserId).IsUnique();
            entity.HasOne(user => user.PortfolioUser)
                .WithMany()
                .HasForeignKey(user => user.PortfolioUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<RefreshToken>(entity =>
        {
            entity.HasIndex(token => token.TokenHash).IsUnique();
            entity.Property(token => token.TokenHash).HasMaxLength(64).IsRequired();
            entity.Property(token => token.RevokedUtc).IsConcurrencyToken();
            entity.HasOne(token => token.User)
                .WithMany()
                .HasForeignKey(token => token.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}