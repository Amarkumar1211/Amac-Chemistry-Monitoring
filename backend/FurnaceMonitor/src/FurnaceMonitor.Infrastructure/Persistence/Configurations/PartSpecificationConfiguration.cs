using FurnaceMonitor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FurnaceMonitor.Infrastructure.Persistence.Configurations;

public class PartSpecificationConfiguration : IEntityTypeConfiguration<PartSpecification>
{
    public void Configure(EntityTypeBuilder<PartSpecification> builder)
    {
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.PartName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.PartNo).IsRequired().HasMaxLength(100);
        builder.Property(x => x.GradeName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.ColorCode).HasMaxLength(100);

        // Max lengths for JSON strings
        builder.Property(x => x.MinTargetsJson).HasMaxLength(2000);
        builder.Property(x => x.MaxTargetsJson).HasMaxLength(2000);
        builder.Property(x => x.CustomerMaxTargetsJson).HasMaxLength(2000);
        builder.Property(x => x.CustomerMinTargetsJson).HasMaxLength(2000);

        // A part spec is uniquely identified by its PartNo, PartName, and GradeName
        builder.HasIndex(x => new { x.PartNo, x.PartName, x.GradeName }).IsUnique();
    }
}
