using EventExamProject.DTOs.Booking;
using EventExamProject.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EventExamProject.Controllers;

[ApiController]
[Route("bookings")]
public class BookingController (IBookingService bookingService) : ControllerBase
{
    [HttpGet("{id:Guid}")]
    [ProducesResponseType(typeof(BookingInfoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingInfoDto>> GetBookingById(Guid id)
    {
        var bookingById = await bookingService.GetBookingByIdAsync(id);

        return Ok(BookingInfoDto.FromBooking(bookingById));
    }
}
