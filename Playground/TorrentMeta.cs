namespace Playground;

//Start with use-case single file torrent
public class TorrentMeta
{
    public string AnnounceURL { get; set; }
    public List<string> AnnounceList { get; set; }
    public string Encoding { get; set; }
    
    public TorrentMetaInfo Info { get; set; }
}