using System.Threading.Channels;

namespace Ex_SurveyApp_api.Services;

public record AccountMail(string Recipient, string Name, string Subject, string Body);
public interface IAccountMailQueue { bool TryEnqueue(AccountMail message); }

// Keep SMTP latency out of the anonymous response, so generic reset responses do
// not disclose registration through delivery time. Token bodies are never logged.
public sealed class AccountMailQueue(IServiceScopeFactory scopes, ILogger<AccountMailQueue> logger) : BackgroundService, IAccountMailQueue
{
    private readonly Channel<AccountMail> queue = Channel.CreateBounded<AccountMail>(new BoundedChannelOptions(100) {SingleReader = true, FullMode = BoundedChannelFullMode.Wait});
    public bool TryEnqueue(AccountMail message) => queue.Writer.TryWrite(message);
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var message in queue.Reader.ReadAllAsync(stoppingToken))
            {
                for (var attempt = 1; attempt <= 3; attempt++)
                {
                    try
                    {
                        using var scope = scopes.CreateScope();
                        await scope.ServiceProvider.GetRequiredService<IEmailService>().SendAccountMessageAsync(message.Recipient, message.Name, message.Subject, message.Body, stoppingToken);
                        break;
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {return;}
                    catch (Exception) when (attempt < 3)
                    {
                        logger.LogWarning("Queued account email delivery attempt {Attempt} failed; retrying. Message content is intentionally omitted.", attempt);
                        await Task.Delay(TimeSpan.FromSeconds(attempt * 2), stoppingToken);
                    }
                    catch (Exception)
                    {
                        logger.LogWarning("Queued account email delivery failed after retries. Message content is intentionally omitted.");
                    }
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
