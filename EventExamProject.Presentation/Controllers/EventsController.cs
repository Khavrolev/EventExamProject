using EventExamProject.Application.DTOs.Booking;
using EventExamProject.Application.DTOs.Event;
using EventExamProject.Application.DTOs.Pagination;
using EventExamProject.Application.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EventExamProject.Presentation.Controllers;

[ApiController]
[Route("events")]
public class EventsController(IEventService eventService, IBookingService bookingService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResultDto<EventInfoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaginatedResultDto<EventInfoDto>>> GetAllEvents(
        [FromQuery] EventFilterDto filter,
        [FromQuery] PaginationParamsDto paginationParams)
    {
        var result = await eventService.GetAllEventsAsync(filter, paginationParams);

        return Ok(result);
    }

    [HttpGet("{id:Guid}")]
    [ProducesResponseType(typeof(EventInfoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventInfoDto>> GetEventById(Guid id)
    {
        var eventInfo = await eventService.GetEventByIdAsync(id);

        return Ok(eventInfo);
    }

    [HttpPost]
    [ProducesResponseType(typeof(EventInfoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EventInfoDto>> CreateEvent(EventDto newEvent)
    {
        var eventInfo = await eventService.CreateEventAsync(newEvent);

        return CreatedAtAction(nameof(GetEventById), new
        {
            id = eventInfo.Id
        }, eventInfo);
    }

    [HttpPut("{id:Guid}")]
    [ProducesResponseType(typeof(EventInfoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventInfoDto>> UpdateEvent(Guid id, EventDto newEvent)
    {
        var eventInfo = await eventService.UpdateEventAsync(id, newEvent);

        return Ok(eventInfo);
    }

    [HttpDelete("{id:Guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteEvent(Guid id)
    {
        await eventService.DeleteEventAsync(id);

        return NoContent();
    }

    [HttpPost("{id:Guid}/book")]
    [ProducesResponseType(typeof(BookingInfoDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BookingInfoDto>> CreateBooking(Guid id)
    {
        var bookingInfo = await bookingService.CreateBookingAsync(id);

        return AcceptedAtAction(nameof(BookingController.GetBookingById), "Booking", new
        {
            id = bookingInfo.Id
        }, bookingInfo);
    }
}
