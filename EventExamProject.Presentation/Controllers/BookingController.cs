using EventExamProject.Application.DTOs.Booking;
using EventExamProject.Application.Services.Interfaces;
using EventExamProject.Presentation.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventExamProject.Presentation.Controllers;

[ApiController]
[Authorize]
[Route("bookings")]
public class BookingController (IBookingService bookingService) : ControllerBase
{
    [HttpGet("{id:Guid}")]
    [ProducesResponseType(typeof(BookingInfoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingInfoDto>> GetBookingById(Guid id)
    {
        var bookingById = await bookingService.GetBookingByIdAsync(id);

        return Ok(bookingById);
    }

    [HttpDelete("{id:Guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelBooking(Guid id)
    {
        await bookingService.CancelBookingAsync(id, User.GetUserId(), User.GetUserRole());

        return NoContent();
    }
}
