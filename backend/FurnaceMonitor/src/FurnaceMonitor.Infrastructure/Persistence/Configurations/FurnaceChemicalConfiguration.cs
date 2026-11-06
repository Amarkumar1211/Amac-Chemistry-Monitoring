using FurnaceMonitor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FurnaceMonitor.Infrastructure.Persistence.Configurations;

public class FurnaceChemicalConfiguration : IEntityTypeConfiguration<FurnaceChemical>
{
    public void Configure(EntityTypeBuilder<FurnaceChemical> builder)
    {
        builder.ToTable("FurnaceChemicals");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Si)
            .HasPrecision(18, 4);

        builder.Property(c => c.Ti)
            .HasPrecision(18, 4);

        builder.Property(c => c.Mg)
            .HasPrecision(18, 4);

        builder.Property(c => c.FutureChemical1)
            .HasPrecision(18, 4);

        builder.Property(c => c.FutureChemical2)
            .HasPrecision(18, 4);

        builder.Property(c => c.FutureChemical3)
            .HasPrecision(18, 4);

        builder.Property(c => c.Instruction)
            .HasMaxLength(500);

        builder.Property(c => c.Remarks)
            .HasMaxLength(4000);

        builder.Property(c => c.ColorCode)
            .HasMaxLength(50);

        builder.HasOne(c => c.Batch)
            .WithMany(b => b.ChemicalHistory)
            .HasForeignKey(c => c.BatchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => new { c.BatchId, c.CapturedAtUtc });
    }
}
