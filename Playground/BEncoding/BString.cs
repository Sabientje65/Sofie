using System.Diagnostics;
using System.Text;

namespace Playground.BEncoding;

[DebuggerDisplay("{(string)this}")]
public class BString : IBType
{
    private readonly byte[] _data;
    public int Length => _data.Length;

    public BString(byte[] data)
    {
        _data = data;
    }

    public BString(string str)
    {
        _data = Encoding.UTF8.GetBytes(str);
    }

    public BString ReadChunk(int start, int end) => new BString(_data[start..end]);

    public string AsString() => Encoding.UTF8.GetString(_data);
    public byte[] AsBytes() => _data;
        
    //Operator overloading?

    public static implicit operator string(BString bStr) => bStr.AsString();
}