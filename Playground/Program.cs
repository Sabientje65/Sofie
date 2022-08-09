using System.Collections;
using System.Text;

namespace Playground;

public class Program
{
    private const string FilePath = @"D:\Users\danny\Downloads\[HorribleSubs] Toaru Kagaku no Railgun T - 01 [1080p].mkv.torrent";
    
    public static async Task Main()
    {
        //First read all bytes in our torrent metadata
        var metaBytes = new Queue<byte>( //TODO: Convert to span
            File.ReadAllBytes(FilePath)
        );
        
        //Strip BOM header
        // metaBytes.Pop();
        // metaBytes.Pop();
        // metaBytes.Pop();

        //First value indicates the start of a dictionary
        // metaBytes.Dequeue();

        var values = new Dictionary<string, object>();
        var currentKey = String.Empty;
        var currentValue = (object)null;

        var lengthIndication = new List<byte>();

        // var type = PeekNextType(metaBytes);

        //Attempt at implementing a basic BEncoding reader as per spec described in 
        //https://www.bittorrent.org/beps/bep_0003.html#bencoding
        var metadata = ReadDictionary(metaBytes);

        // while (type != BEncodingType.Unknown)
        // {
        //     if (type == BEncodingType.Dictionary)
        //     {
        //         //Consume
        //         metaBytes.Dequeue();
        //
        //         //Read until we finished consuming our dictionary
        //         while (PeekNextType(metaBytes) != BEncodingType.End)
        //         {
        //             var key = ReadString(metaBytes, ReadLength(metaBytes));
        //             var value = ReadString(metaBytes, ReadLength(metaBytes));
        //             
        //             Console.WriteLine("key: " + key + " value: " + value);
        //         }
        //     }
        //
        //     break;
        //     // type = PeekNextType(metaBytes);
        // }


        // var length = ReadLength(metaBytes);
        // var str = ReadString(metaBytes, length);
    }

    private static List<object> ReadList(Queue<byte> metaBytes)
    {
        var list = new List<object>();
        metaBytes.Dequeue(); //consume

        while (PeekNextType(metaBytes) != BEncodingType.End)
        {
            list.Add(ReadValue(metaBytes));
        }
        
        metaBytes.Dequeue(); //Consume end

        return list;
    }

    private static Dictionary<string, object> ReadDictionary(Queue<byte> metaBytes)
    {
        var dictionary = new Dictionary<string, object>();
        metaBytes.Dequeue(); //Consume
        
        while (PeekNextType(metaBytes) != BEncodingType.End)
        {
            var key = ReadString(metaBytes);
            dictionary[key] = ReadValue(metaBytes);
        }

        metaBytes.Dequeue(); //Consume end

        return dictionary;
    }

    private static object ReadValue(Queue<byte> metaBytes)
    {
        return PeekNextType(metaBytes) switch
        {
            BEncodingType.List => ReadList(metaBytes),
            BEncodingType.Dictionary => ReadDictionary(metaBytes),
            BEncodingType.Integer => ReadInteger(metaBytes),
            BEncodingType.String => ReadString(metaBytes)
        };
    }

    private static BEncodingType PeekNextType(Queue<byte> metaBytes)
    {
        return metaBytes.Peek() switch
        {
            0x65                        => BEncodingType.End,         //e
            0x6C                        => BEncodingType.List,        //l
            0x64                        => BEncodingType.Dictionary,  //d
            0x69                        => BEncodingType.Integer,     //i
            byte and > 0x30 and <= 0x39 => BEncodingType.String,      //between 1 and 9
            _                           => BEncodingType.Unknown
        };
    }

    private static int ReadInteger(Queue<byte> metaData)
    {
        metaData.Dequeue(); //Consume type marker

        var isNegative = metaData.Peek() == 0x2D;
        if (isNegative) metaData.Dequeue(); //Skip negative marker
        
        var value = ReadInteger(metaData, 0x65);
        
        return isNegative ? -value : value;
    }

    private static int ReadInteger(Queue<byte> metaData, byte terminator)
    {
        //877
        //8 * 10 = 80
        //+ 7 = 87
        //87 * 10 = 870
        //etc.
        
        var value = 0;
        for (var current = metaData.Dequeue(); current != terminator; current = metaData.Dequeue())
        {
            value *= 10;
            value += current - 0x30; //x030 = UTF-8  DIGIT ZERO
        }
        return value;
    }

    private static string ReadString(Queue<byte> metaBytes)
    {
        var sb = new StringBuilder();
        var length = ReadInteger(metaBytes, 0x3A);
        while (length-- > 0)
        {
            sb.Append((char)metaBytes.Dequeue());   
        }
        return sb.ToString();
    }
}

public class BEncoding
{
    private int _metaIndex = 0;
    private int _typeBytesLeft = 0;
    private BEncodingType _currentType = BEncodingType.Unknown;
    private readonly byte[] _bytes;

    public BEncoding(byte[] bytes)
    {
        _bytes = bytes;
    }

    public bool Next()
    {
        var nextByte = _bytes[_metaIndex];

        _metaIndex++;
        return true;
    }
}

public enum BEncodingType : byte
{
    Unknown,
    End,
    
    Dictionary,
    List,
    String,
    Integer
}