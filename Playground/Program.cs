using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Web;
using Playground.BEncoding;

namespace Playground;

public class Program
{
    private const string FilePath = @"D:\Users\danny\Downloads\[HorribleSubs] Toaru Kagaku no Railgun T - 01 [1080p].mkv.torrent";

    private const string Id = "qB-awfiaur27v367ab21";
    
    public static async Task Main()
    {
        // implementation guide for reference:
        // https://allenkim67.github.io/programming/2016/05/04/how-to-make-your-own-bittorrent-client.html
        
        // using var reader = new BEncodingDeserializer(File.OpenRead(FilePath));
        var n = typeof(FixedSizeNonTerminatedStringMarshaller).FullName;

        var v2 = new PackagedString(Id);
        var packedBytes = ToBytes(v2);
        var unpackedBytes = FromBytes<PackagedString>(packedBytes);
        
        // var announceRequest = new AnnounceRequest(
        //     1, 
        //     1,
        //     "abc",
        //     Id,
        //     Random.Shared.Next(),
        //     6881 // https://www.speedguide.net/port.php?port=6881 bittorrent
        // );
        //
        //Torrent meta is always a dictionary
        var metaDictionary = BEncodingSerializer.Deserialize(File.OpenRead(FilePath)) as BDictionary;

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


        await ConnectUdp(
            meta.AnnounceList.Last(x => x.StartsWith("udp")),
            meta
        );
        // await new TorrentClient(meta.AnnounceURL).Announce(meta);


        // transactionid 
        // buff[12] - 0;
        // buff[13] - 0;
        // buff[14] - 0;
        // buff[15] - 0;

        // Buffer.SetByte(buff, 0, 0x417 27 10 19 80);

        // buff[0] 

        // sock.SendAsync(
        //     new []
        //     {
        //         ,
        //         
        //         0,
        //         
        //     }    
        // )

        // var client = new TorrentClient(meta.AnnounceURL);
        // await client.Announce(meta);

        // await ConnectHttp(meta.AnnounceURL);
    }

    // private static async Task ConnectHttp(string url)
    // {
    //     //http://nyaa.tracker.wf:7777/announce
    //     
    //     var parameters = new Dictionary<string, string>
    //     {
    //         { "info_hash",  HttpUtility.UrlEncode(new BString(meta.Info.Chunks[0].Hash)) },
    //         { "peer_id",    HttpUtility.UrlEncode(new BString(Id)) },
    //         { "port",       HttpUtility.UrlEncode(new BString("6882")) },
    //         { "uploaded",   HttpUtility.UrlEncode(new BString("0")) },
    //         { "downloaded", HttpUtility.UrlEncode(new BString("0")) },
    //         { "left",       HttpUtility.UrlEncode(new BString((meta.Info.Chunks.Length * 20).ToString())) },
    //         { "event",      HttpUtility.UrlEncode(new BString("started")) }
    //     };
    // }

    private static async Task ConnectUdp(string uriStr, TorrentMeta meta)
    {
        var uri = new Uri(uriStr);
        
        await Task.CompletedTask;
        
        // https://www.bittorrent.org/beps/bep_0015.html
        // If a response is not received after 15 * 2 ^ n seconds, the client should retransmit the request, where n starts at 0 and is increased up to 8 (3840 seconds) after every retransmission
        
        // using var sock = new Socket(SocketType.Dgram, ProtocolType.Udp);
        // await sock.ConnectAsync(
        //     new Uri(meta.AnnounceURL).Host,    
        //     new Uri(meta.AnnounceURL).Port    
        // );

        // send connection request
        // BitConverter.GetBytes()
        // 0x41727101980
        var connectionRequest = new ConnectRequest(Math.Abs(Random.Shared.Next()));
        var connectRequestBuffer = ToBytes(connectionRequest);

        
        // var connectRequest = new byte[16];
        //
        // var connectionId = BitConverter.GetBytes(0x41727101980);
        // var transactionId = new byte[4];
        // Random.Shared.Next();
        
        // swap bytes to big endian
        SwapEndianness(connectRequestBuffer, 0, 8);
        SwapEndianness(connectRequestBuffer, 12, 4);
        
        // Array.Copy(connectionId, connectRequest, 8);
        // connectRequest[8] = 0; // action
        // Array.Copy(transactionId, 0, connectRequest, 12, 4);

        // return;

        using var sock = new Socket(SocketType.Dgram, ProtocolType.Udp);
        
        // using var sock = new Socket(SocketType.Dgram, ProtocolType.Udp);
        await sock.ConnectAsync(
            uri.Host,    
            uri.Port
        );
        
        sock.Send(connectRequestBuffer);
        
        
        // distinction between http/udp based trackers, even on connect level
        
        var connectResponseBuffer = new byte[16];
        sock.Receive(connectResponseBuffer);

        // first: convert BE to LE
        SwapEndianness(connectResponseBuffer, 0, 4);
        SwapEndianness(connectResponseBuffer, 4, 4);
        
        
        var connectionResponse = FromBytes<ConnectResponse>(connectResponseBuffer);
        
        if(connectionResponse.TransactionId != connectionRequest.TransactionId) throw new Exception("Transaction ids dont match!");

        var announceRequest = new AnnounceRequest(
            connectionResponse.ConnectionId, 
            connectionResponse.TransactionId,
            meta.Info.Chunks[0].Hash,
            Id,
            Random.Shared.Next(),
            6881 // https://www.speedguide.net/port.php?port=6881 bittorrent
        );

        var announceRequestBuffer = ToBytes(announceRequest);
        
        // swap to BE
        SwapEndianness(announceRequestBuffer, 0, 8); 
        SwapEndianness(announceRequestBuffer, 8, 4);
        SwapEndianness(announceRequestBuffer, 12, 4);
        SwapEndianness(announceRequestBuffer, 56, 8);
        SwapEndianness(announceRequestBuffer, 64, 8);
        SwapEndianness(announceRequestBuffer, 72, 8);
        SwapEndianness(announceRequestBuffer, 80, 4);
        SwapEndianness(announceRequestBuffer, 84, 4);
        SwapEndianness(announceRequestBuffer, 88, 4);
        SwapEndianness(announceRequestBuffer, 92, 4);
        SwapEndianness(announceRequestBuffer, 96, 2);
        
        sock.Send(announceRequestBuffer);

        var bufferSize = sock.ReceiveBufferSize;
        // var announceResponseBuffer = sock.Receive();
    }

    private static byte[] ToBytes<T>(T structure) where T : struct
    {
        // Easy/efficient conversion of struct <> byte[]
        // https://www.genericgamedev.com/general/converting-between-structs-and-byte-arrays/
        // Worth checking out: https://benbowen.blog/post/fun_with_makeref/
        
        var size = Marshal.SizeOf<T>();
        var bytes = new byte[size];
        
        // allocate memory for our structure, obtain a pointer to the memory reserved for it
        var structurePointer = Marshal.AllocHGlobal(size);
        
        
        Marshal.StructureToPtr(
            structure, 
            structurePointer, 
            false // no previous data assumed to be present, see: https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.marshal.structuretoptr?view=net-7.0
        );
        
        Marshal.Copy(structurePointer, bytes, 0, size);
        Marshal.FreeHGlobal(structurePointer);

        return bytes;
    }

    private static T FromBytes<T>(byte[] bytes) where T : struct
    {
        var size = Marshal.SizeOf<T>();
        if (size != bytes.Length) throw new Exception("Size mismatch!");
        
        var structurePointer = Marshal.AllocHGlobal(size);
        
        Marshal.Copy(bytes, 0, structurePointer, size);
        var structure = Marshal.PtrToStructure<T>(structurePointer);
        
        Marshal.FreeHGlobal(structurePointer);

        return structure;
    }
    
    private static void SwapEndianness(byte[] source, int offset, int length)
    {
        var end = offset + length;

        for (var idx = 0; idx < length / 2; idx++)
        {
            var leftIdx = offset + idx;
            var rightIdx = end - idx - 1;
            var left = source[leftIdx];
            var right = source[rightIdx];
            
            source[leftIdx] = right;
            source[rightIdx] = left;
        }
    }

    // private static void ToBigEndian(byte[] source)
    // {
    //     // simple index swap based technique
    //     var halfwayPoint = source.Length / 2;
    //     for (var leftIdx = 0; leftIdx < halfwayPoint; leftIdx++)
    //     {
    //         var rightIdx = source.Length - leftIdx - 1;
    //         
    //         var left = source[leftIdx];
    //         var right = source[rightIdx];
    //         source[leftIdx] = right;
    //         source[rightIdx] = left;
    //     }
    // }
    
    // https://stackoverflow.com/q/65877653
    [StructLayout(LayoutKind.Explicit, Pack = 0, Size = 16)]
    public struct ConnectRequest
    {
        public ConnectRequest(int transactionId) => TransactionId = transactionId;
        
        [FieldOffset(0)]
        public readonly long ProtocolId = 0x41727101980;
        
        [FieldOffset(8)]
        public readonly int Action = 0; // connect
        
        [FieldOffset(12)]
        public readonly int TransactionId = 0;
    }

    [StructLayout(LayoutKind.Explicit, Pack = 0, Size = 16)]
    public struct ConnectResponse
    {
        [FieldOffset(0)]
        public readonly int Action;

        [FieldOffset(4)]
        public readonly int TransactionId;
        
        [FieldOffset(8)]
        public readonly long ConnectionId;
    }
    
    [DebuggerDisplay("{GetValue()}")]
    [StructLayout(LayoutKind.Explicit, Pack = 0, Size = 20)]
    public struct PackagedString
    {
        [MarshalAs(
            UnmanagedType.CustomMarshaler, 
            MarshalType = "Playground.Program+FixedSizeNonTerminatedStringMarshaller", 
            MarshalCookie = "20", 
            SizeConst = 20, 
            MarshalTypeRef = typeof(FixedSizeNonTerminatedStringMarshaller))
        ]
        [FieldOffset(0)]
        private char[] _value;

        public string GetValue() => new string(_value);
        
        public PackagedString(string value)
        {
            _value = value.ToCharArray();
        }
    }
    
    public class FixedSizeNonTerminatedStringMarshaller : ICustomMarshaler
    {
        private int _size;

        public FixedSizeNonTerminatedStringMarshaller()
        {
            
        }
        
        public FixedSizeNonTerminatedStringMarshaller(int size)
        {
            
        }
        
        // Method expected by CLR
        public static ICustomMarshaler GetInstance(string pstrCookie) => new FixedSizeNonTerminatedStringMarshaller(20);
        
        public void CleanUpManagedData(object ManagedObj)
        {
            return;
        }

        public void CleanUpNativeData(IntPtr pNativeData)
        {
            Marshal.FreeHGlobal(pNativeData);
        }

        public int GetNativeDataSize() => 20;

        public IntPtr MarshalManagedToNative(object ManagedObj)
        {
            // allocate memory for our structure, obtain a pointer to the memory reserved for it
            // var structurePointer = Marshal.AllocHGlobal(_size);

            var ptr = Marshal.AllocHGlobal(_size);
            Marshal.Copy(Encoding.ASCII.GetBytes((string)ManagedObj), 0, ptr, _size);
            return ptr;

            // Marshal.StructureToPtr(
            //     structure, 
            //     structurePointer, 
            //     false // no previous data assumed to be present, see: https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.marshal.structuretoptr?view=net-7.0
            // );

            // Marshal.Copy(structurePointer, bytes, 0, size);
            // Marshal.FreeHGlobal(structurePointer);

            // return bytes;
        }

        public object MarshalNativeToManaged(IntPtr pNativeData)
        {
            var buffer = new byte[_size];
            Marshal.Copy(pNativeData, buffer, 0, _size);
            return Encoding.ASCII.GetString(buffer);
        }
    }

    [StructLayout(LayoutKind.Explicit, Pack = 0, Size = 98)]
    public struct AnnounceRequest
    {
        public AnnounceRequest(
            long connectionId, 
            int transactionId, 
            string infoHash, 
            string peerId,
            int key,
            short port
        )
        {
            ConnectionId = connectionId;
            TransactionId = transactionId;
            InfoHash = infoHash;
            PeerId = peerId;
            Downloaded = 0;
            Left = 0;
            Uploaded = 0;
            Key = key;
            Port = port;
        }
        
        [FieldOffset(0)]
        public readonly long ConnectionId;

        [FieldOffset(8)]
        public readonly  int Action = 1; // announce

        [FieldOffset(12)]
        public readonly int TransactionId;

        // 20 bytes
        [FieldOffset(16)]
        // [MarshalAs()]
        public readonly string InfoHash;

        // 20 bytes
        [FieldOffset(36)]
        public readonly string PeerId;

        [FieldOffset(56)]
        public readonly long Downloaded;

        [FieldOffset(64)]
        public readonly long Left;

        [FieldOffset(72)]
        public readonly long Uploaded;

        [FieldOffset(80)]
        public readonly int Event = 0; // 0: none; 1: completed; 2: started; 3: stopped

        [FieldOffset(84)]
        public readonly int IpAddress = 0;

        [FieldOffset(88)]
        public readonly int Key;

        [FieldOffset(92)]
        public readonly int NumWant = -1;

        [FieldOffset(96)]
        public readonly short Port;
    }
    
    [StructLayout(LayoutKind.Explicit, Pack = 0)]
    public struct AnnounceResponse
    {
        [FieldOffset(0)]
        public readonly int Action;
        
        [FieldOffset(4)]
        public readonly int TransactionId;
        
        [FieldOffset(8)]
        public readonly int Interval;
        
        [FieldOffset(12)]
        public readonly int Leechers;
        
        // offset of 6
        [FieldOffset(16)]
        public readonly int[] Seeders; // peers
        
        [FieldOffset(20)]
        public readonly int IpAddress; 
        
        [FieldOffset(24)]
        public readonly int TcpPort;

    }
    
    class TorrentClient
    {
        private TcpClient _tcp;
        
        private HttpClient _httpClient = new HttpClient(); 
        
        public TorrentClient(string url)
        {
            
            
            // _tcp.
            
            // _httpClient = new HttpClient
            // {
            //     // BaseAddress = new Uri(url)
            // };
        }

        public async Task Announce(TorrentMeta meta)
        {
            // Reference implementation:
            //https://github.com/alanmcgovern/monotorrent/blob/master/src/MonoTorrent.Trackers/MonoTorrent.Connections.Tracker/HTTPTrackerConnection.cs
            
            
            var parameters = new Dictionary<string, string>
            {
                { "info_hash",  HttpUtility.UrlEncode(new BString(meta.Info.Chunks[0].Hash)) },
                { "peer_id",    HttpUtility.UrlEncode(new BString(Id)) },
                { "port",       HttpUtility.UrlEncode(new BString("6882")) },
                { "uploaded",   HttpUtility.UrlEncode(new BString("0")) },
                { "downloaded", HttpUtility.UrlEncode(new BString("0")) },
                { "left",       HttpUtility.UrlEncode(new BString((meta.Info.Chunks.Length * 20).ToString())) },
                { "event",      HttpUtility.UrlEncode(new BString("started")) }
            };

            var parametersQuery = String.Join(
                "&",
                parameters.Select(x => $"{x.Key}={x.Value}")
            );

            var url = "http://nyaa.tracker.wf:7777/announce/?" + parametersQuery;
            var response = await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, url));
        }
    }
}
