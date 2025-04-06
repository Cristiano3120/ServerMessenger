namespace Server_Messenger
{
    public class Worker(IHostApplicationLifetime applicationLifetime) : BackgroundService
    {
        private readonly IHostApplicationLifetime _applicationLifetime = applicationLifetime;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Server.StartAsync();
            Action shutdownAsync = static async() => await Server.ShutdownAsync();
            _applicationLifetime.ApplicationStopping.Register(shutdownAsync);
        }
    }
}
