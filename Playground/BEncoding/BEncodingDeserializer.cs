using System.Text;

namespace Playground.BEncoding;

public static class BTokens
{
    public const byte Terminator = 0x65;             //e
    public const byte StringLengthTerminator = 0x3A; //:
    public const byte IntegerIdentifier = 0x69;      //i
    public const byte DictionaryIdentifier = 0x64;   //d
    public const byte ListIdentifier = 0x6C;         //l
}


//Attempt to mimic native JsonSerializer API feel
public class BEncodingSerializer
{
    public static string Serialize(IBType value) => BEncodingSerializerImpl.Serialize(value);
    public static IBType Deserialize(Stream stream) => BEncodingDeserializerImpl.Deserialize(stream);
}

internal class BEncodingSerializerImpl
{
    public static string Serialize(IBType value)
    {
        var sb = new StringBuilder();
        var sw = new StringWriter(sb);
        Serialize(value, sw);
        return sb.ToString();
    }

    private static void Serialize(IBType value, TextWriter writer)
    {
        //If we need more extensibility -> BEncodingSerializer.Register( ISerializer )
        //In case of StackOverflow exceptions, convert to stack/queue based solution

        if (value is BInteger bInt)
        {
            writer.Write(BTokens.IntegerIdentifier);
            writer.Write(bInt.AsInteger());
            writer.Write(BTokens.Terminator);
        }
        
        if (value is BString bStr)
        {
            writer.Write(bStr.Length);
            writer.Write(BTokens.StringLengthTerminator);
            writer.Write(bStr);
        }

        if (value is BList bList)
        {
            writer.Write(BTokens.ListIdentifier);
            foreach (var bItem in bList) Serialize(bItem, writer);
            writer.Write(BTokens.Terminator);
        }

        if (value is BDictionary bDict)
        {
            writer.Write(BTokens.DictionaryIdentifier);
            foreach (var bKv in bDict)
            {
                Serialize(new BString(bKv.Key), writer);
                Serialize(bKv.Value, writer);
            }
            writer.Write(BTokens.Terminator);
        }
    }
}

internal class BEncodingDeserializerImpl
{
    public static IBType Deserialize(Stream stream)
    {
        var context = new DeserializationContext(stream);
        context.Advance(); //Load first value
        return ReadValue(context);
    }
    
    private static BString ReadString(DeserializationContext context)
    {
        //Strings are unique in their type token also dubbing as the start of their length indicator
        var length = ToInt(context.Current);
        while (context.Advance(BTokens.StringLengthTerminator))
        {
            length = length * 10 + ToInt(context.Current);
        }

        var stringData = new byte[length];

        for (var i = 0; i < stringData.Length; i++)
        {
            context.Advance(BTokens.Terminator);
            stringData[i] = context.Current;
        }

        return new BString(stringData);
    }
        
    private static BInteger ReadInteger(DeserializationContext context)
    {
        //Keep reading until we hit a terminator - we read from left to right, so multiply each previous value with a factor of 10 to get the proper integer representation
        //874 ->
        //8
        //80 + 7
        //870 + 4
        var value = 0;
        while (context.Advance(BTokens.Terminator))
        {
            value = value * 10 + ToInt(context.Current);
        }
        
        return new BInteger(value);
    }
        
    private static BList ReadList(DeserializationContext context)
    {
        //Keep reading individual items until we hit a terminator
        var list = new List<IBType>();
        while (context.Advance(BTokens.Terminator))
        {
            list.Add(ReadValue(context));
        }
        
        return new BList(list);
    }
        
    private static BDictionary ReadDictionary(DeserializationContext context)
    {
        //Dictionaries consist of keyvaluepairs with keys always being string values
        //keep consuming until we hit a terminator, this marks our final item
        var dictionary = new Dictionary<string, IBType>();
        while (context.Advance(BTokens.Terminator))
        {
            var key = ReadString(context);
            context.Advance(); //Consume next value indicator token
            var value = ReadValue(context);
            dictionary[key] = value;
        }
        return new BDictionary(dictionary);
    }
    
    private static IBType ReadValue(DeserializationContext context)
    {
        return CurrentType(context) switch
        {
            BType.String     => ReadString(context),
            BType.Integer    => ReadInteger(context),
            BType.List       => ReadList(context),
            BType.Dictionary => ReadDictionary(context),
            _                => throw new ArgumentException("Unknown type identifier: " + (char)context.Current)
        };
    }

    private static int ToInt(byte b) => b - 0x30; //integer representation - ASCII table 0

    private static BType CurrentType(DeserializationContext context)
    {
        var current = context.Current;
        
        if (current > 0x30 && current <= 0x39)    return BType.String;     //1 - 9
        if (current == 0x69)                      return BType.Integer;    //i
        if (current == 0x64)                      return BType.Dictionary; //d
        if (current == 0x6C)                      return BType.List;       //l
        return BType.Unknown;
    }
    
    private class DeserializationContext
    {
        private readonly Stream _stream;
        private readonly byte[] _buffer = new byte[2048];

        private int _bufferSize;
        private int _bufferIndex;
        
        public byte Current { get; private set; }

        public DeserializationContext(Stream stream)
        {
            _stream = stream;
        }

        public void Advance()
        {
            //Refill our buffer once exhausted, reset our index to the start
            if (_bufferIndex == _bufferSize)
            {
                _bufferSize = _stream.Read(_buffer, 0, _buffer.Length);
                _bufferIndex = 0;
            }

            //Consume from our buffer, move our index to the right by 1 by decrementing our offset
            Current = _buffer[_bufferIndex++];
        }
        
        public bool Advance(byte terminator)
        {
            Advance();
            return Current != terminator;
        }
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
