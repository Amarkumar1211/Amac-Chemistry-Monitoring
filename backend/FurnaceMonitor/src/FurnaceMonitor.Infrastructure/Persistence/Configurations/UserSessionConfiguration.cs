using FurnaceMonitor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FurnaceMonitor.Infrastructure.Persistence.Configurations;

public class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.ToTable("UserSessions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.JwtId)
            .HasMaxLength(100);

        builder.Property(s => s.IpAddress)
            .HasMaxLength(50);

        builder.Property(s => s.UserAgent)
            .HasMaxLength(500);

        builder.HasOne(s => s.User)
            .WithMany(u => u.Sessions)
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(s => s.RefreshToken)
            .HasMaxLength(500);
    }
}
