using System.Net.Sockets;

public class UserToken
{
    public Socket socket { get; set; }
    public SocketAsyncEventArgs receive_eventArgs { get; set; }
    public SocketAsyncEventArgs send_eventArgs { get; set; }

    private MessageResolver messageResolver;
    private Queue<Packet> sending_queue;
    private object sending_queue_obj;

    public GameRoom game_room { get; private set; }

    public delegate void OnPacketReceiveHandler(UserToken token, Packet packet);
    public OnPacketReceiveHandler OnPacketReceive { get; set; }

    public delegate void OnDisconnectHandler(UserToken token);
    public OnDisconnectHandler OnDisconnect { get; set; }

    public UserToken()
    {
        socket = null;
        messageResolver = new MessageResolver();
        sending_queue = new Queue<Packet>();
        sending_queue_obj = new object();
    }

    public void SetGameRoom(GameRoom room)
    {
        this.game_room = room;
    }

    public void SetOnPacketReceive(OnPacketReceiveHandler handler)
    {
        OnPacketReceive = handler;
    }

    public void SetOnDisconnect(OnDisconnectHandler handler)
    {
        OnDisconnect = handler;
    }

    public void OnReceive(byte[] buffer, int offset, int bytesTransferred)
    {
        messageResolver.OnReceive(buffer, offset, bytesTransferred, OnMessage);
    }

    void OnMessage(byte[] buffer)
    {
        Packet packet = new Packet(buffer);
        if (OnPacketReceive != null)
        {
            OnPacketReceive(this, packet);
        }
    }

    public void Send(Packet msg)
    {
        lock (sending_queue_obj)
        {
            if (sending_queue.Count <= 0)
            {
                sending_queue.Enqueue(msg);
                Start_Send();
                return;
            }
            sending_queue.Enqueue(msg);
        }
    }

    void Start_Send()
    {
        lock (sending_queue_obj)
        {
            Packet msg = sending_queue.Peek();
            msg.RecordSize();

            send_eventArgs.SetBuffer(send_eventArgs.Offset, msg.position);
            Array.Copy(msg.buffer, 0, send_eventArgs.Buffer, send_eventArgs.Offset, msg.position);

            bool pending = socket.SendAsync(send_eventArgs);
            if (!pending)
            {
                Process_Send(send_eventArgs);
            }
        }
    }

    public void Process_Send(SocketAsyncEventArgs e)
    {
        if (e.SocketError != SocketError.Success)
        {
            Console.WriteLine($"Send failed with error: {e.SocketError}");
            return;
        }

        lock (sending_queue_obj)
        {
            sending_queue.Dequeue();
            if (sending_queue.Count > 0)
            {
                Start_Send();
            }
        }
    }

    public void Close()
    {
        if (socket != null)
        {
            try
            {
                socket.Shutdown(SocketShutdown.Both);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"오류 발생: {ex.Message}");
            }

            socket.Close();
            socket = null;
            if (OnDisconnect != null)
            {
                OnDisconnect(this);
            }
        }
    }
}