public class GameRoom
{
    public UserToken player1_token { get; private set; }
    public UserToken player2_token { get; private set; }

    private byte[] board_state = new byte[9];
    private byte current_turn_player_index;

    private bool is_game_over = false;

    public GameRoom(UserToken token1, UserToken token2)
    {
        player1_token = token1;
        player2_token = token2;

        player1_token.SetGameRoom(this);
        player2_token.SetGameRoom(this);

        ResetGame();
    }

    public void ResetGame()
    {
        for (int i = 0; i < 9; i++)
        {
            board_state[i] = 0;
        }
        is_game_over = false;

        Random rand = new Random();
        current_turn_player_index = (byte)rand.Next(1, 3);

        SendMatchSuccessAck();
        SendTurnUpdateAck();
        SendBoardUpdateAck();
    }

    private void SendMatchSuccessAck()
    {
        Packet packet1 = new Packet(PROTOCOL.MATCH_SUCCESS_ACK);
        packet1.Push((byte)1); // player1의 인덱스는 1
        player1_token.Send(packet1);

        Packet packet2 = new Packet(PROTOCOL.MATCH_SUCCESS_ACK);
        packet2.Push((byte)2); // player2의 인덱스는 2
        player2_token.Send(packet2);
    }

    private void SendTurnUpdateAck()
    {
        Packet packet = new Packet(PROTOCOL.TURN_UPDATE_ACK);
        packet.Push(current_turn_player_index);

        player1_token.Send(packet);
        player2_token.Send(packet);
    }

    private void SendBoardUpdateAck()
    {
        Packet packet = new Packet(PROTOCOL.BOARD_UPDATE_ACK);
        packet.Push(board_state);

        player1_token.Send(packet);
        player2_token.Send(packet);
    }

    private void SendGameOverAck(byte winner_index)
    {
        Packet packet = new Packet(PROTOCOL.GAME_OVER_ACK);
        packet.Push(winner_index);

        player1_token.Send(packet);
        player2_token.Send(packet);
    }

    public void PlaceStone(UserToken request_token, byte position)
    {
        byte player_index = (request_token == player1_token) ? (byte)1 : (byte)2;
        if (player_index != current_turn_player_index)
        {
            return;
        }

        if (position < 0 || position >= 9 || board_state[position] != 0)
        {
            return;
        }

        board_state[position] = player_index;

        SendBoardUpdateAck();

        byte winner = CheckWinner();

        if (winner != 255)
        {
            is_game_over = true;
            SendGameOverAck(winner);
        }
        else
        {
            current_turn_player_index = (current_turn_player_index == 1) ? (byte)2 : (byte)1;
            SendTurnUpdateAck();
        }
    }

    private byte CheckWinner()
    {
        byte[][] winPatterns = new byte[][]
        {
            new byte[] {0, 1, 2}, new byte[] {3, 4, 5}, new byte[] {6, 7, 8},
            new byte[] {0, 3, 6}, new byte[] {1, 4, 7}, new byte[] {2, 5, 8},
            new byte[] {0, 4, 8}, new byte[] {2, 4, 6}
        };

        foreach (var pattern in winPatterns)
        {
            if (board_state[pattern[0]] != 0 &&
                board_state[pattern[0]] == board_state[pattern[1]] &&
                board_state[pattern[1]] == board_state[pattern[2]])
            {
                return board_state[pattern[0]];
            }
        }

        bool isDraw = true;
        for (int i = 0; i < 9; i++)
        {
            if (board_state[i] == 0)
            {
                isDraw = false;
                break;
            }
        }

        return isDraw ? (byte)0 : (byte)255;
    }

    public void OnPlayerDisconnect(UserToken disconnected_token)
    {
        if (is_game_over) return;

        is_game_over = true;
        byte winner_index = (disconnected_token == player1_token) ? (byte)2 : (byte)1;
        SendGameOverAck(winner_index);
    }
}