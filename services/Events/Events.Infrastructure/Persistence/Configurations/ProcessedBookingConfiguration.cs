using Events.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Events.Infrastructure.Persistence.Configurations;

internal sealed class ProcessedBookingConfiguration : IEntityTypeConfiguration<ProcessedBooking>
{
    public void Configure(EntityTypeBuilder<ProcessedBooking> builder)
    {
        builder.ToTable("processed_bookings");

        builder.HasKey(p => p.BookingId);
        builder.Property(p => p.BookingId).ValueGeneratedNever();

        builder.Property(p => p.ProcessedAt).IsRequired();
    }
}
