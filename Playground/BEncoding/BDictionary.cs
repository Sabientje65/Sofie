using System.Collections;

namespace Playground.BEncoding;

public class BDictionary : IBType, IEnumerable<KeyValuePair<string, IBType>>
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

    public IEnumerator<KeyValuePair<string, IBType>> GetEnumerator() => _internalDictionary.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    
    private T Read<T>(string key) where T : IBType => (T)_internalDictionary[key];
}