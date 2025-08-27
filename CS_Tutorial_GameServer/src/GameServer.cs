public class GameServer
{
    private Queue<UserToken> waiting_players = new Queue<UserToken>();

    public void OnClientConnected(UserToken token)
    {
        Console.WriteLine($"Client connected: {token.socket.RemoteEndPoint}");
        token.SetOnPacketReceive(OnPacketReceived);
        token.SetOnDisconnect(OnClientDisconnected);
    }

    public void OnPacketReceived(UserToken token, Packet packet)
    {
        short protocol_id = packet.PopShort();
        switch ((PROTOCOL)protocol_id)
        {
            case PROTOCOL.MATCH_REQ:
                Console.WriteLine($"Match request from client.");
                MatchRequest(token);
                break;
            case PROTOCOL.PLACE_STONE_REQ:
                byte position = packet.PopByte();
                Console.WriteLine($"Place stone request from client at position {position}");
                token.game_room.PlaceStone(token, position);
                break;
            default:
                break;
        }
    }

    private void MatchRequest(UserToken token)
    {
        lock (waiting_players)
        {
            waiting_players.Enqueue(token);

            if (waiting_players.Count >= 2)
            {
                UserToken player1 = waiting_players.Dequeue();
                UserToken player2 = waiting_players.Dequeue();

                Console.WriteLine("Match found! Starting new game.");
                new GameRoom(player1, player2);
            }
        }
    }

    private void OnClientDisconnected(UserToken token)
    {
        if (token.game_room != null)
        {
            token.game_room.OnPlayerDisconnect(token);
        }
    }
}