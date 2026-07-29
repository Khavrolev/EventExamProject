using EventExamProject.Models;
using EventExamProject.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EventExamProject.Controllers;

[ApiController]
[Route("bookings")]
public class BookingController (IBookingService bookingService) : ControllerBase
{
    [HttpGet("{id:Guid}")]
    public async Task<ActionResult<Booking>> GetBookingById(Guid id)
    {
        var bookingById = await bookingService.GetBookingById(id);
        
        return Ok(bookingById);
    }
}