using System;
using System.Collections.Generic;

[System.Serializable]
public class Manifest
{
    public Dictionary<long, PathIdMap_Asset> assets;
}

[Serializable]
public class PathIdMap
{
    public PathIdMap_Files[] Files;
}

[Serializable]
public class PathIdMap_Files
{
    public string Name;
    public PathIdMap_Asset[] Assets;
}

[Serializable]
public class PathIdMap_Asset
{
    public long PathID;
    public string Name;
    public string Type;
    public string GUID;
    public long? AddressablesPathID;
    public string? AddressablesCAB;
}