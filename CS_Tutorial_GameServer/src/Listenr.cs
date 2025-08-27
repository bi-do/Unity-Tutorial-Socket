using System.Net;
using System.Net.Sockets;

public class Listener
{
    private SocketAsyncEventArgs accept_args;
    private Socket listen_socket;
    private AutoResetEvent flowControlEvent;

    public delegate void NewClientHandler(Socket client_socket, object token);
    public NewClientHandler onNewClient;

    public Listener()
    {
        onNewClient = null;
    }

    public void Start(string host, int port, int backlog)
    {
        listen_socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        IPAddress address = (host == "0.0.0.0") ? IPAddress.Any : IPAddress.Parse(host);
        IPEndPoint endPoint = new IPEndPoint(address, port);

        try
        {
            listen_socket.Bind(endPoint);
            listen_socket.Listen(backlog);

            accept_args = new SocketAsyncEventArgs();
            accept_args.Completed += new EventHandler<SocketAsyncEventArgs>(On_Accept_Completed);

            Thread listen_thread = new Thread(DoListen);
            listen_thread.Start();
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }

    void DoListen()
    {
        flowControlEvent = new AutoResetEvent(false);
        while (true)
        {
            accept_args.AcceptSocket = null;
            bool pending = true;
            try
            {
                pending = listen_socket.AcceptAsync(accept_args);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                continue;
            }

            if (!pending)
            {
                On_Accept_Completed(null, accept_args);
            }

            flowControlEvent.WaitOne();
        }
    }

    void On_Accept_Completed(object sender, SocketAsyncEventArgs e)
    {
        if (e.SocketError == SocketError.Success)
        {
            Socket client_socket = e.AcceptSocket;
            flowControlEvent.Set();
            if (onNewClient != null)
            {
                onNewClient(client_socket, e.UserToken);
            }
            return;
        }
        else
        {
            Console.WriteLine("Failed to accept client.");
        }

        flowControlEvent.Set();
    }
}