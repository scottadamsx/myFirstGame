using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class RoadData
{
    public float[] xs, ys, zs;
    public float width;
    public string kind;
    public int PointCount => xs != null ? xs.Length : 0;
}

[Serializable]
public class LandmarkData
{
    public string name;
    public float x, y, z;
}

[Serializable]
public class CityData
{
    public List<RoadData> roads;
    public List<LandmarkData> landmarks;

    public static CityData Load()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "stjohns_game_data.json");
        if (!File.Exists(path))
        {
            Debug.LogError($"City data missing at {path}");
            return new CityData { roads = new List<RoadData>(), landmarks = new List<LandmarkData>() };
        }
        return JsonUtility.FromJson<CityData>(File.ReadAllText(path));
    }

    public LandmarkData Landmark(string name)
    {
        foreach (var l in landmarks)
            if (l.name == name) return l;
        return null;
    }
}
