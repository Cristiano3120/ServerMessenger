namespace Server_Messenger
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
            builder.Services.AddHostedService<Worker>();

            IHost host = builder.Build();
            host.Run();
        }
    }
}