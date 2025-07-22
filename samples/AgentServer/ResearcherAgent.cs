using System.Diagnostics;

namespace A2A;

public class ResearcherAgent
{
    private ITaskManager? _taskManager;
    private readonly Dictionary<string, AgentState> _agentStates = [];
    public static readonly ActivitySource ActivitySource = new("A2A.ResearcherAgent", "1.0.0");

    private enum AgentState
    {
        Planning,
        WaitingForFeedbackOnPlan,
        Researching
    }

    public void Attach(ITaskManager taskManager)
    {
        _taskManager = taskManager;
        _taskManager.OnTaskCreated = async (task, cancellationToken) =>
        {
            // Initialize the agent state for the task
            _agentStates[task.Id] = AgentState.Planning;
            // Ignore other content in the task, just assume it is a text message.
            var message = ((TextPart?)task.History?.Last()?.Parts?.FirstOrDefault())?.Text ?? string.Empty;
            await InvokeAsync(task.Id, message, cancellationToken);
        };
        _taskManager.OnTaskUpdated = async (task, cancellationToken) =>
        {
            // Note that the updated callback is helpful to know not to initialize the agent state again.
            var message = ((TextPart?)task.History?.Last()?.Parts?.FirstOrDefault())?.Text ?? string.Empty;
            await InvokeAsync(task.Id, message, cancellationToken);
        };
        _taskManager.OnAgentCardQuery = GetAgentCardAsync;
    }

    // This is the main entry point for the agent. It is called when a task is created or updated.
    // It probably should have a cancellation token to enable the process to be cancelled.
    public async Task InvokeAsync(string taskId, string message, CancellationToken cancellationToken)
    {
        if (_taskManager == null)
        {
            throw new InvalidOperationException("TaskManager is not attached.");
        }

        using var activity = ActivitySource.StartActivity("Invoke", ActivityKind.Server);
        activity?.SetTag("task.id", taskId);
        activity?.SetTag("message", message);
        activity?.SetTag("state", _agentStates[taskId].ToString());

        switch (_agentStates[taskId])
        {
            case AgentState.Planning:
                await DoPlanningAsync(taskId, message, cancellationToken);
                await _taskManager.UpdateStatusAsync(taskId, TaskState.InputRequired, new Message()
                {
                    Parts = [new TextPart() { Text = "When ready say go ahead" }],
                },
                cancellationToken: cancellationToken);
                break;
            case AgentState.WaitingForFeedbackOnPlan:
                if (message == "go ahead")  // Dumb check for now to avoid using an LLM
                {
                    await DoResearchAsync(taskId, message, cancellationToken);
                }
                else
                {
                    // Take the message and redo planning
                    await DoPlanningAsync(taskId, message, cancellationToken);
                    await _taskManager.UpdateStatusAsync(taskId, TaskState.InputRequired, new Message()
                    {
                        Parts = [new TextPart() { Text = "When ready say go ahead" }],
                    },
                    cancellationToken: cancellationToken);
                }
                break;
            case AgentState.Researching:
                await DoResearchAsync(taskId, message, cancellationToken);
                break;
        }
    }

    private async Task DoResearchAsync(string taskId, string message, CancellationToken cancellationToken)
    {
        if (_taskManager == null)
        {
            throw new InvalidOperationException("TaskManager is not attached.");
        }

        using var activity = ActivitySource.StartActivity("DoResearch", ActivityKind.Server);
        activity?.SetTag("task.id", taskId);
        activity?.SetTag("message", message);

        _agentStates[taskId] = AgentState.Researching;
        await _taskManager.UpdateStatusAsync(taskId, TaskState.Working, cancellationToken: cancellationToken);

        await _taskManager.ReturnArtifactAsync(
            taskId,
            new Artifact()
            {
                Parts = [new TextPart() { Text = $"{message} received." }],
            },
            cancellationToken);

        await _taskManager.UpdateStatusAsync(taskId, TaskState.Completed, new Message()
        {
            Parts = [new TextPart() { Text = "Task completed successfully" }],
        },
        cancellationToken: cancellationToken);
    }
    private async Task DoPlanningAsync(string taskId, string message, CancellationToken cancellationToken)
    {
        if (_taskManager == null)
        {
            throw new InvalidOperationException("TaskManager is not attached.");
        }

        using var activity = ActivitySource.StartActivity("DoPlanning", ActivityKind.Server);
        activity?.SetTag("task.id", taskId);
        activity?.SetTag("message", message);

        // Task should be in status Submitted
        // Simulate being in a queue for a while
        await Task.Delay(1000, cancellationToken);

        // Simulate processing the task
        await _taskManager.UpdateStatusAsync(taskId, TaskState.Working, cancellationToken: cancellationToken);

        await _taskManager.ReturnArtifactAsync(
            taskId,
            new Artifact()
            {
                Parts = [new TextPart() { Text = $"{message} received." }],
            },
            cancellationToken);

        await _taskManager.UpdateStatusAsync(taskId, TaskState.InputRequired, new Message()
        {
            Parts = [new TextPart() { Text = "When ready say go ahead" }],
        },
        cancellationToken: cancellationToken);
        _agentStates[taskId] = AgentState.WaitingForFeedbackOnPlan;
    }

    private Task<AgentCard> GetAgentCardAsync(string agentUrl, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<AgentCard>(cancellationToken);
        }

        var capabilities = new AgentCapabilities()
        {
            Streaming = true,
            PushNotifications = false,
        };

        return Task.FromResult(new AgentCard()
        {
            Name = "Researcher Agent",
            Description = "Agent which conducts research.",
            Url = agentUrl,
            Version = "1.0.0",
            DefaultInputModes = ["text"],
            DefaultOutputModes = ["text"],
            Capabilities = capabilities,
            Skills = [],
        });
    }
}