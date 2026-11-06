using FurnaceMonitor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FurnaceMonitor.Infrastructure.Persistence.Configurations;

public class FurnaceConfiguration : IEntityTypeConfiguration<Furnace>
{
    public void Configure(EntityTypeBuilder<Furnace> builder)
    {
        builder.ToTable("Furnaces");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.Number)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(f => f.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(f => f.Location)
            .HasMaxLength(150);

        builder.HasIndex(f => f.Number)
            .IsUnique();
    }
}
