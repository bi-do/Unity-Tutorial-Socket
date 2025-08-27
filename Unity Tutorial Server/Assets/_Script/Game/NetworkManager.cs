using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.UI;

public class NetworkManager : MonoBehaviour
{
    public static NetworkManager Instance { get; private set; }

    private Socket client_socket;
    private byte[] receive_buffer = new byte[4096];
    private List<byte> incomplete_packet_buffer = new List<byte>();
    private readonly object packetBufferLock = new object();

    private readonly Queue<Action> main_thread_actions = new Queue<Action>();

    public byte my_player_index { get; private set; } = 255; 
    public byte current_turn_player_index { get; private set; }
    public byte[] board_state { get; private set; }
    public byte game_over_winner { get; private set; }
    public bool is_game_over { get; private set; }

    public Action OnMatchSuccess;
    public Action OnBoardUpdate;
    public Action OnTurnUpdate;
    public Action<byte> OnGameOver;

    [SerializeField] private Button connectButton;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            board_state = new byte[9];
        }
        else
        {
            Destroy(gameObject);
        }

        if (connectButton != null)
        {
            connectButton.onClick.AddListener(connect);
        }
        
        Application.runInBackground = true;
    }

    void Update()
    {
        lock (main_thread_actions)
        {
            while (main_thread_actions.Count > 0)
            {
                Action action = main_thread_actions.Dequeue();
                action?.Invoke();
            }
        }
    }

    public void connect()
    {
        try
        {
            client_socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPAddress ipAddress = IPAddress.Parse("127.0.0.1");
            IPEndPoint remoteEP = new IPEndPoint(ipAddress, 7979);
            
            Debug.Log("Connecting to server...");
            client_socket.BeginConnect(remoteEP, new AsyncCallback(ConnectCallback), null);
        }
        catch (Exception e)
        {
            Debug.LogError($"Connection failed: {e.Message}");
        }
    }

    private void ConnectCallback(IAsyncResult AR)
    {
        try
        {
            client_socket.EndConnect(AR);

            lock (main_thread_actions)
            {
                main_thread_actions.Enqueue(() => {
                    Debug.Log("Connected successfully!");
                });
            }
            client_socket.BeginReceive(receive_buffer, 0, receive_buffer.Length, SocketFlags.None, new AsyncCallback(ReceiveCallback), null);
        }
        catch (Exception e)
        {
            lock (main_thread_actions)
            {
                main_thread_actions.Enqueue(() => {
                    Debug.LogError($"Connection callback failed: {e.Message}");
                });
            }
        }
    }

    private void SendCallback(IAsyncResult AR)
    {
        try
        {
            int bytesSent = client_socket.EndSend(AR);
            lock (main_thread_actions)
            {
                main_thread_actions.Enqueue(() => {
                    Debug.Log($"Sent {bytesSent} bytes to server.");
                });
            }
        }
        catch (Exception e)
        {
            lock (main_thread_actions)
            {
                main_thread_actions.Enqueue(() => {
                    Debug.LogError($"Send failed: {e.Message}");
                });
            }
        }
    }

    private void ReceiveCallback(IAsyncResult AR)
    {
        try
        {
            int bytesRead = client_socket.EndReceive(AR);
            if (bytesRead > 0)
            {
                lock (packetBufferLock)
                {
                    for (int i = 0; i < bytesRead; i++)
                    {
                        incomplete_packet_buffer.Add(receive_buffer[i]);
                    }
                    ProcessReceivedData();
                }
                client_socket.BeginReceive(receive_buffer, 0, receive_buffer.Length, SocketFlags.None, new AsyncCallback(ReceiveCallback), null);
            }
            else
            {
                lock (main_thread_actions)
                {
                    main_thread_actions.Enqueue(() => {
                        Debug.Log("Server disconnected.");
                        Disconnect();
                    });
                }
            }
        }
        catch (Exception e)
        {
            lock (main_thread_actions)
            {
                main_thread_actions.Enqueue(() => {
                    Debug.LogError($"Receive failed: {e.Message}");
                    Disconnect();
                });
            }
        }
    }

    private void ProcessReceivedData()
    {
        while (true)
        {
            if (incomplete_packet_buffer.Count < Defines.HEADERSIZE) return;
            
            short body_size = BitConverter.ToInt16(incomplete_packet_buffer.ToArray(), 0);
            
            if (incomplete_packet_buffer.Count < Defines.HEADERSIZE + body_size) return;

            byte[] completed_message_body = new byte[body_size];
            incomplete_packet_buffer.CopyTo(Defines.HEADERSIZE, completed_message_body, 0, body_size);
            incomplete_packet_buffer.RemoveRange(0, Defines.HEADERSIZE + body_size);

            lock (main_thread_actions)
            {
                main_thread_actions.Enqueue(() => {
                    short protocol = BitConverter.ToInt16(completed_message_body, 0);
                    switch ((PROTOCOL)protocol)
                    {
                        case PROTOCOL.MATCH_SUCCESS_ACK:
                            handle_match_success(completed_message_body);
                            break;
                        case PROTOCOL.BOARD_UPDATE_ACK:
                            handle_board_update(completed_message_body);
                            break;
                        case PROTOCOL.TURN_UPDATE_ACK:
                            handle_turn_update(completed_message_body);
                            break;
                        case PROTOCOL.GAME_OVER_ACK:
                            handle_game_over(completed_message_body);
                            break;
                    }
                });
            }
        }
    }

    public void Disconnect()
    {
        if (client_socket != null && client_socket.Connected)
        {
            client_socket.Shutdown(SocketShutdown.Both);
            client_socket.Close();
        }
        client_socket = null;
        Debug.Log("Disconnected from server.");
    }

    void OnApplicationQuit()
    {
        Disconnect();
    }
    
    private void send_packet(Packet packet)
    {
        if (client_socket == null || !client_socket.Connected) return;
        packet.RecordSize();
        byte[] data_to_send = new byte[packet.position];
        Array.Copy(packet.buffer, 0, data_to_send, 0, packet.position);
        client_socket.BeginSend(data_to_send, 0, data_to_send.Length, SocketFlags.None, new AsyncCallback(SendCallback), null);
    }
    
    public void send_match_request()
    {
        Packet packet = new Packet(PROTOCOL.MATCH_REQ);
        send_packet(packet);
    }

    public void send_place_stone(byte position)
    {
        Packet packet = new Packet(PROTOCOL.PLACE_STONE_REQ);
        packet.Push(position);
        send_packet(packet);
    }
    
    private void handle_match_success(byte[] data)
    {
        my_player_index = data[2];
        Debug.Log($"Match success! I am player index: {my_player_index}");
        OnMatchSuccess?.Invoke();
    }

    private void handle_board_update(byte[] data)
    {
        Array.Copy(data, 2, board_state, 0, 9);
        Debug.Log("Board state updated.");
        OnBoardUpdate?.Invoke();
    }
    
    private void handle_turn_update(byte[] data)
    {
        current_turn_player_index = data[2];
        Debug.Log($"Turn updated. Current turn is player: {current_turn_player_index}");
        OnTurnUpdate?.Invoke();
    }

    private void handle_game_over(byte[] data)
    {
        is_game_over = true;
        game_over_winner = data[2];
        Debug.Log($"Game over! Winner is player: {game_over_winner}");
        OnGameOver?.Invoke(game_over_winner);
    }
}