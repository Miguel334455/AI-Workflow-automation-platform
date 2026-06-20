using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkflowService.API.Domain;
using WorkflowService.API.DTOs;
using WorkflowService.API.Infrastructure;

namespace WorkflowService.API.Controllers;

[ApiController]
[Route("api/workflows")]
[Authorize]
public class WorkflowsController : ControllerBase
{
    private readonly WorkflowDbContext _db;

    public WorkflowsController(WorkflowDbContext db)
    {
        _db = db;
    }

    /// <summary>List all workflows owned by the current user.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<WorkflowResponse>>> GetAll()
    {
        var ownerId = GetUserId();

        var workflows = await _db.Workflows
            .Include(w => w.Nodes)
            .Include(w => w.Connections)
            .Where(w => w.OwnerUserId == ownerId)
            .ToListAsync();

        return Ok(workflows.Select(ToResponse));
    }

    /// <summary>Get a single workflow by id.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WorkflowResponse>> GetById(Guid id)
    {
        var workflow = await _db.Workflows
            .Include(w => w.Nodes)
            .Include(w => w.Connections)
            .FirstOrDefaultAsync(w => w.Id == id && w.OwnerUserId == GetUserId());

        if (workflow is null) return NotFound();

        return Ok(ToResponse(workflow));
    }

    /// <summary>
    /// Returns the workflow definition in the shared DTO shape used by the
    /// Execution Service to walk the graph. Intended for service-to-service calls.
    /// </summary>
    [HttpGet("{id:guid}/definition")]
    [AllowAnonymous] // internal call from Execution Service; lock down via network/API gateway in production
    public async Task<ActionResult> GetDefinition(Guid id)
    {
        var workflow = await _db.Workflows
            .Include(w => w.Nodes)
            .Include(w => w.Connections)
            .FirstOrDefaultAsync(w => w.Id == id);

        if (workflow is null) return NotFound();

        return Ok(ToResponse(workflow).ToDefinitionDto());
    }

    /// <summary>Create a new workflow with its nodes and connections.</summary>
    [HttpPost]
    public async Task<ActionResult<WorkflowResponse>> Create(CreateWorkflowRequest request)
    {
        var workflow = new Workflow
        {
            Name = request.Name,
            Description = request.Description,
            OwnerUserId = GetUserId(),
            IsActive = false,
            Nodes = request.Nodes.Select(n => new WorkflowNode
            {
                Id = n.Id ?? Guid.NewGuid(),
                Name = n.Name,
                Type = n.Type,
                ConfigJson = n.ConfigJson,
                Order = n.Order
            }).ToList(),
        };

        // Connections reference node IDs, so build them after nodes have IDs assigned.
        workflow.Connections = request.Connections.Select(c => new WorkflowConnection
        {
            FromNodeId = c.FromNodeId,
            ToNodeId = c.ToNodeId,
            Condition = c.Condition
        }).ToList();

        _db.Workflows.Add(workflow);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = workflow.Id }, ToResponse(workflow));
    }

    /// <summary>Update an existing workflow, replacing its nodes and connections.</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<WorkflowResponse>> Update(Guid id, UpdateWorkflowRequest request)
    {
        var workflow = await _db.Workflows
            .Include(w => w.Nodes)
            .Include(w => w.Connections)
            .FirstOrDefaultAsync(w => w.Id == id && w.OwnerUserId == GetUserId());

        if (workflow is null) return NotFound();

        workflow.Name = request.Name;
        workflow.Description = request.Description;
        workflow.IsActive = request.IsActive;
        workflow.UpdatedAtUtc = DateTime.UtcNow;

        // Replace nodes/connections (simple strategy; revisit for partial updates).
        _db.WorkflowNodes.RemoveRange(workflow.Nodes);
        _db.WorkflowConnections.RemoveRange(workflow.Connections);

        workflow.Nodes = request.Nodes.Select(n => new WorkflowNode
        {
            Id = n.Id ?? Guid.NewGuid(),
            WorkflowId = workflow.Id,
            Name = n.Name,
            Type = n.Type,
            ConfigJson = n.ConfigJson,
            Order = n.Order
        }).ToList();

        workflow.Connections = request.Connections.Select(c => new WorkflowConnection
        {
            WorkflowId = workflow.Id,
            FromNodeId = c.FromNodeId,
            ToNodeId = c.ToNodeId,
            Condition = c.Condition
        }).ToList();

        await _db.SaveChangesAsync();

        return Ok(ToResponse(workflow));
    }

    /// <summary>Activate or deactivate a workflow (controls whether triggers fire).</summary>
    [HttpPost("{id:guid}/activate")]
    public async Task<IActionResult> SetActive(Guid id, [FromQuery] bool active = true)
    {
        var workflow = await _db.Workflows
            .FirstOrDefaultAsync(w => w.Id == id && w.OwnerUserId == GetUserId());

        if (workflow is null) return NotFound();

        workflow.IsActive = active;
        workflow.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>Delete a workflow and its nodes/connections.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var workflow = await _db.Workflows
            .FirstOrDefaultAsync(w => w.Id == id && w.OwnerUserId == GetUserId());

        if (workflow is null) return NotFound();

        _db.Workflows.Remove(workflow);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    private string GetUserId()
    {
        // "sub" claim from JWT identifies the owning user.
        return User.FindFirst("sub")?.Value
               ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
               ?? "anonymous";
    }

    private static WorkflowResponse ToResponse(Workflow w) => new()
    {
        Id = w.Id,
        Name = w.Name,
        Description = w.Description,
        IsActive = w.IsActive,
        CreatedAtUtc = w.CreatedAtUtc,
        UpdatedAtUtc = w.UpdatedAtUtc,
        Nodes = w.Nodes.Select(n => new WorkflowNodeRequest
        {
            Id = n.Id,
            Name = n.Name,
            Type = n.Type,
            ConfigJson = n.ConfigJson,
            Order = n.Order
        }).ToList(),
        Connections = w.Connections.Select(c => new WorkflowConnectionRequest
        {
            FromNodeId = c.FromNodeId,
            ToNodeId = c.ToNodeId,
            Condition = c.Condition
        }).ToList()
    };
}
