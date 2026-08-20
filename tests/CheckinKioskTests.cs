namespace StockSync.Tests;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using StockSync.Data;
using StockSync.Models;
using StockSync.Repositories;
using StockSync.Services;
using Xunit;

public class CheckinKioskTests : IDisposable
{
    private readonly string _dbPath;
    private readonly string _connectionString;
    private readonly AttendeeRepository _attendeeRepo;
    private readonly PrintJobRepository _printJobRepo;
    private readonly WebhookEventRepository _webhookEventRepo;

    public CheckinKioskTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"solstice_test_{Guid.NewGuid():N}.db");
        _connectionString = $"Data Source={_dbPath}";

        var initializer = new DatabaseInitializer(_connectionString, _dbPath);
        initializer.Initialize();

        _attendeeRepo = new AttendeeRepository(_connectionString);
        _printJobRepo = new PrintJobRepository(_connectionString);
        _webhookEventRepo = new WebhookEventRepository(_connectionString);
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch { }
        }
    }

    private class TestBadgePrintPublisher : IBadgePrintPublisher
    {
        public List<BadgePrintMessage> PublishedMessages { get; } = new();

        public Task PublishPrintRequestAsync(BadgePrintMessage message)
        {
            PublishedMessages.Add(message);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Test1_NewScan_SetsStatusToPending_AndCreatesPrintJob()
    {
        // Arrange
        var publisher = new TestBadgePrintPublisher();
        var checkinService = new CheckinService(_attendeeRepo, _printJobRepo, publisher, NullLogger<CheckinService>.Instance);

        // Act - Scan Alice (A001)
        var result = await checkinService.ProcessCheckinAsync("A001");

        // Assert
        Assert.True(result.Success);
        Assert.Equal(AttendeeStatus.Pending, result.Status);
        Assert.NotNull(result.PrintJobId);

        var attendee = await _attendeeRepo.GetByIdAsync("A001");
        Assert.NotNull(attendee);
        Assert.Equal(AttendeeStatus.Pending, attendee.Status);

        var printJobs = await _printJobRepo.GetAllAsync();
        Assert.Single(printJobs);
        Assert.Equal("A001", printJobs[0].AttendeeId);
        Assert.Equal("pending", printJobs[0].Status);
    }

    [Fact]
    public async Task Test2_NewScan_PublishesBadgePrintMessageWithCorrectPayload()
    {
        // Arrange
        var publisher = new TestBadgePrintPublisher();
        var checkinService = new CheckinService(_attendeeRepo, _printJobRepo, publisher, NullLogger<CheckinService>.Instance);

        // Act - Scan Brian (A002)
        var result = await checkinService.ProcessCheckinAsync("A002");

        // Assert
        Assert.Single(publisher.PublishedMessages);
        var msg = publisher.PublishedMessages[0];
        Assert.Equal("A002", msg.AttendeeId);
        Assert.Equal("Brian", msg.AttendeeName);
        Assert.Equal(result.PrintJobId, msg.PrintJobId);
    }

    [Fact]
    public async Task Test3_WebhookCompletion_TransitionsPendingToCheckedIn()
    {
        // Arrange
        var publisher = new TestBadgePrintPublisher();
        var checkinService = new CheckinService(_attendeeRepo, _printJobRepo, publisher, NullLogger<CheckinService>.Instance);
        var webhookService = new WebhookService(_webhookEventRepo, _printJobRepo, _attendeeRepo, NullLogger<WebhookService>.Instance);

        // Step 1: Scan Carol (A003) -> PENDING
        var checkinResult = await checkinService.ProcessCheckinAsync("A003");
        Assert.Equal(AttendeeStatus.Pending, checkinResult.Status);

        // Step 2: Vendor Mock Printer executes webhook callback
        var webhookPayload = new PrintCompletedWebhookPayload
        {
            EventId = "evt-unit-test-3",
            PrintJobId = checkinResult.PrintJobId!,
            AttendeeId = "A003",
            Status = "completed"
        };
        var webhookResult = await webhookService.ProcessPrintCompletedAsync(webhookPayload);

        // Assert
        Assert.True(webhookResult.Success);
        Assert.False(webhookResult.IsDuplicate);

        var attendee = await _attendeeRepo.GetByIdAsync("A003");
        Assert.NotNull(attendee);
        Assert.Equal(AttendeeStatus.CheckedIn, attendee.Status);

        var printJob = await _printJobRepo.GetByIdAsync(checkinResult.PrintJobId!);
        Assert.NotNull(printJob);
        Assert.Equal("completed", printJob.Status);
        Assert.NotNull(printJob.CompletedAt);
    }

    [Fact]
    public async Task Test4_DuplicateScan_OnCheckedInAttendee_DoesNotCreateSecondPrintJob()
    {
        // Arrange
        var publisher = new TestBadgePrintPublisher();
        var checkinService = new CheckinService(_attendeeRepo, _printJobRepo, publisher, NullLogger<CheckinService>.Instance);
        var webhookService = new WebhookService(_webhookEventRepo, _printJobRepo, _attendeeRepo, NullLogger<WebhookService>.Instance);

        // Scan Alice and complete check-in
        var scan1 = await checkinService.ProcessCheckinAsync("A001");
        await webhookService.ProcessPrintCompletedAsync(new PrintCompletedWebhookPayload
        {
            EventId = "evt-unit-test-4",
            PrintJobId = scan1.PrintJobId!,
            AttendeeId = "A001",
            Status = "completed"
        });

        var initialJobsCount = (await _printJobRepo.GetAllAsync()).Count;
        Assert.Equal(1, initialJobsCount);
        Assert.Single(publisher.PublishedMessages);

        // Act - Attempt duplicate scan on already CheckedIn Alice
        var duplicateScan = await checkinService.ProcessCheckinAsync("A001");

        // Assert - Duplicate scan is identified, NO new print job or message
        Assert.True(duplicateScan.Success);
        Assert.Equal(AttendeeStatus.CheckedIn, duplicateScan.Status);
        Assert.Contains("already checked in", duplicateScan.Message, StringComparison.OrdinalIgnoreCase);

        var finalJobsCount = (await _printJobRepo.GetAllAsync()).Count;
        Assert.Equal(1, finalJobsCount);
        Assert.Single(publisher.PublishedMessages);
    }

    [Fact]
    public async Task Test5_DuplicateScan_OnPendingAttendee_DoesNotCreateSecondPrintJob()
    {
        // Arrange
        var publisher = new TestBadgePrintPublisher();
        var checkinService = new CheckinService(_attendeeRepo, _printJobRepo, publisher, NullLogger<CheckinService>.Instance);

        // Scan Brian -> PENDING
        var scan1 = await checkinService.ProcessCheckinAsync("A002");
        Assert.Equal(AttendeeStatus.Pending, scan1.Status);
        Assert.Single(publisher.PublishedMessages);

        // Act - Rescan while still PENDING
        var duplicateScan = await checkinService.ProcessCheckinAsync("A002");

        // Assert - NO second print job or message
        Assert.True(duplicateScan.Success);
        Assert.Equal(AttendeeStatus.Pending, duplicateScan.Status);
        Assert.Contains("already in progress", duplicateScan.Message, StringComparison.OrdinalIgnoreCase);

        var jobs = await _printJobRepo.GetAllAsync();
        Assert.Single(jobs);
        Assert.Single(publisher.PublishedMessages);
    }

    [Fact]
    public async Task Test6_DuplicateWebhook_IsPersistentInSqlite_AndSafelyIgnored()
    {
        // Arrange
        var webhookService = new WebhookService(_webhookEventRepo, _printJobRepo, _attendeeRepo, NullLogger<WebhookService>.Instance);
        var payload = new PrintCompletedWebhookPayload
        {
            EventId = "evt-idempotent-unique-01",
            PrintJobId = "job-unit-01",
            AttendeeId = "A001",
            Status = "completed"
        };

        // Act 1: First delivery
        var result1 = await webhookService.ProcessPrintCompletedAsync(payload);
        Assert.True(result1.Success);
        Assert.False(result1.IsDuplicate);

        // Act 2: Duplicate delivery with same EventId
        var result2 = await webhookService.ProcessPrintCompletedAsync(payload);

        // Assert
        Assert.True(result2.Success);
        Assert.True(result2.IsDuplicate);
        Assert.Contains("already processed", result2.Message, StringComparison.OrdinalIgnoreCase);

        // Verify SQLite idempotency record
        var exists = await _webhookEventRepo.ExistsAsync("evt-idempotent-unique-01");
        Assert.True(exists);
    }

    [Fact]
    public async Task Test7_UnknownAttendee_ReturnsNotFound()
    {
        // Arrange
        var publisher = new TestBadgePrintPublisher();
        var checkinService = new CheckinService(_attendeeRepo, _printJobRepo, publisher, NullLogger<CheckinService>.Instance);

        // Act
        var result = await checkinService.ProcessCheckinAsync("UNKNOWN_999");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("NOT_FOUND", result.Status);
        Assert.Empty(publisher.PublishedMessages);
    }

    [Fact]
    public async Task Test8_OutOfOrderOrLateWebhook_PreservesStateDeterministically()
    {
        // Arrange
        var webhookService = new WebhookService(_webhookEventRepo, _printJobRepo, _attendeeRepo, NullLogger<WebhookService>.Instance);

        // An attendee is NOT_CHECKED_IN, but a late/rogue webhook arrives for A001
        var payload = new PrintCompletedWebhookPayload
        {
            EventId = "evt-out-of-order-99",
            PrintJobId = "job-old",
            AttendeeId = "A001",
            Status = "completed"
        };

        // Act
        var result = await webhookService.ProcessPrintCompletedAsync(payload);

        // Assert - Event recorded in SQLite without corrupting un-scanned attendee state
        Assert.True(result.Success);
        var attendee = await _attendeeRepo.GetByIdAsync("A001");
        Assert.NotNull(attendee);
        Assert.Equal(AttendeeStatus.NotCheckedIn, attendee.Status);
    }

    [Fact]
    public async Task Test9_RealRabbitMQ_BadgePrintPublisher_PublishesToQueueSuccessfully()
    {
        // Integration test against real RabbitMQ broker at localhost:5672
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMQ:Host"] = "localhost",
                ["RabbitMQ:Port"] = "5672",
                ["RabbitMQ:QueueName"] = "badge-print-requests"
            })
            .Build();

        var publisher = new BadgePrintPublisher(config, NullLogger<BadgePrintPublisher>.Instance);

        var testMessage = new BadgePrintMessage
        {
            PrintJobId = $"job-test-rmq-{Guid.NewGuid():N}",
            AttendeeId = "A001",
            AttendeeName = "Alice"
        };

        // Act & Assert - Proves actual AMQP connection and queue declare/publish on localhost:5672
        var exception = await Record.ExceptionAsync(() => publisher.PublishPrintRequestAsync(testMessage));
        Assert.Null(exception);
    }
}
