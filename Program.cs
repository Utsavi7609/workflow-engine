using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddSingleton<IWorkflowService, WorkflowService>();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var app = builder.Build();

// Workflow Definition endpoints
app.MapPost("/api/workflows", async (CreateWorkflowRequest request, IWorkflowService service) =>
{
    try
    {
        var definition = service.CreateWorkflowDefinition(request.Name, request.States, request.Actions);
        return Results.Created($"/api/workflows/{definition.Id}", definition);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/api/workflows/{id}", (string id, IWorkflowService service) =>
{
    var definition = service.GetWorkflowDefinition(id);
    return definition != null ? Results.Ok(definition) : Results.NotFound();
});

app.MapGet("/api/workflows", (IWorkflowService service) =>
{
    return Results.Ok(service.GetAllWorkflowDefinitions());
});

// Workflow Instance endpoints
app.MapPost("/api/workflows/{definitionId}/instances", (string definitionId, IWorkflowService service) =>
{
    try
    {
        var instance = service.StartWorkflowInstance(definitionId);
        return Results.Created($"/api/instances/{instance.Id}", instance);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/api/instances/{id}", (string id, IWorkflowService service) =>
{
    var instance = service.GetWorkflowInstance(id);
    return instance != null ? Results.Ok(instance) : Results.NotFound();
});

app.MapGet("/api/instances", (IWorkflowService service) =>
{
    return Results.Ok(service.GetAllWorkflowInstances());
});

app.MapPost("/api/instances/{id}/execute", (string id, ExecuteActionRequest request, IWorkflowService service) =>
{
    try
    {
        var instance = service.ExecuteAction(id, request.ActionId);
        return Results.Ok(instance);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});
app.MapGet("/", () => Results.Ok("✅ Workflow Engine API is running. Visit /api/workflows etc."));

app.Run();

// Models
public class State
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsInitial { get; set; }
    public bool IsFinal { get; set; }
    public bool Enabled { get; set; } = true;
    public string? Description { get; set; }
}

public class Action
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public List<string> FromStates { get; set; } = new();
    public string ToState { get; set; } = string.Empty;
}

public class WorkflowDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<State> States { get; set; } = new();
    public List<Action> Actions { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class WorkflowInstance
{
    public string Id { get; set; } = string.Empty;
    public string DefinitionId { get; set; } = string.Empty;
    public string CurrentStateId { get; set; } = string.Empty;
    public List<HistoryEntry> History { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class HistoryEntry
{
    public string ActionId { get; set; } = string.Empty;
    public string ActionName { get; set; } = string.Empty;
    public string FromStateId { get; set; } = string.Empty;
    public string ToStateId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

// Request models
public class CreateWorkflowRequest
{
    public string Name { get; set; } = string.Empty;
    public List<State> States { get; set; } = new();
    public List<Action> Actions { get; set; } = new();
}

public class ExecuteActionRequest
{
    public string ActionId { get; set; } = string.Empty;
}

// Service Interface
public interface IWorkflowService
{
    WorkflowDefinition CreateWorkflowDefinition(string name, List<State> states, List<Action> actions);
    WorkflowDefinition? GetWorkflowDefinition(string id);
    List<WorkflowDefinition> GetAllWorkflowDefinitions();
    WorkflowInstance StartWorkflowInstance(string definitionId);
    WorkflowInstance? GetWorkflowInstance(string id);
    List<WorkflowInstance> GetAllWorkflowInstances();
    WorkflowInstance ExecuteAction(string instanceId, string actionId);
}

// Service Implementation
public class WorkflowService : IWorkflowService
{
    private readonly Dictionary<string, WorkflowDefinition> _definitions = new();
    private readonly Dictionary<string, WorkflowInstance> _instances = new();

    public WorkflowDefinition CreateWorkflowDefinition(string name, List<State> states, List<Action> actions)
    {
        // Validation
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Workflow name is required");

        if (!states.Any())
            throw new InvalidOperationException("At least one state is required");

        // Check for duplicate state IDs
        var duplicateStates = states.GroupBy(s => s.Id).Where(g => g.Count() > 1).Select(g => g.Key);
        if (duplicateStates.Any())
            throw new InvalidOperationException($"Duplicate state IDs found: {string.Join(", ", duplicateStates)}");

        // Check for duplicate action IDs
        var duplicateActions = actions.GroupBy(a => a.Id).Where(g => g.Count() > 1).Select(g => g.Key);
        if (duplicateActions.Any())
            throw new InvalidOperationException($"Duplicate action IDs found: {string.Join(", ", duplicateActions)}");

        // Must have exactly one initial state
        var initialStates = states.Where(s => s.IsInitial).ToList();
        if (initialStates.Count != 1)
            throw new InvalidOperationException("Exactly one initial state is required");

        // Validate actions reference valid states
        var stateIds = states.Select(s => s.Id).ToHashSet();
        foreach (var action in actions)
        {
            if (!stateIds.Contains(action.ToState))
                throw new InvalidOperationException($"Action {action.Id} references unknown target state: {action.ToState}");

            foreach (var fromState in action.FromStates)
            {
                if (!stateIds.Contains(fromState))
                    throw new InvalidOperationException($"Action {action.Id} references unknown source state: {fromState}");
            }
        }

        var definition = new WorkflowDefinition
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            States = states,
            Actions = actions
        };

        _definitions[definition.Id] = definition;
        return definition;
    }

    public WorkflowDefinition? GetWorkflowDefinition(string id)
    {
        return _definitions.TryGetValue(id, out var definition) ? definition : null;
    }

    public List<WorkflowDefinition> GetAllWorkflowDefinitions()
    {
        return _definitions.Values.ToList();
    }

    public WorkflowInstance StartWorkflowInstance(string definitionId)
    {
        var definition = GetWorkflowDefinition(definitionId);
        if (definition == null)
            throw new InvalidOperationException($"Workflow definition not found: {definitionId}");

        var initialState = definition.States.First(s => s.IsInitial);
        
        var instance = new WorkflowInstance
        {
            Id = Guid.NewGuid().ToString(),
            DefinitionId = definitionId,
            CurrentStateId = initialState.Id
        };

        _instances[instance.Id] = instance;
        return instance;
    }

    public WorkflowInstance? GetWorkflowInstance(string id)
    {
        return _instances.TryGetValue(id, out var instance) ? instance : null;
    }

    public List<WorkflowInstance> GetAllWorkflowInstances()
    {
        return _instances.Values.ToList();
    }

    public WorkflowInstance ExecuteAction(string instanceId, string actionId)
    {
        var instance = GetWorkflowInstance(instanceId);
        if (instance == null)
            throw new InvalidOperationException($"Workflow instance not found: {instanceId}");

        var definition = GetWorkflowDefinition(instance.DefinitionId);
        if (definition == null)
            throw new InvalidOperationException($"Workflow definition not found: {instance.DefinitionId}");

        var action = definition.Actions.FirstOrDefault(a => a.Id == actionId);
        if (action == null)
            throw new InvalidOperationException($"Action not found: {actionId}");

        if (!action.Enabled)
            throw new InvalidOperationException($"Action is disabled: {actionId}");

        var currentState = definition.States.First(s => s.Id == instance.CurrentStateId);
        
        // Check if current state is final
        if (currentState.IsFinal)
            throw new InvalidOperationException("Cannot execute actions on final states");

        // Check if action can be executed from current state
        if (!action.FromStates.Contains(instance.CurrentStateId))
            throw new InvalidOperationException($"Action {actionId} cannot be executed from state {instance.CurrentStateId}");

        // Execute the action
        var historyEntry = new HistoryEntry
        {
            ActionId = action.Id,
            ActionName = action.Name,
            FromStateId = instance.CurrentStateId,
            ToStateId = action.ToState
        };

        instance.CurrentStateId = action.ToState;
        instance.History.Add(historyEntry);

        return instance;
    }
}

