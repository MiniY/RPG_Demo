using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Explicit authored ground shape for a renderer-only occupant; never inferred from canopy/shadow pixels.</summary>
public sealed class MapGroundFootprint : MonoBehaviour
{
    [SerializeField] private Vector2[] vertices = { new Vector2(-.5f, -.5f), new Vector2(.5f, -.5f), new Vector2(.5f, .5f), new Vector2(-.5f, .5f) };
    public void Configure(Vector2[] points)
    { if (points == null || points.Length < 3) throw new ArgumentException("GroundFootprintRequired"); vertices = (Vector2[])points.Clone(); }
    public IReadOnlyList<Vector2> Vertices => Array.AsReadOnly(vertices);
}
