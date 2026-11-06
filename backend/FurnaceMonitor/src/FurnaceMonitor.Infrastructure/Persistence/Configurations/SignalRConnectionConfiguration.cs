using FurnaceMonitor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FurnaceMonitor.Infrastructure.Persistence.Configurations;

public class SignalRConnectionConfiguration : IEntityTypeConfiguration<SignalRConnection>
{
    public void Configure(EntityTypeBuilder<SignalRConnection> builder)
    {
        builder.ToTable("SignalRConnections");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.ConnectionId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.FurnaceNumber)
            .HasMaxLength(10);

        builder.Property(c => c.UserAgent)
            .HasMaxLength(500);

        builder.Property(c => c.IpAddress)
            .HasMaxLength(50);

        builder.HasIndex(c => c.ConnectionId)
            .IsUnique();

        builder.HasIndex(c => c.IsConnected);
    }
}
