using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TriggerService.API.Domain;
using TriggerService.API.DTOs;
using TriggerService.API.Infrastructure;
using TriggerService.API.Services;

namespace TriggerService.API.Controllers;

[ApiController]
[Route("api/triggers")]
[Authorize]
public class TriggersController : ControllerBase
{
    private readonly TriggerDbContext _db;
    private readonly TriggerPublisher _publisher;

    public TriggersController(TriggerDbContext db, TriggerPublisher publisher)
    {
        _db = db;
        _publisher = publisher;
    }

    /// <summary>List all triggers for a workflow.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TriggerResponse>>> GetAll([FromQuery] Guid? workflowId)
    {
        var query = _db.Triggers.AsQueryable();
        if (workflowId.HasValue) query = query.Where(t => t.WorkflowId == workflowId.Value);

        var triggers = await query.ToListAsync();
        return Ok(triggers.Select(TriggerResponse.From));
    }

    /// <summary>Get a single trigger.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TriggerResponse>> GetById(Guid id)
    {
        var trigger = await _db.Triggers.FindAsync(id);
        if (trigger is null) return NotFound();
        return Ok(TriggerResponse.From(trigger));
    }

    /// <summary>Create a new trigger for a workflow.</summary>
    [HttpPost]
    public async Task<ActionResult<TriggerResponse>> Create(CreateTriggerRequest request)
    {
        if (request.Type == TriggerType.Schedule && string.IsNullOrWhiteSpace(request.CronExpression))
        {
            return BadRequest("CronExpression is required for Schedule triggers.");
        }

        var trigger = new Trigger
        {
            WorkflowId = request.WorkflowId,
            Type = request.Type,
            CronExpression = request.CronExpression,
            ConfigJson = request.ConfigJson,
            IsActive = true
        };

        _db.Triggers.Add(trigger);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = trigger.Id }, TriggerResponse.From(trigger));
    }

    /// <summary>Enable or disable a trigger.</summary>
    [HttpPost("{id:guid}/active")]
    public async Task<IActionResult> SetActive(Guid id, [FromQuery] bool active = true)
    {
        var trigger = await _db.Triggers.FindAsync(id);
        if (trigger is null) return NotFound();

        trigger.IsActive = active;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Delete a trigger.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var trigger = await _db.Triggers.FindAsync(id);
        if (trigger is null) return NotFound();

        _db.Triggers.Remove(trigger);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Manually fire a trigger (any type). Requires authentication.
    /// This is the primary way to test a workflow end-to-end.
    /// </summary>
    [HttpPost("{id:guid}/fire")]
    public async Task<ActionResult<FireTriggerResponse>> Fire(Guid id, FireTriggerRequest request)
    {
        var trigger = await _db.Triggers.FindAsync(id);
        if (trigger is null) return NotFound();

        if (!trigger.IsActive) return BadRequest("Trigger is not active.");

        var runId = await _publisher.FireAsync(trigger, request.PayloadJson);
        return Ok(new FireTriggerResponse { RunId = runId });
    }
}

/// <summary>
/// Public (unauthenticated) webhook endpoint. In production, secure with a
/// per-trigger secret/HMAC validated against trigger.ConfigJson.
/// </summary>
[ApiController]
[Route("api/triggers/webhook")]
[AllowAnonymous]
public class WebhookController : ControllerBase
{
    private readonly TriggerDbContext _db;
    private readonly TriggerPublisher _publisher;

    public WebhookController(TriggerDbContext db, TriggerPublisher publisher)
    {
        _db = db;
        _publisher = publisher;
    }

    [HttpPost("{triggerId:guid}")]
    public async Task<ActionResult<FireTriggerResponse>> Receive(Guid triggerId, [FromBody] object? payload)
    {
        var trigger = await _db.Triggers.FindAsync(triggerId);
        if (trigger is null) return NotFound();

        if (!trigger.IsActive) return BadRequest("Trigger is not active.");
        if (trigger.Type != TriggerType.Webhook) return BadRequest("Trigger is not a webhook trigger.");

        var payloadJson = payload is null ? null : System.Text.Json.JsonSerializer.Serialize(payload);
        var runId = await _publisher.FireAsync(trigger, payloadJson);

        return Ok(new FireTriggerResponse { RunId = runId });
    }
}
