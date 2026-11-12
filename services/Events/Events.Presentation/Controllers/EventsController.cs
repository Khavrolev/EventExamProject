using Events.Application.DTOs;
using Events.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Events.Presentation.Controllers;

[ApiController]
[Route("events")]
public class EventsController(IEventService eventService) : ControllerBase
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
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(EventInfoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EventInfoDto>> CreateEvent([FromBody] EventDto newEvent)
    {
        var eventInfo = await eventService.CreateEventAsync(newEvent);

        return CreatedAtAction(nameof(GetEventById), new { id = eventInfo.Id }, eventInfo);
    }

    [HttpPut("{id:Guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(EventInfoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventInfoDto>> UpdateEvent(Guid id, [FromBody] EventDto updatedEvent)
    {
        var eventInfo = await eventService.UpdateEventAsync(id, updatedEvent);
        return Ok(eventInfo);
    }

    [HttpDelete("{id:Guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteEvent(Guid id)
    {
        await eventService.DeleteEventAsync(id);
        return NoContent();
    }
}
