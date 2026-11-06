using FurnaceMonitor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FurnaceMonitor.Infrastructure.Persistence.Configurations;

public class FurnaceBatchConfiguration : IEntityTypeConfiguration<FurnaceBatch>
{
    public void Configure(EntityTypeBuilder<FurnaceBatch> builder)
    {
        builder.ToTable("FurnaceBatches");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.BatchNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(b => b.Instruction)
            .HasMaxLength(500);

        builder.Property(b => b.Remarks)
            .HasMaxLength(4000);

        builder.Property(b => b.ClosedBy)
            .HasMaxLength(100);

        builder.Property(b => b.ColorCode)
            .HasMaxLength(50);

        builder.HasIndex(b => b.BatchNumber);

        builder.Ignore(b => b.Chemical);

        builder.HasOne(b => b.Furnace)
            .WithMany(f => f.Batches)
            .HasForeignKey(b => b.FurnaceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.Operator)
            .WithMany(u => u.Batches)
            .HasForeignKey(b => b.OperatorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
