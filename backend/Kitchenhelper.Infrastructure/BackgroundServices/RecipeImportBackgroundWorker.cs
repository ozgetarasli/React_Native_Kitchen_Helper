using Kitchenhelper.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kitchenhelper.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that periodically processes queued recipe imports.
/// Implements IHostedService to run in the background of the ASP.NET Core app.
/// </summary>
public class RecipeImportBackgroundWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RecipeImportBackgroundWorker> _logger;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(10);
    private readonly int _maxConcurrentProcessing = 3;

    public RecipeImportBackgroundWorker(
        IServiceProvider serviceProvider,
        ILogger<RecipeImportBackgroundWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RecipeImportBackgroundWorker starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessQueuedImportsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in background worker loop");
            }

            // Wait before next poll
            await Task.Delay(_pollingInterval, stoppingToken);
        }

        _logger.LogInformation("RecipeImportBackgroundWorker stopping");
    }

    private async Task ProcessQueuedImportsAsync(CancellationToken cancellationToken)
    {
        // Create a scope for each processing cycle to get fresh DbContext
        using var scope = _serviceProvider.CreateScope();
        var importService = scope.ServiceProvider.GetRequiredService<IRecipeImportService>();
        var processingService = scope.ServiceProvider.GetRequiredService<IRecipeImportProcessingService>();

        // Fetch queued imports
        var queuedImports = await importService.GetQueuedImportsAsync(_maxConcurrentProcessing);

        if (queuedImports.Count == 0)
            return; // Nothing to process

        _logger.LogInformation("Found {Count} queued imports to process", queuedImports.Count);

        // Process imports concurrently (with limit)
        var tasks = queuedImports.Select(async import =>
        {
            // Use a separate scope for each import to avoid DbContext conflicts
            using var importScope = _serviceProvider.CreateScope();
            var scopedImportService = importScope.ServiceProvider.GetRequiredService<IRecipeImportService>();
            var scopedProcessingService = importScope.ServiceProvider.GetRequiredService<IRecipeImportProcessingService>();

            try
            {
                // Atomically acquire the import for processing (prevents duplicate processing)
                var acquired = await scopedImportService.TryAcquireForProcessingAsync(import.Id);
                
                if (!acquired)
                {
                    _logger.LogInformation("Import {ImportId} was already acquired by another worker", import.Id);
                    return;
                }

                // Process the import through the complete pipeline
                await scopedProcessingService.ProcessImportAsync(import.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process import {ImportId}", import.Id);
            }
        });

        await Task.WhenAll(tasks);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("RecipeImportBackgroundWorker is stopping gracefully");
        await base.StopAsync(cancellationToken);
    }
}
