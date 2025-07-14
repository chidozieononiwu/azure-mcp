using System.CommandLine.Parsing;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
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

namespace AzureMcp.Tests.Areas.Cosmos.UnitTests
{
    [Trait("Area", "Cosmos")]
    public class ItemQueryCommandTests
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ICosmosService _cosmosService;
        private readonly ILogger<ItemQueryCommand> _logger;
        private readonly ItemQueryCommand _command;
        private readonly CommandContext _context;
        private readonly Parser _parser;

        public ItemQueryCommandTests()
        {
            _cosmosService = Substitute.For<ICosmosService>();
            _logger = Substitute.For<ILogger<ItemQueryCommand>>();
            _command = new (_logger);
            _parser = new (_command.GetCommand());
            _serviceProvider = new ServiceCollection()
                .AddSingleton(_cosmosService)
                .BuildServiceProvider();
            _context = new (_serviceProvider);
        }

        [Fact]
        public async Task ExecuteAsync_ReturnsItems_WhenQueryIsProvided()
        {
            // Arrange
            var query = "SELECT * FROM c WHERE c.type = 'test'";
            var expectedItems = new List<JsonNode> { JsonNode.Parse("{\"id\":\"item1\"}")!, JsonNode.Parse("{\"id\":\"item2\"}")! };
            _cosmosService.QueryItems(
                Arg.Is("account123"),
                Arg.Is("database123"),
                Arg.Is("container123"),
                Arg.Is(query),
                Arg.Is("sub123"),
                Arg.Any<AuthMethod>(), null, Arg.Any<RetryPolicyOptions>())
                .Returns(Task.FromResult(expectedItems));

            var args = _parser.Parse([
                "--account-name", "account123",
                "--database-name", "database123",
                "--container-name", "container123",
                "--subscription", "sub123",
                "--query", query
            ]);

            // Act
            var response = await _command.ExecuteAsync(_context, args);

            // Assert
            Assert.NotNull(response);
            Assert.NotNull(response.Results);
            var json = JsonSerializer.Serialize(response.Results);
            var result = JsonSerializer.Deserialize<ItemQueryCommandResult>(json);
            Assert.NotNull(result);
            Assert.Equal(2, result.Items.Count);
        }

        [Fact]
        public async Task ExecuteAsync_ReturnsItems_WhenNoQueryProvided()
        {
            // Arrange
            var expectedItems = new List<JsonNode> { JsonNode.Parse("{\"id\":\"item1\"}")!, JsonNode.Parse("{\"id\":\"item2\"}")! };
            _cosmosService.QueryItems(
                "account123", "database123", "container123", null, "sub123",
                AuthMethod.Credential, null, null)
                .Returns(Task.FromResult(expectedItems));

            var args = _parser.Parse([
                "--account-name", "account123",
        "--database-name", "database123",
        "--container-name", "container123",
        "--subscription", "sub123"
            // No --query option
            ]);

            // Act
            var response = await _command.ExecuteAsync(_context, args);

            // Assert
            Assert.NotNull(response);
            Assert.NotNull(response.Results);
            var json = JsonSerializer.Serialize(response.Results);
            var result = JsonSerializer.Deserialize<ItemQueryCommandResult>(json);
            Assert.NotNull(result);
            Assert.Equal(2, result.Items.Count);
        }

        [Fact]
        public async Task ExecuteAsync_ReturnsNull_WhenNoItemsExist()
        {
            // Arrange
            _cosmosService.QueryItems(
                Arg.Is<string>(s => s == "account123"),
                Arg.Is<string>(d => d == "database123"),
                Arg.Is<string>(c => c == "container123"),
                Arg.Is<string?>(q => q == null),
                Arg.Is<string>(s => s == "sub123"),
                Arg.Is<AuthMethod>(a => a == AuthMethod.Credential),
                Arg.Is<string?>(t => t == null),
                Arg.Any<RetryPolicyOptions?>())
                .Returns(new List<JsonNode>());

            var args = _parser.Parse([
                "--account-name", "account123",
                "--database-name", "database123",
                "--container-name", "container123",
                "--subscription", "sub123"
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

            _cosmosService.QueryItems(
                Arg.Is<string>(s => s == "account123"),
                Arg.Is<string>(d => d == "database123"),
                Arg.Is<string>(c => c == "container123"),
                Arg.Is<string?>(q => q == null),
                Arg.Is<string>(s => s == "sub123"),
                Arg.Is<AuthMethod>(a => a == AuthMethod.Credential),
                Arg.Is<string?>(t => t == null),
                Arg.Is<RetryPolicyOptions?>(r => r == null))
                .ThrowsAsync(new Exception(expectedError));

            var args = _parser.Parse([
                "--account-name", "account123",
                "--database-name", "database123",
                "--container-name", "container123",
                "--subscription", "sub123"
            ]);

            // Act 
            var response = await _command.ExecuteAsync(_context, args);

            // Assert
            Assert.NotNull(response);
            Assert.Equal(500, response.Status);
            Assert.StartsWith(expectedError, response.Message);
        }

        [Theory]
        [InlineData("--account-name", "account123", "--database-name", "database123", "--container-name", "container123")] // Missing subscription
        [InlineData("--subscription", "sub123", "--database-name", "database123", "--container-name", "container123")] // Missing account-name
        [InlineData("--subscription", "sub123", "--account-name", "account123", "--container-name", "container123")] // Missing database-name
        [InlineData("--subscription", "sub123", "--account-name", "account123", "--database-name", "database123")] // Missing container-name
        public async Task ExecuteAsync_Returns400_WhenRequiredParametersAreMissing(params string[] args)
        {
            // Arrange & Act 
            var response = await _command.ExecuteAsync(_context, _parser.Parse(args));

            // Assert
            Assert.Equal(400, response.Status);
            Assert.Contains("required", response.Message.ToLower());
        }

        private class ItemQueryCommandResult
        {
            [JsonPropertyName("items")]
            public List<string> Items { get; set; } = [];
        }
    }
}
