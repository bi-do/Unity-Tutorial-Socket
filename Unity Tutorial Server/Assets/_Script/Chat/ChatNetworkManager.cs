using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChatNetworkManager : MonoBehaviour
{
    public static ChatNetworkManager Instance { get; private set; }

    private Socket client_socket;
    private byte[] receive_buffer = new byte[1024];

    private List<byte> incomplete_packet_buffer = new List<byte>();

    [SerializeField] private TMP_InputField input_field;
    [SerializeField] private Button connect_btn;
    [SerializeField] private Button send_btn;

    void Awake()
    {
        Instance = this;
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        connect_btn.onClick.AddListener(Connect);
        send_btn.onClick.AddListener(Send);
    }

    public void Connect()
    {
        try
        {
            client_socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPAddress ip_addr = IPAddress.Parse("127.0.0.1");
            IPEndPoint remote_ep = new IPEndPoint(ip_addr, 7979);

            Debug.Log("Connecting to server...");
            client_socket.BeginConnect(remote_ep, new System.AsyncCallback(ConnectCallback), null);

        }
        catch (Exception e)
        {
            Debug.LogError(e.Message);
        }
    }

    private void ConnectCallback(IAsyncResult ar)
    {
        try
        {
            client_socket.EndConnect(ar);
            Debug.Log("Connected Successfully");

            client_socket.BeginReceive(receive_buffer, 0, receive_buffer.Length, SocketFlags.None, new AsyncCallback(ReceiveCallback), null);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }

    private void ReceiveCallback(IAsyncResult AR)
    {
        try
        {
            int bytesRead = client_socket.EndReceive(AR);

            if (bytesRead > 0)
            {
                for (int i = 0; i < bytesRead; i++)
                {
                    incomplete_packet_buffer.Add(receive_buffer[i]);
                }

                ProcessReceivedData();

                client_socket.BeginReceive(receive_buffer, 0, receive_buffer.Length, SocketFlags.None, new AsyncCallback(ReceiveCallback), null);
            }
            else
            {
                Debug.Log("Server disConnected.");
                DisConnect();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Receive failed: {e.Message}");
            DisConnect();
        }
    }

     private void ProcessReceivedData()
    {
        while (true)
        {
            if (incomplete_packet_buffer.Count < Defines.HEADERSIZE)
            {
                return;
            }

            short bodySize = BitConverter.ToInt16(incomplete_packet_buffer.ToArray(), 0);

            if (incomplete_packet_buffer.Count < Defines.HEADERSIZE + bodySize)
            {
                return;
            }

            byte[] completedMessage = new byte[bodySize];
            incomplete_packet_buffer.CopyTo(Defines.HEADERSIZE, completedMessage, 0, bodySize);

            string received_text = Encoding.UTF8.GetString(completedMessage);
            Debug.Log($"[Echo from Server] {received_text}");

            incomplete_packet_buffer.RemoveRange(0, Defines.HEADERSIZE + bodySize);
        }
    }


    public void Send()
    {
        if (client_socket == null || !client_socket.Connected)
        {
            Debug.Log("Not Connected to Server");
            return;
        }

        string msg = this.input_field.text;

        Packet packet = new Packet();
        // packet.Push(msg); 원래 넣어야하는데 Packet 스크립트 수정 후 넣어야 함
        packet.RecordSize();

        byte[] data_to_send = new byte[packet.position];
        Array.Copy(packet.buffer, 0, data_to_send, 0, packet.position);

        client_socket.BeginSend(data_to_send, 0, data_to_send.Length, SocketFlags.None, new AsyncCallback(SendCallback), null);
    }

    private void SendCallback(IAsyncResult ar)
    {
        try
        {
            int byte_sent = client_socket.EndSend(ar);

            Debug.Log($"Sent {byte_sent} byte to server");
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }

    public void DisConnect()
    {
        if (client_socket != null && client_socket.Connected)
        {
            client_socket.Shutdown(SocketShutdown.Both);
            client_socket.Close();
        }
        client_socket = null;
    }

    void OnApplicationQuit()
    {
        DisConnect();
    }

}
