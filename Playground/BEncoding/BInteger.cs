﻿namespace Playground.BEncoding;

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