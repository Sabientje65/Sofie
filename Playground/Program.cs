using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
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
            meta.AnnounceList.Last(x => x.StartsWith("udp"))    
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

    private static async Task ConnectUdp(string uriStr)
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
        var connectionRequest = new ConnectionRequest(Math.Abs(Random.Shared.Next()));
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
        
        
        var connectionResponse = FromBytes<ConnectionResponse>(connectResponseBuffer);
        
        if(connectionResponse.TransactionId != connectionRequest.TransactionId) throw new Exception("Transaction ids dont match!");
    }

    private static byte[] ToBytes<T>(T structure) where T : struct
    {
        // Easy/efficient conversion of struct <> byte[]
        // https://www.genericgamedev.com/general/converting-between-structs-and-byte-arrays/
        
        var size = Marshal.SizeOf<T>();
        var bytes = new byte[size];
        
        // allocate memory for our structure, obtain a pointer to the memory reserved for it
        var structurePointer = Marshal.AllocHGlobal(size);
        
        
        Marshal.StructureToPtr(
            structure, 
            structurePointer, 
            true // destroy previous contained data prior to writing
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
    public struct ConnectionRequest
    {
        public ConnectionRequest(int transactionId) => TransactionId = transactionId;
        
        [FieldOffset(0)]
        public readonly long ProtocolId = 0x41727101980;
        
        [FieldOffset(8)]
        public readonly int Action = 0;
        
        [FieldOffset(12)]
        public readonly int TransactionId = 0;
    }

    [StructLayout(LayoutKind.Explicit, Pack = 0, Size = 16)]
    public struct ConnectionResponse
    {
        [FieldOffset(0)]
        public readonly int Action;

        [FieldOffset(4)]
        public readonly int TransactionId;
        
        [FieldOffset(8)]
        public readonly long ConnectionId;
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
