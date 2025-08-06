﻿using A2A.AspNetCore.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace A2A.UnitTests.Server;

public class DistributedCacheTaskStoreTests
{
    [Fact]
    public async Task SetTaskAsync_And_GetTaskAsync_ShouldStoreAndRetrieveTask()
    {
        // Arrange
        var sut = BuildDistributedCacheTaskStore();
        var task = new AgentTask { Id = "task1", Status = new AgentTaskStatus { State = TaskState.Submitted } };

        // Act
        await sut.SetTaskAsync(task);
        var result = await sut.GetTaskAsync("task1");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("task1", result!.Id);
        Assert.Equal(TaskState.Submitted, result.Status.State);
    }

    [Fact]
    public async Task GetTaskAsync_ShouldReturnNull_WhenTaskDoesNotExist()
    {
        // Arrange
        var sut = BuildDistributedCacheTaskStore();

        // Act
        var result = await sut.GetTaskAsync("nonexistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetTaskAsync_ShouldThrowArgumentException_WhenTaskIdIsNullOrEmpty()
    {
        // Arrange
        var sut = BuildDistributedCacheTaskStore();

        // Act
        var task = sut.GetTaskAsync(string.Empty);

        // Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => task);
    }

    [Fact]
    public async Task UpdateStatusAsync_ShouldUpdateTaskStatus()
    {
        // Arrange
        var sut = BuildDistributedCacheTaskStore();
        var task = new AgentTask { Id = "task2", Status = new AgentTaskStatus { State = TaskState.Submitted } };
        await sut.SetTaskAsync(task);
        var message = new Message { MessageId = "msg1" };

        // Act
        var status = await sut.UpdateStatusAsync("task2", TaskState.Working, message);
        var updatedTask = await sut.GetTaskAsync("task2");

        // Assert
        Assert.Equal(TaskState.Working, status.State);
        Assert.Equal(TaskState.Working, updatedTask!.Status.State);
        Assert.Equal("msg1", status.Message!.MessageId);
    }

    [Fact]
    public async Task UpdateStatusAsync_ShouldThrow_WhenTaskDoesNotExist()
    {
        // Arrange
        var sut = BuildDistributedCacheTaskStore();

        // Act & Assert
        var x = await Assert.ThrowsAsync<A2AException>(() => sut.UpdateStatusAsync("notfound", TaskState.Completed));
        Assert.Equal(A2AErrorCode.TaskNotFound, x.ErrorCode);
    }

    [Fact]
    public async Task GetPushNotificationAsync_ShouldReturnNull_WhenTaskDoesNotExist()
    {
        // Arrange
        var sut = BuildDistributedCacheTaskStore();

        // Act
        var result = await sut.GetPushNotificationAsync("missing", "config-missing");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetPushNotificationAsync_ShouldReturnNull_WhenConfigDoesNotExist()
    {
        // Arrange
        var sut = BuildDistributedCacheTaskStore();

        await sut.SetPushNotificationConfigAsync(new TaskPushNotificationConfig { TaskId = "task-id", PushNotificationConfig = new() { Id = "config-id" } });

        // Act
        var result = await sut.GetPushNotificationAsync("task-id", "config-missing");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetPushNotificationAsync_ShouldReturnCorrectConfig_WhenMultipleConfigsExistForSameTask()
    {
        // Arrange
        var sut = BuildDistributedCacheTaskStore();
        var taskId = "task-with-multiple-configs";

        var config1 = new TaskPushNotificationConfig
        {
            TaskId = taskId,
            PushNotificationConfig = new PushNotificationConfig
            {
                Url = "http://config1",
                Id = "config-id-1",
                Token = "token1"
            }
        };

        var config2 = new TaskPushNotificationConfig
        {
            TaskId = taskId,
            PushNotificationConfig = new PushNotificationConfig
            {
                Url = "http://config2",
                Id = "config-id-2",
                Token = "token2"
            }
        };

        var config3 = new TaskPushNotificationConfig
        {
            TaskId = taskId,
            PushNotificationConfig = new PushNotificationConfig
            {
                Url = "http://config3",
                Id = "config-id-3",
                Token = "token3"
            }
        };

        // Act - Store multiple configs for the same task
        await sut.SetPushNotificationConfigAsync(config1);
        await sut.SetPushNotificationConfigAsync(config2);
        await sut.SetPushNotificationConfigAsync(config3);

        // Get specific configs by both taskId and notificationConfigId
        var result1 = await sut.GetPushNotificationAsync(taskId, "config-id-1");
        var result2 = await sut.GetPushNotificationAsync(taskId, "config-id-2");
        var result3 = await sut.GetPushNotificationAsync(taskId, "config-id-3");
        var resultNotFound = await sut.GetPushNotificationAsync(taskId, "non-existent-config");

        // Assert - Verify each call returns the correct specific config
        Assert.NotNull(result1);
        Assert.Equal(taskId, result1!.TaskId);
        Assert.Equal("config-id-1", result1.PushNotificationConfig.Id);
        Assert.Equal("http://config1", result1.PushNotificationConfig.Url);
        Assert.Equal("token1", result1.PushNotificationConfig.Token);

        Assert.NotNull(result2);
        Assert.Equal(taskId, result2!.TaskId);
        Assert.Equal("config-id-2", result2.PushNotificationConfig.Id);
        Assert.Equal("http://config2", result2.PushNotificationConfig.Url);
        Assert.Equal("token2", result2.PushNotificationConfig.Token);

        Assert.NotNull(result3);
        Assert.Equal(taskId, result3!.TaskId);
        Assert.Equal("config-id-3", result3.PushNotificationConfig.Id);
        Assert.Equal("http://config3", result3.PushNotificationConfig.Url);
        Assert.Equal("token3", result3.PushNotificationConfig.Token);

        Assert.Null(resultNotFound);
    }

    [Fact]
    public async Task GetPushNotificationsAsync_ShouldReturnEmptyList_WhenNoConfigsExistForTask()
    {
        // Arrange
        var sut = BuildDistributedCacheTaskStore();

        // Act
        var result = await sut.GetPushNotificationsAsync("task-without-configs");

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetTaskAsync_ShouldReturnCanceledTask_WhenCancellationTokenIsCanceled()
    {
        // Arrange
        var sut = BuildDistributedCacheTaskStore();

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var task = sut.GetTaskAsync("test-id", cts.Token);

        // Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => task);
    }

    [Fact]
    public async Task GetPushNotificationAsync_ShouldReturnCanceledTask_WhenCancellationTokenIsCanceled()
    {
        // Arrange
        var sut = BuildDistributedCacheTaskStore();

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var task = sut.GetPushNotificationAsync("test-id", "config-id", cts.Token);

        // Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => task);
    }

    [Fact]
    public async Task UpdateStatusAsync_ShouldReturnCanceledTask_WhenCancellationTokenIsCanceled()
    {
        // Arrange
        var sut = BuildDistributedCacheTaskStore();

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var task = sut.UpdateStatusAsync("test-id", TaskState.Working, cancellationToken: cts.Token);

        // Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => task);
    }

    [Fact]
    public async Task SetTaskAsync_ShouldReturnCanceledTask_WhenCancellationTokenIsCanceled()
    {
        // Arrange
        var sut = BuildDistributedCacheTaskStore();
        var agentTask = new AgentTask { Id = "test-id", Status = new AgentTaskStatus { State = TaskState.Submitted } };

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var task = sut.SetTaskAsync(agentTask, cts.Token);

        // Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => task);
    }

    [Fact]
    public async Task SetPushNotificationConfigAsync_ShouldReturnCanceledTask_WhenCancellationTokenIsCanceled()
    {
        // Arrange
        var sut = BuildDistributedCacheTaskStore();
        var config = new TaskPushNotificationConfig();

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var task = sut.SetPushNotificationConfigAsync(config, cts.Token);

        // Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => task);
    }

    [Fact]
    public async Task SetPushNotificationConfigAsync_ShouldThrowArgumentNullException_WhenConfigIsNull()
    {
        // Arrange
        var sut = BuildDistributedCacheTaskStore();

        // Act
        var task = sut.SetPushNotificationConfigAsync(null!);

        // Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => task);
    }

    [Fact]
    public async Task SetPushNotificationConfigAsync_ShouldThrowArgumentException_WhenTaskIdIsNullOrEmpty()
    {
        // Arrange
        var sut = BuildDistributedCacheTaskStore();
        var config = new TaskPushNotificationConfig
        {
            TaskId = string.Empty,
            PushNotificationConfig = new PushNotificationConfig { Id = "config-id" }
        };

        // Act
        var task = sut.SetPushNotificationConfigAsync(config);

        // Assert
        await Assert.ThrowsAsync<ArgumentException>(() => task);
    }

    [Fact]
    public async Task GetPushNotificationsAsync_ShouldReturnCanceledTask_WhenCancellationTokenIsCanceled()
    {
        // Arrange
        var sut = BuildDistributedCacheTaskStore();

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var task = sut.GetPushNotificationsAsync("test-id", cts.Token);

        // Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => task);
    }

    static DistributedCacheTaskStore BuildDistributedCacheTaskStore()
    {
        var memoryCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        return new DistributedCacheTaskStore(memoryCache);
    }
}