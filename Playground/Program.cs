using System.Collections;
using System.Text;

namespace Playground;

public class Program
{
    private const string FilePath = @"D:\Users\danny\Downloads\[HorribleSubs] Toaru Kagaku no Railgun T - 01 [1080p].mkv.torrent";
    
    public static async Task Main()
    {
        using var reader = new BEncodingDeserializer(File.OpenRead(FilePath));
        
        //Torrent meta is always a dictionary
        var metaDictionary = reader.Consume() as BDictionary;

        var chunksStr = metaDictionary.ReadDictionary("info")
            .ReadString("pieces");

        var chunks = new TorrentChunkInfo[chunksStr.Length / 20];
        for (var chunkIdx = 0; chunkIdx < chunks.Length; chunkIdx++)
        {
            var start = chunkIdx * 20;
            var end = start + 20;

            chunks[chunkIdx] = new TorrentChunkInfo
            {
                Index = chunkIdx,
                Hash = chunksStr.ReadChunk(start, end).AsString()
            };
        }
        
        var meta = new TorrentMeta
        {
            AnnounceURL = metaDictionary.ReadString("announce").AsString(),
            AnnounceList = metaDictionary.ReadList("announce-list")
                .Select(x => ((BString)((BList)x)[0]).AsString())
                .ToList(),
            Encoding = metaDictionary.ReadString("encoding").AsString(),
            Info = new TorrentMetaInfo
            {
                Length = metaDictionary.ReadDictionary("info").ReadInt("length"),
                Name = metaDictionary.ReadDictionary("info").ReadString("name").AsString(),
                PieceLength = metaDictionary.ReadDictionary("info").ReadInt("piece length"),
                Chunks = chunks
            }
        };
    }

    //Start with use-case single file torrent
    public class TorrentMeta
    {
        public string AnnounceURL { get; set; }
        public List<string> AnnounceList { get; set; }
        public string Encoding { get; set; }
    
        public TorrentMetaInfo Info { get; set; }
    }
    
    public class TorrentMetaInfo
    {
        public int Length { get; set; }
        public string Name { get; set; } 
    
        public int PieceLength { get; set; }
        public TorrentChunkInfo[] Chunks { get; set; }
    }

    public class TorrentChunkInfo
    {
        public int Index { get; set; }
        public string Hash { get; set; }
    }
    
    public interface IBType
    {
    }

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

    public class BInteger : IBType
    {
        private readonly int _value;

        public BInteger(int value)
        {
            _value = value;
        }

        public int AsInteger() => _value;

        public static implicit operator int(BInteger bInt) => bInt.AsInteger();
    }

    public class BDictionary : IBType
    {
        private readonly IDictionary<string, IBType> _internalDictionary;

        public IBType this[string key] => _internalDictionary[key];

        public BDictionary(IDictionary<string, IBType> dictionary)
        {
            _internalDictionary = dictionary;
        }

        public BString ReadString(string key) => Read<BString>(key);
        public BDictionary ReadDictionary(string key) => Read<BDictionary>(key);
        public BInteger ReadInt(string key) => Read<BInteger>(key);
        public BList ReadList(string key) => Read<BList>(key);

        private T Read<T>(string key) where T : IBType => (T)_internalDictionary[key];
    }

    public class BList : IBType, IEnumerable<IBType>
    {
        private readonly IList<IBType> _internalList;

        public IBType this[int index] => _internalList[index];
        
        public BList(IList<IBType> internalList)
        {
            _internalList = internalList;
        }

        public IEnumerator<IBType> GetEnumerator() => _internalList.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    
    private class BEncodingDeserializer : IDisposable
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

        public object Consume()
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
