using FocusLens.Application.Common.Interfaces;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.StudySessions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FocusLens.Infrastructure.StudySessions;

public sealed class StudySessionBehaviorAnalysisWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<StudySessionBehaviorAnalysisWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ProcessingLease = TimeSpan.FromMinutes(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                bool processed = await ProcessOneAsync(stoppingToken);

                if (!processed)
                {
                    await Task.Delay(PollInterval, stoppingToken);
                }
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Unexpected error while processing study-session behavior analysis jobs.");

                await Task.Delay(PollInterval, stoppingToken);
            }
        }
    }

    private async Task<bool> ProcessOneAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();

        IBaseRepository<StudySessionBehaviorAnalysisJob> jobRepository =
            scope.ServiceProvider
                .GetRequiredService<IBaseRepository<StudySessionBehaviorAnalysisJob>>();

        IUnitOfWork unitOfWork =
            scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        IStudySessionFinalBehaviorAnalysisService analyzer =
            scope.ServiceProvider.GetRequiredService<IStudySessionFinalBehaviorAnalysisService>();

        DateTimeOffset now = timeProvider.GetUtcNow();

        StudySessionBehaviorAnalysisJob? job =
            await jobRepository.FirstOrDefaultAsync(
                item =>
                    (item.Status == StudySessionBehaviorAnalysisJobStatus.Pending &&
                     (item.NextAttemptAtUtc == null ||
                      item.NextAttemptAtUtc <= now))
                    ||
                    (item.Status == StudySessionBehaviorAnalysisJobStatus.Processing &&
                     item.ProcessingStartedAtUtc != null &&
                     item.ProcessingStartedAtUtc <= now - ProcessingLease));

        if (job is null)
        {
            return false;
        }

        Result<Success> processingResult = job.MarkProcessing(now);

        if (processingResult.IsError)
        {
            return true;
        }

        await unitOfWork.SaveChangesAsync();

        try
        {
            Result<Success> analysisResult =
                await analyzer.AnalyzeFinalAsync(
                    job.StudySessionId,
                    cancellationToken);

            if (analysisResult.IsError)
            {
                DateTimeOffset retryAt =
                    now.Add(ComputeRetryDelay(job.AttemptCount));

                job.ScheduleRetry(
                    retryAt,
                    analysisResult.TopError.Description);

                await unitOfWork.SaveChangesAsync();

                logger.LogWarning(
                    "Final behavior analysis for session {SessionId} failed. Retry at {RetryAtUtc}. Error: {Error}",
                    job.StudySessionId,
                    retryAt,
                    analysisResult.TopError.Description);

                return true;
            }

            job.MarkCompleted(timeProvider.GetUtcNow());
            await unitOfWork.SaveChangesAsync();

            logger.LogInformation(
                "Final behavior analysis completed for session {SessionId}.",
                job.StudySessionId);

            return true;
        }
        catch (Exception exception)
        {
            DateTimeOffset retryAt =
                now.Add(ComputeRetryDelay(job.AttemptCount));

            job.ScheduleRetry(retryAt, exception.Message);
            await unitOfWork.SaveChangesAsync();

            logger.LogError(
                exception,
                "Final behavior analysis for session {SessionId} failed unexpectedly. Retry at {RetryAtUtc}.",
                job.StudySessionId,
                retryAt);

            return true;
        }
    }

    private static TimeSpan ComputeRetryDelay(int attempt)
    {
        int exponent = Math.Min(Math.Max(attempt - 1, 0), 5);
        return TimeSpan.FromSeconds(Math.Pow(2, exponent) * 5);
    }
}
