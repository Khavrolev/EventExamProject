using EventExamProject.DTOs.Event;
using EventExamProject.DTOs.Pagination;
using EventExamProject.Models;
using EventExamProject.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EventExamProject.Controllers;

[ApiController]
[Route("events")]
public class EventsController(IEventService eventService, IBookingService bookingService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<Event>>> GetAllEvents(
        [FromQuery] EventFilterDto filter,
        [FromQuery] PaginationParams paginationParams)
    {
        return Ok(await eventService.GetAllEvents(filter, paginationParams));
    }
    
    [HttpGet("{id:Guid}")]
    public async Task<ActionResult<Event>> GetEventById(Guid id)
    {
        var eventById = await eventService.GetEventById(id);
        
        return Ok(eventById);
    }

    [HttpPost]
    public async Task<ActionResult<Event>> CreateEvent(EventDto newEvent)
    {
        var created = await eventService.CreateEvent(newEvent);

        return CreatedAtAction(nameof(GetEventById), new
        {
            id = created.Id
        }, created);
    }

    [HttpPut("{id:Guid}")]
    public async Task<ActionResult<Event>> UpdateEvent(Guid id, EventDto newEvent)
    {
        var updated = await eventService.UpdateEvent(id, newEvent);
        
        return Ok(updated);
    }

    [HttpDelete("{id:Guid}")]
    public async Task<ActionResult> DeleteEvent(Guid id)
    {
        await eventService.DeleteEvent(id);
        
        return NoContent();
    }
    
    [HttpPost("{id:Guid}/book")]
    public async Task<ActionResult<Booking>> CreateBooking(Guid id)
    {
        var created = await bookingService.CreateBooking(id);

        return AcceptedAtAction(nameof(BookingController.GetBookingById), "Booking", new
        {
            id = created.Id
        }, created);
    }
}