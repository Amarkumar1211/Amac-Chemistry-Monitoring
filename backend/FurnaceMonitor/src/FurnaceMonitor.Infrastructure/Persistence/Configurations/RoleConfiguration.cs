using FurnaceMonitor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FurnaceMonitor.Infrastructure.Persistence.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(r => r.Description)
            .HasMaxLength(250);

        builder.HasIndex(r => r.Name)
            .IsUnique();

        builder.HasData(
            new Role
            {
                Id = 1,
                Name = "Admin",
                Description = "Full management rights",
                RoleType = Domain.Enums.UserRole.Admin,
                CreatedAtUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new Role
            {
                Id = 2,
                Name = "Operator",
                Description = "Operates furnaces and batches",
                RoleType = Domain.Enums.UserRole.Operator,
                CreatedAtUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new Role
            {
                Id = 3,
                Name = "Viewer",
                Description = "Read-only display access",
                RoleType = Domain.Enums.UserRole.Viewer,
                CreatedAtUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            });
    }
}
