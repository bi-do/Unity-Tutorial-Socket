using System.Net.Sockets;

public class NetworkService
{
    private Listener client_listener;
    private SocketAsyncEventArgsPool receive_eventArgsPool;
    private SocketAsyncEventArgsPool send_eventArgsPool;
    private BufferManager bufferManager;

    public delegate void SessionHandler(UserToken token);
    public SessionHandler session_created_callback { get; set; }

    private int maxConnections;
    private int bufferSize;
    private int preAllocCount;

    public NetworkService()
    {
        maxConnections = 10000;
        bufferSize = 1024;
        preAllocCount = 2;
    }

    public void Init()
    {
        bufferManager = new BufferManager(maxConnections * bufferSize * preAllocCount, bufferSize);
        bufferManager.InitBuffer();

        receive_eventArgsPool = new SocketAsyncEventArgsPool(maxConnections);
        send_eventArgsPool = new SocketAsyncEventArgsPool(maxConnections);

        SocketAsyncEventArgs arg;

        for (int i = 0; i < maxConnections; i++)
        {
            UserToken token = new UserToken();

            arg = new SocketAsyncEventArgs();
            arg.Completed += new EventHandler<SocketAsyncEventArgs>(Receive_Completed);
            arg.UserToken = token;
            bufferManager.SetBuffer(arg);
            receive_eventArgsPool.Push(arg);

            arg = new SocketAsyncEventArgs();
            arg.Completed += new EventHandler<SocketAsyncEventArgs>(Send_Completed);
            arg.UserToken = token;
            bufferManager.SetBuffer(arg);
            send_eventArgsPool.Push(arg);
        }
    }

    public void Listen(string host, int port, int backlog)
    {
        client_listener = new Listener();
        client_listener.onNewClient += OnNewClient;
        client_listener.Start(host, port, backlog);
    }

    void OnNewClient(Socket client_socket, object token)
    {
        SocketAsyncEventArgs receive_args = receive_eventArgsPool.Pop();
        SocketAsyncEventArgs send_args = send_eventArgsPool.Pop();

        UserToken userToken = receive_args.UserToken as UserToken;
        userToken.socket = client_socket;
        userToken.receive_eventArgs = receive_args;
        userToken.send_eventArgs = send_args;

        if (session_created_callback != null)
        {
            session_created_callback(userToken);
        }

        Begin_Receive(client_socket, receive_args);
    }

    void Begin_Receive(Socket socket, SocketAsyncEventArgs receive_args)
    {
        bool pending = socket.ReceiveAsync(receive_args);
        if (!pending)
        {
            Process_Receive(receive_args);
        }
    }

    void Receive_Completed(object sender, SocketAsyncEventArgs e)
    {
        if (e.LastOperation == SocketAsyncOperation.Receive)
        {
            Process_Receive(e);
            return;
        }
        throw new ArgumentException("The last operation completed on the socket was not a receive.");
    }

    void Send_Completed(object sender, SocketAsyncEventArgs e)
    {
        (e.UserToken as UserToken).Process_Send(e);
    }

    private void Process_Receive(SocketAsyncEventArgs e)
    {
        UserToken token = e.UserToken as UserToken;
        if (e.BytesTransferred > 0 && e.SocketError == SocketError.Success)
        {
            token.OnReceive(e.Buffer, e.Offset, e.BytesTransferred);
            bool pending = token.socket.ReceiveAsync(e);
            if (!pending)
            {
                Process_Receive(e);
            }
        }
        else
        {
            Close_ClientSocket(token);
        }
    }

    void Close_ClientSocket(UserToken token)
    {
        if (token.socket == null)
            return;

        Console.WriteLine($"Client disconnected: {token.socket.RemoteEndPoint}");

        token.Close();

        if (token.receive_eventArgs != null)
        {
            receive_eventArgsPool.Push(token.receive_eventArgs);
        }

        if (token.send_eventArgs != null)
        {
            send_eventArgsPool.Push(token.send_eventArgs);
        }
    }
}
