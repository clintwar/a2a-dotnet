using System.Text.Json;
using System.Text.Json.Serialization;

namespace A2A;

/// <summary>
/// Represents a task that can be processed by an agent.
/// </summary>
public sealed class AgentTask : A2AResponse
{
    /// <summary>
    /// Unique identifier for the task.
    /// </summary>
    [JsonPropertyName("id")]
    [JsonRequired]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Server-generated id for contextual alignment across interactions.
    /// </summary>
    [JsonPropertyName("contextId")]
    [JsonRequired]
    public string ContextId { get; set; } = string.Empty;

    /// <summary>
    /// Current status of the task.
    /// </summary>
    [JsonPropertyName("status")]
    [JsonRequired]
    public AgentTaskStatus Status { get; set; } = new AgentTaskStatus();

    /// <summary>
    /// Collection of artifacts created by the agent.
    /// </summary>
    [JsonPropertyName("artifacts")]
    public List<Artifact>? Artifacts { get; set; }

    /// <summary>
    /// Collection of messages in the task history.
    /// </summary>
    [JsonPropertyName("history")]
    public List<Message>? History { get; set; } = [];

    /// <summary>
    /// Extension metadata.
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, JsonElement>? Metadata { get; set; }

    /// <summary>
    /// Trims the <see cref="History"/> collection to the specified length, keeping only the most recent messages.
    /// </summary>
    /// <param name="toLength">
    /// The maximum number of messages to retain in the history. If <c>null</c> or greater than the current count, no trimming occurs.
    /// </param>
    public void TrimHistory(int? toLength)
    {
        // Trim history if historyLength is specified
        if (toLength is { } historyLength && this.History?.Count > historyLength)
        {
            this.History = [.. this.History.Skip(Math.Max(0, this.History.Count - historyLength))];
        }
    }
}