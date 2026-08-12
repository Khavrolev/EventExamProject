using EventExamProject.DTOs.Booking;
using EventExamProject.DTOs.Event;
using EventExamProject.DTOs.Pagination;
using EventExamProject.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EventExamProject.Controllers;

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

        return Ok(new PaginatedResultDto<EventInfoDto>
        {
            Data = result.Data.Select(EventInfoDto.FromEvent).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        });
    }

    [HttpGet("{id:Guid}")]
    [ProducesResponseType(typeof(EventInfoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventInfoDto>> GetEventById(Guid id)
    {
        var eventById = await eventService.GetEventByIdAsync(id);

        return Ok(EventInfoDto.FromEvent(eventById));
    }

    [HttpPost]
    [ProducesResponseType(typeof(EventInfoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EventInfoDto>> CreateEvent(EventDto newEvent)
    {
        var created = await eventService.CreateEventAsync(newEvent);
        var eventInfo = EventInfoDto.FromEvent(created);

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
        var updated = await eventService.UpdateEventAsync(id, newEvent);

        return Ok(EventInfoDto.FromEvent(updated));
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
        var created = await bookingService.CreateBookingAsync(id);
        var bookingInfo = BookingInfoDto.FromBooking(created);

        return AcceptedAtAction(nameof(BookingController.GetBookingById), "Booking", new
        {
            id = bookingInfo.Id
        }, bookingInfo);
    }
}
