namespace Playground;

public class TorrentMetaInfo
{
    public int Length { get; set; }
    public string Name { get; set; } 
    
    public int PieceLength { get; set; }
    public TorrentChunkInfo[] Chunks { get; set; }
}