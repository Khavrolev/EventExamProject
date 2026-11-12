using Bookings.Application.DTOs;
using Bookings.Application.Services.Interfaces;
using Bookings.Presentation.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookings.Presentation.Controllers;

[ApiController]
[Authorize]
public class BookingController(IBookingService bookingService) : ControllerBase
{
    [HttpPost("bookings")]
    [ProducesResponseType(typeof(BookingInfoDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BookingInfoDto>> CreateBooking([FromBody] CreateBookingDto dto)
    {
        var bookingInfo = await bookingService.CreateBookingAsync(dto.EventId, User.GetUserId());

        return AcceptedAtAction(nameof(GetBookingById), new { id = bookingInfo.Id }, bookingInfo);
    }

    [HttpGet("bookings/{id:Guid}")]
    [ProducesResponseType(typeof(BookingInfoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingInfoDto>> GetBookingById(Guid id)
    {
        var bookingById = await bookingService.GetBookingByIdAsync(id, User.GetUserId(), User.GetUserRole());

        return Ok(bookingById);
    }

    [HttpDelete("bookings/{id:Guid}")]
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
