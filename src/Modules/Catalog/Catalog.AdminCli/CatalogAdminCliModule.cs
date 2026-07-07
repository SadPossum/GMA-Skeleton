namespace Catalog.AdminCli;

using System.CommandLine;
using Catalog.Admin.Contracts;
using Catalog.Application;
using Catalog.Application.Commands;
using Catalog.Application.Queries;
using Catalog.Contracts;
using Catalog.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Gma.Framework.Administration;
using Gma.Framework.Administration.Cli;
using Gma.Framework.Cqrs;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Pagination;
using Gma.Framework.Results;

public sealed class CatalogAdminCliModule : IAdminCliModule
{
    public string Name => CatalogModuleMetadata.Name;

    public void AddServices(IHostApplicationBuilder builder)
    {
        builder.SelectModuleProfile(CatalogProfiles.Default, "Catalog.AdminCli");
        builder.Services.AddCatalogApplication();
        builder.AddCatalogPersistence();
    }

    public void MapCommands(IAdminCliCommandRegistry commands)
    {
        AdminCliGlobalOptions globalOptions = commands.Services.GetRequiredService<AdminCliGlobalOptions>();
        Command items = new("items", "Manage catalog items.")
        {
            CreateListCommand(commands.Services, globalOptions),
            CreateGetCommand(commands.Services, globalOptions),
            CreateCreateCommand(commands.Services, globalOptions),
            CreateUpdateCommand(commands.Services, globalOptions),
            CreateDiscontinueCommand(commands.Services, globalOptions)
        };
        Command catalog = new(CatalogModuleMetadata.Name, "Catalog administration operations.")
        {
            items
        };

        commands.AddCommand(this.Name, catalog);
    }

    private static Command CreateListCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<int> pageOption = new("--page") { DefaultValueFactory = _ => PageRequest.DefaultPage };
        Option<int> pageSizeOption = new("--page-size") { DefaultValueFactory = _ => PageRequest.DefaultPageSize };
        Command command = new("list", "List catalog items.")
        {
            pageOption,
            pageSizeOption
        };
        command.SetAction((parseResult, cancellationToken) =>
        {
            AdminCliExecutor executor = services.GetRequiredService<AdminCliExecutor>();
            return executor.ExecuteAsync(
                parseResult,
                AdminOperation.Create(CatalogAdminOperationNames.ItemsList, CatalogAdminPermissions.ItemsRead),
                parseResult.GetValue(globalOptions.TenantOption),
                requireTenant: true,
                async (provider, token) =>
                {
                    IRequestDispatcher dispatcher = provider.GetRequiredService<IRequestDispatcher>();
                    Result<CatalogItemListResponse> result = await dispatcher.QueryAsync(
                        new ListCatalogItemsQuery(parseResult.GetValue(pageOption), parseResult.GetValue(pageSizeOption)),
                        token).ConfigureAwait(false);

                    if (result.IsSuccess)
                    {
                        AdminCliOutput.WriteRows(
                            result.Value.Items,
                            parseResult.GetValue(globalOptions.OutputOption) ?? AdminCliOutput.Table,
                            [
                                ("ItemId", item => item.ItemId.ToString()),
                                ("Sku", item => item.Sku),
                                ("Name", item => item.Name),
                                ("Price", item => item.Price.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                                ("Currency", item => item.Currency),
                                ("Status", item => item.Status.ToString())
                            ]);
                    }

                    return result;
                },
                cancellationToken);
        });

        return command;
    }

    private static Command CreateGetCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> itemIdOption = CreateItemIdOption();
        Command command = new("get", "Get a catalog item.")
        {
            itemIdOption
        };
        command.SetAction((parseResult, cancellationToken) =>
        {
            AdminCliExecutor executor = services.GetRequiredService<AdminCliExecutor>();
            return executor.ExecuteAsync(
                parseResult,
                AdminOperation.Create(CatalogAdminOperationNames.ItemsGet, CatalogAdminPermissions.ItemsRead),
                parseResult.GetValue(globalOptions.TenantOption),
                requireTenant: true,
                async (provider, token) =>
                {
                    IRequestDispatcher dispatcher = provider.GetRequiredService<IRequestDispatcher>();
                    Result<CatalogItemDto> result = await dispatcher.QueryAsync(
                        new GetCatalogItemQuery(parseResult.GetRequiredValue(itemIdOption)),
                        token).ConfigureAwait(false);

                    if (result.IsSuccess)
                    {
                        AdminCliOutput.WriteRows(
                            [result.Value],
                            parseResult.GetValue(globalOptions.OutputOption) ?? AdminCliOutput.Table,
                            [
                                ("ItemId", item => item.ItemId.ToString()),
                                ("Sku", item => item.Sku),
                                ("Name", item => item.Name),
                                ("Price", item => item.Price.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                                ("Currency", item => item.Currency),
                                ("Status", item => item.Status.ToString())
                            ]);
                    }

                    return result;
                },
                cancellationToken);
        });

        return command;
    }

    private static Command CreateCreateCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<string> skuOption = new("--sku") { Required = true };
        Option<string> nameOption = new("--name") { Required = true };
        Option<decimal> priceOption = new("--price") { Required = true };
        Option<string> currencyOption = new("--currency") { DefaultValueFactory = _ => "USD" };
        Command command = new("create", "Create a catalog item.")
        {
            skuOption,
            nameOption,
            priceOption,
            currencyOption
        };
        command.SetAction((parseResult, cancellationToken) =>
        {
            AdminCliExecutor executor = services.GetRequiredService<AdminCliExecutor>();
            return executor.ExecuteAsync(
                parseResult,
                AdminOperation.Create(CatalogAdminOperationNames.ItemsCreate, CatalogAdminPermissions.ItemsCreate),
                parseResult.GetValue(globalOptions.TenantOption),
                requireTenant: true,
                async (provider, token) =>
                {
                    IRequestDispatcher dispatcher = provider.GetRequiredService<IRequestDispatcher>();
                    Result<CatalogItemDto> result = await dispatcher.SendAsync(
                        new CreateCatalogItemCommand(
                            parseResult.GetRequiredValue(skuOption),
                            parseResult.GetRequiredValue(nameOption),
                            parseResult.GetRequiredValue(priceOption),
                            parseResult.GetValue(currencyOption) ?? "USD"),
                        token).ConfigureAwait(false);

                    if (result.IsSuccess)
                    {
                        AdminCliOutput.WriteMessage($"Created catalog item '{result.Value.ItemId}'.");
                    }

                    return result;
                },
                cancellationToken);
        });

        return command;
    }

    private static Command CreateUpdateCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> itemIdOption = CreateItemIdOption();
        Option<string> skuOption = new("--sku") { Required = true };
        Option<string> nameOption = new("--name") { Required = true };
        Option<decimal> priceOption = new("--price") { Required = true };
        Option<string> currencyOption = new("--currency") { DefaultValueFactory = _ => "USD" };
        Command command = new("update", "Update a catalog item.")
        {
            itemIdOption,
            skuOption,
            nameOption,
            priceOption,
            currencyOption
        };
        command.SetAction((parseResult, cancellationToken) =>
        {
            AdminCliExecutor executor = services.GetRequiredService<AdminCliExecutor>();
            return executor.ExecuteAsync(
                parseResult,
                AdminOperation.Create(CatalogAdminOperationNames.ItemsUpdate, CatalogAdminPermissions.ItemsUpdate),
                parseResult.GetValue(globalOptions.TenantOption),
                requireTenant: true,
                async (provider, token) =>
                {
                    IRequestDispatcher dispatcher = provider.GetRequiredService<IRequestDispatcher>();
                    Result<CatalogItemDto> result = await dispatcher.SendAsync(
                        new UpdateCatalogItemCommand(
                            parseResult.GetRequiredValue(itemIdOption),
                            parseResult.GetRequiredValue(skuOption),
                            parseResult.GetRequiredValue(nameOption),
                            parseResult.GetRequiredValue(priceOption),
                            parseResult.GetValue(currencyOption) ?? "USD"),
                        token).ConfigureAwait(false);

                    if (result.IsSuccess)
                    {
                        AdminCliOutput.WriteMessage("Catalog item updated.");
                    }

                    return result;
                },
                cancellationToken);
        });

        return command;
    }

    private static Command CreateDiscontinueCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> itemIdOption = CreateItemIdOption();
        Option<bool> yesOption = new("--yes");
        Command command = new("discontinue", "Discontinue a catalog item.")
        {
            itemIdOption,
            yesOption
        };
        command.SetAction((parseResult, cancellationToken) =>
        {
            AdminCliExecutor executor = services.GetRequiredService<AdminCliExecutor>();
            return executor.ExecuteAsync(
                parseResult,
                AdminOperation.Create(CatalogAdminOperationNames.ItemsDiscontinue, CatalogAdminPermissions.ItemsDiscontinue),
                parseResult.GetValue(globalOptions.TenantOption),
                requireTenant: true,
                async (provider, token) =>
                {
                    if (!parseResult.GetValue(yesOption))
                    {
                        return Result.Failure<Unit>(AdminErrors.ConfirmationRequired);
                    }

                    IRequestDispatcher dispatcher = provider.GetRequiredService<IRequestDispatcher>();
                    Result<Unit> result = await dispatcher.SendAsync(
                        new DiscontinueCatalogItemCommand(parseResult.GetRequiredValue(itemIdOption)),
                        token).ConfigureAwait(false);

                    if (result.IsSuccess)
                    {
                        AdminCliOutput.WriteMessage("Catalog item discontinued.");
                    }

                    return result;
                },
                cancellationToken);
        });

        return command;
    }

    private static Option<Guid> CreateItemIdOption() =>
        new("--item-id")
        {
            Required = true
        };
}
