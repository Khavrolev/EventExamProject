using System.ComponentModel.DataAnnotations;

namespace Bookings.Application.DTOs;

public class CreateBookingDto
{
    [Required]
    public Guid EventId { get; set; }
}
