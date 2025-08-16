// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureMcp.Core.Commands;
using AzureMcp.Core.Services.Telemetry;
using AzureMcp.Cosmos.Options;
using AzureMcp.Cosmos.Services;
using Microsoft.Extensions.Logging;

namespace AzureMcp.Cosmos.Commands;

public sealed class ContainerListCommand(ILogger<ContainerListCommand> logger) : BaseDatabaseCommand<ContainerListOptions>()
{
    private const string CommandTitle = "List Cosmos DB Containers";
    private readonly ILogger<ContainerListCommand> _logger = logger;

    public override string Name => "list";

    public override string Description =>
        """
        List all containers in a Cosmos DB database. This command retrieves and displays all containers within
        the specified database and Cosmos DB account. Results include container names and are returned as a
        JSON array. You must specify both an account name and a database name.
        """;

    public override string Title => CommandTitle;

    public override ToolMetadata Metadata => new() { Destructive = false, ReadOnly = true };

    public override async Task<CommandResponse> ExecuteAsync(CommandContext context, ParseResult parseResult)
    {
        var options = BindOptions(parseResult);

        try
        {
            if (!Validate(parseResult.CommandResult, context.Response).IsValid)
            {
                return context.Response;
            }

            context.Activity?.WithSubscriptionTag(options);

            var cosmosService = context.GetService<ICosmosService>();
            var containers = await cosmosService.ListContainers(
                options.Account!,
                options.Database!,
                options.Subscription!,
                options.AuthMethod ?? AuthMethod.Credential,
                options.Tenant,
                options.RetryPolicy);

            context.Response.Results = containers?.Count > 0 ?
                ResponseResult.Create(
                    new ContainerListCommandResult(containers),
                    CosmosJsonContext.Default.ContainerListCommandResult) :
                null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An exception occurred listing containers for Cosmos DB database.");
            HandleException(context, ex);
        }

        return context.Response;
    }

    internal record ContainerListCommandResult(IReadOnlyList<string> Containers);
}
