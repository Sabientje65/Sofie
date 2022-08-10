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

        using var reader = new BEncodingReader(File.OpenRead(FilePath));
        var value = reader.Consume();
    }
    
    
    private class BEncodingReader : IDisposable
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
        

        public BEncodingReader(Stream stream)
        {
            _stream = stream;
        }

        public object Consume()
        {
            Advance(); //Advance type token
            return ReadValue();
        }

        private object ReadValue()
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

        private string ReadString()
        {
            var sb = new StringBuilder();
            
            //Strings are unique in their type token also dubbing as the start of their length indicator
            var length = ToInt(_current);
            while (Advance(StringLengthTerminator)) length = length * 10 + ToInt(_current);

            while (length-- > 0)
            {
                Advance();
                sb.Append((char)_current);
            }
            
            return sb.ToString();
        }
        
        private int ReadInteger()
        {
            //Keep reading until we hit a terminator - we read from left to right, so multiply each previous value with a factor of 10 to get the proper integer representation
            //874 ->
            //8
            //80 + 7
            //870 + 4
            var value = 0;
            while (Advance(Terminator)) value = value * 10 + ToInt(_current);
            return value;
        }
        
        private List<object> ReadList()
        {
            //Keep reading individual items until we hit a terminator
            var list = new List<object>();
            while (Advance(Terminator)) list.Add(ReadValue());
            return list;
        }
        
        private Dictionary<string, object> ReadDictionary()
        {
            //Dictionaries consist of keyvaluepairs with keys always being string values
            //keep consuming until we hit a terminator, this marks our final item
            var dictionary = new Dictionary<string, object>();
            while (Advance(Terminator))
            {
                var key = ReadString();
                Advance(); //Consume next value indicator token
                var value = ReadValue();
                dictionary[key] = value;
            }
            return dictionary;
        }

        private BType CurrentType()
        {
            if (_current > 0x30 && _current <= 0x39)   return BType.String;     //1 - 9
            if (_current == 0x69)                      return BType.Integer;    //i
            if (_current == 0x64)                      return BType.Dictionary; //d
            if (_current == 0x6C)                      return BType.List;       //l
            return BType.Unknown;
        }
        
        private int ToInt(byte b) => b - 0x30;

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
}
