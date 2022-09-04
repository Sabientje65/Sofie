using Playground.BEncoding;

namespace Playground;

public class Program
{
    private const string FilePath = @"D:\Users\danny\Downloads\[HorribleSubs] Toaru Kagaku no Railgun T - 01 [1080p].mkv.torrent";
    
    public static async Task Main()
    {
        //Boristicus is cool yo
        
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
    }
}
