using System;
using System.Text;

public class Packet
{
    public byte[] buffer { get; private set; }
    public int position { get; private set; }

    public Packet()
    {
        buffer = new byte[4096];
        position = Defines.HEADERSIZE;
    }

    public Packet(byte[] buffer)
    {
        buffer = new byte[buffer.Length];
        Array.Copy(buffer, buffer, buffer.Length);
        position = Defines.HEADERSIZE;
    }

    public Packet(PROTOCOL protocol) : this()
    {
        Push((short)protocol);
    }

    public void RecordSize()
    {
        short body_size = (short)(position - Defines.HEADERSIZE);
        byte[] header = BitConverter.GetBytes(body_size);
        header.CopyTo(buffer, 0);
    }
    
    public void Push(string value) // string 을 byte로 변환하는 코드 필요
    {
        // byte[] data = BitConverter.GetBytes(value);
        // Array.Copy(data, 0, buffer, position, data.Length);
        // position += data.Length;
    }


    public void Push(short value)
    {
        byte[] data = BitConverter.GetBytes(value);
        Array.Copy(data, 0, buffer, position, data.Length);
        position += data.Length;
    }

    public void Push(byte value)
    {
        buffer[position] = value;
        position++;
    }

    public void Push(byte[] value)
    {
        Array.Copy(value, 0, buffer, position, value.Length);
        position += value.Length;
    }

    public short PopShort()
    {
        short value = BitConverter.ToInt16(buffer, position);
        position += 2;
        return value;
    }

    public byte PopByte()
    {
        byte value = buffer[position];
        position++;
        return value;
    }
}