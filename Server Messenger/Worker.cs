namespace Server_Messenger
{
    public class Worker(IHostApplicationLifetime applicationLifetime) : BackgroundService
    {
        #pragma warning disable CS1998
        private readonly IHostApplicationLifetime _applicationLifetime = applicationLifetime;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Server.Start();
            Action shutdownAsync = static async() => await Server.ShutdownAsync();
            _applicationLifetime.ApplicationStopping.Register(shutdownAsync);
        }
    }
}
