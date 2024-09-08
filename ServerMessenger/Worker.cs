namespace ServerMessenger
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Starts the Server
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Server.StartServer();
        }
    }
}
