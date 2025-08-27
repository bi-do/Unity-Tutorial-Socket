class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Starting Tic-Tac-Toe Server");

        NetworkService networkService = new NetworkService();
        GameServer gameServer = new GameServer();

        networkService.session_created_callback += gameServer.OnClientConnected;

        networkService.Init();
        networkService.Listen("0.0.0.0", 7979, 100);

        Console.WriteLine("Server is running. Press Enter to exit.");

        while (true)
        {
            Console.ReadLine();
        }
    }
}