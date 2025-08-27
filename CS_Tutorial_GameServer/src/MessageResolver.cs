public class MessageResolver
{
    public delegate void CompletedMessageCallback(byte[] buffer);

    private int messageSize;
    private byte[] messageBuffer = new byte[4096]; // 버퍼 크기 확장
    private int currentPosition;
    private int positionToRead;
    private int remainBytes;

    public MessageResolver()
    {
        messageSize = 0;
        currentPosition = 0;
        positionToRead = 0;
        remainBytes = 0;
    }

    private bool ReadUntil(byte[] buffer, ref int srcPosition, int offset, int transferred)
    {
        if (currentPosition >= offset + transferred) return false;

        int copySize = positionToRead - currentPosition;
        if (remainBytes < copySize)
        {
            copySize = remainBytes;
        }

        Array.Copy(buffer, srcPosition, messageBuffer, currentPosition, copySize);

        srcPosition += copySize;
        currentPosition += copySize;
        remainBytes -= copySize;

        return currentPosition >= positionToRead;
    }

    public void OnReceive(byte[] buffer, int offset, int transferred, CompletedMessageCallback callback)
    {
        remainBytes = transferred;
        int srcPosition = offset;

        while (remainBytes > 0)
        {
            bool completed = false;
            if (currentPosition < Defines.HEADERSIZE)
            {
                positionToRead = Defines.HEADERSIZE;
                completed = ReadUntil(buffer, ref srcPosition, offset, transferred);
                if (!completed) return;

                messageSize = Get_BodySize();
                positionToRead = messageSize + Defines.HEADERSIZE;
            }

            completed = ReadUntil(buffer, ref srcPosition, offset, transferred);
            if (completed)
            {
                byte[] completed_message = new byte[messageSize + Defines.HEADERSIZE];
                Array.Copy(messageBuffer, 0, completed_message, 0, messageSize + Defines.HEADERSIZE);

                callback(completed_message);
                Clear_buffer();
            }
        }
    }

    private short Get_BodySize()
    {
        return BitConverter.ToInt16(messageBuffer, 0);
    }

    private void Clear_buffer()
    {
        currentPosition = 0;
        messageSize = 0;

        Array.Clear(messageBuffer, 0, messageBuffer.Length);
    }
}