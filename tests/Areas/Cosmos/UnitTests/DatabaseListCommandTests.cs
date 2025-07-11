using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text.Json;
using System.Text.Json.Serialization;
using AzureMcp.Areas.AppConfig.Commands.KeyValue;
using AzureMcp.Areas.Cosmos.Commands;
using AzureMcp.Areas.Cosmos.Services;
using AzureMcp.Models;
using AzureMcp.Models.Command;
using AzureMcp.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace AzureMcp.Tests.Areas.Cosmos.UnitTests;

[Trait("Area", "Cosmos")]
public class DatabaseListCommandTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ICosmosService _cosmosService;
    private readonly ILogger<DatabaseListCommand> _logger;
    private readonly DatabaseListCommand _command;
    private readonly CommandContext _context;
    private readonly Parser _parser;
    private readonly string _knownAccountName = "account123";
    private readonly string _knownSubscriptionId = "sub123";

    public DatabaseListCommandTests()
    {
        _cosmosService = Substitute.For<ICosmosService>();
        _logger = Substitute.For<ILogger<DatabaseListCommand>>();
        _command = new (_logger);
        _parser = new (_command.GetCommand());
        _serviceProvider = new ServiceCollection()
            .AddSingleton(_cosmosService)
            .BuildServiceProvider();
        _context = new (_serviceProvider);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsDatabases_WhenDatabasesExist()
    {
        // Arrange
        var expectedDatabases = new List<string> { "database1", "database2" };
        _cosmosService.ListDatabases(
            Arg.Is(_knownAccountName),
            Arg.Is(_knownSubscriptionId),
            Arg.Any<AuthMethod>(),
            Arg.Any<string>(),
            Arg.Any<RetryPolicyOptions>())
            .Returns(expectedDatabases);

        var args = _parser.Parse([
            "--account-name", _knownAccountName,
            "--subscription", _knownSubscriptionId
        ]);

        // Act
        var response = await _command.ExecuteAsync(_context, args);

        // Assert
        Assert.NotNull(response);
        Assert.NotNull(response.Results);
        var json = JsonSerializer.Serialize(response.Results);
        var result = JsonSerializer.Deserialize<DatabaseListResult>(json);
        Assert.NotNull(result);
        Assert.Equal(expectedDatabases.Count, result.Databases.Count);
        Assert.Equal(expectedDatabases, result.Databases);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsNull_WhenNoDataBaseExists()
    {
        // Arrange
        _cosmosService.ListDatabases(
            Arg.Is(_knownAccountName),
            Arg.Is(_knownSubscriptionId),
            Arg.Any<AuthMethod>(),
            Arg.Any<string>(),
            Arg.Any<RetryPolicyOptions>())
            .Returns(new List<string>());

        var args = _parser.Parse([
            "--account-name", _knownAccountName,
            "--subscription", _knownSubscriptionId
        ]);

        // Act
        var response = await _command.ExecuteAsync(_context, args);

        // Assert
        Assert.NotNull(response);
        Assert.Null(response.Results);
    }

    [Fact]
    public async Task ExecuteAsync_Returns500_WhenServiceThrowsException()
    {
        // Arrange
        var expectedError = "Test error";

        _cosmosService.ListDatabases(
            Arg.Is(_knownAccountName),
            Arg.Is(_knownSubscriptionId),
            Arg.Any<AuthMethod>(),
            Arg.Any<string>(),
            Arg.Any<RetryPolicyOptions>())
            .ThrowsAsync(new Exception(expectedError));

        var args = _parser.Parse([
            "--account-name", _knownAccountName,
            "--subscription", _knownSubscriptionId
        ]);

        // Act 
        var response = await _command.ExecuteAsync(_context, args);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(500, response.Status);
        Assert.StartsWith(expectedError, response.Message);
    }


    [Theory]
    [InlineData("--account-name", "account123")]
    [InlineData("--subscription", "sub123")]
    public async Task ExecuteAsync_Returns400_WhenRequiredParametersAreMissing(params string[] args)
    {
        // Arrange & Act 
        var response = await _command.ExecuteAsync(_context, _parser.Parse(args));

        // Assert
        Assert.Equal(400, response.Status);
        Assert.Contains("required", response.Message.ToLower());
    }

    private class DatabaseListResult
    {
        [JsonPropertyName("databases")]
        public List<string> Databases { get; set; } = [];
    }
}
