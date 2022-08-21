namespace Playground.BEncoding;


public class BEncodingDeserializer : IDisposable
{
    //Attempt at implementing a basic BEncoding reader as per spec described in 
    //https://www.bittorrent.org/beps/bep_0003.html#bencoding
        
    private const byte Terminator = 0x65;             //e
    private const byte StringLengthTerminator = 0x3A; //:

    private readonly Stream _stream;
    private readonly byte[] _buffer = new byte[2048];

    private int _bufferSize;
    private int _bufferIndex;
    private byte _current;

    public BEncodingDeserializer(Stream stream)
    {
        _stream = stream;
    }

    public IBType Consume()
    {
        Advance(); //Advance type token
        return ReadValue();
    }

    private IBType ReadValue()
    {
        return CurrentType() switch
        {
            BType.String     => ReadString(),
            BType.Integer    => ReadInteger(),
            BType.List       => ReadList(),
            BType.Dictionary => ReadDictionary(),
            _                => throw new ArgumentException("Unknown type identifier: " + (char)_current)
        };
    }
        
    private void Advance()
    {
        //Refill our buffer once exhausted, reset our index to the start
        if (_bufferIndex == _bufferSize)
        {
            _bufferSize = _stream.Read(_buffer, 0, _buffer.Length);
            _bufferIndex = 0;
        }

        //Consume from our buffer, move our index to the right by 1 by decrementing our offset
        _current = _buffer[_bufferIndex++];
    }

    private bool Advance(byte terminator)
    {
        Advance();
            
        //For ease, simply check for our general terminator
        return _current != terminator;
    }

    private BString ReadString()
    {
        //Strings are unique in their type token also dubbing as the start of their length indicator
        var length = ToInt(_current);
        while (Advance(StringLengthTerminator)) length = length * 10 + ToInt(_current);

        var stringData = new byte[length];

        for (var i = 0; i < stringData.Length; i++)
        {
            Advance();
            stringData[i] = _current;
        }

        return new BString(stringData);
    }
        
    private BInteger ReadInteger()
    {
        //Keep reading until we hit a terminator - we read from left to right, so multiply each previous value with a factor of 10 to get the proper integer representation
        //874 ->
        //8
        //80 + 7
        //870 + 4
        var value = 0;
        while (Advance(Terminator)) value = value * 10 + ToInt(_current);
        return new BInteger(value);
    }
        
    private BList ReadList()
    {
        //Keep reading individual items until we hit a terminator
        var list = new List<IBType>();
        while (Advance(Terminator)) list.Add(ReadValue());
        return new BList(list);
    }
        
    private BDictionary ReadDictionary()
    {
        //Dictionaries consist of keyvaluepairs with keys always being string values
        //keep consuming until we hit a terminator, this marks our final item
        var dictionary = new Dictionary<string, IBType>();
        while (Advance(Terminator))
        {
            var key = ReadString();
            Advance(); //Consume next value indicator token
            var value = ReadValue();
            dictionary[key] = value;
        }
        return new BDictionary(dictionary);
    }

    private BType CurrentType()
    {
        if (_current > 0x30 && _current <= 0x39)   return BType.String;     //1 - 9
        if (_current == 0x69)                      return BType.Integer;    //i
        if (_current == 0x64)                      return BType.Dictionary; //d
        if (_current == 0x6C)                      return BType.List;       //l
        return BType.Unknown;
    }
        
    private int ToInt(byte b) => b - 0x30; //Subtract position of 0 in ASCII table

    public void Dispose()
    {
        _stream.Dispose();
    }

    private enum BType
    {
        Unknown,
            
        Dictionary,
        List,
        Integer,
        String
    }
}