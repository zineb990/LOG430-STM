﻿using Application.Commands.Seedwork;
using Application.EventHandlers.AntiCorruption;
using Application.Queries.Seedwork;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Integration.Config;

public class IntegrationTest
{
    private readonly IntegrationWebApplicationFactory _factory;
    protected readonly ICommandDispatcher CommandDispatcher;
    protected readonly IConsumer Consumer;
    protected readonly ITestOutputHelper OutputHelper;
    protected readonly IQueryDispatcher QueryDispatcher;

    protected readonly IServiceScope Scope;

    protected IntegrationTest(ITestOutputHelper outputHelper)
    {
        OutputHelper = outputHelper;

        _factory = new IntegrationWebApplicationFactory(outputHelper);

        _factory.InitializeAsync().Wait();

        Scope = _factory.Services.CreateScope();

        QueryDispatcher = Scope.ServiceProvider.GetRequiredService<IQueryDispatcher>();
        CommandDispatcher = Scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();
        Consumer = Scope.ServiceProvider.GetRequiredService<IConsumer>();
    }
}