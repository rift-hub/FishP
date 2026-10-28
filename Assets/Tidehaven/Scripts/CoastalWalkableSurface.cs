using UnityEngine;
using System.Collections.Generic;

/// <summary>Registered deck colliders and the walkable lagoon surface.</summary>
[RequireComponent(typeof(BoxCollider))]
public sealed class CoastalWalkableSurface : MonoBehaviour
{
    static readonly List<CoastalWalkableSurface> surfaces = new List<CoastalWalkableSurface>();
    BoxCollider deck;
    void OnEnable() { deck = GetComponent<BoxCollider>(); if (!surfaces.Contains(this)) surfaces.Add(this); }
    void OnDisable() { surfaces.Remove(this); }
    public static bool TryDeck(float x, float z, out float height)
    {
        height = float.NegativeInfinity;
        var ray = new Ray(new Vector3(x, 50f, z), Vector3.down);
        for (int i = surfaces.Count - 1; i >= 0; i--)
        {
            var s = surfaces[i];
            if (!s) { surfaces.RemoveAt(i); continue; }
            if (s.deck && s.deck.enabled && s.deck.Raycast(ray, out var hit, 100f) && hit.normal.y > .6f)
                height = Mathf.Max(height, hit.point.y);
        }
        return !float.IsNegativeInfinity(height);
    }
    public static float Height(float x, float z, float maximumStepHeight = float.PositiveInfinity)
    {
        float ground = Mathf.Max(0.025f, CoastalIslandLayout.Height(x, z));
        if (TryDeck(x, z, out float deckHeight) && deckHeight <= maximumStepHeight)
            ground = Mathf.Max(ground, deckHeight);
        return ground;
    }
    public static bool CanEnter(float x, float z)
    {
        return x >= CoastalIslandLayout.MinX && x <= CoastalIslandLayout.MaxX && z >= CoastalIslandLayout.MinZ && z <= CoastalIslandLayout.MaxZ;
    }
}
