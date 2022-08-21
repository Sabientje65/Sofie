using System.Collections;

namespace Playground.BEncoding;

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