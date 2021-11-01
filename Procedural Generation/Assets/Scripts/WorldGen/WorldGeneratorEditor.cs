using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor (typeof(WorldGenerator))]
public class WorldGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        WorldGenerator worldGen = (WorldGenerator)target;

        DrawDefaultInspector();

        if (GUILayout.Button("Generate"))
        {
            worldGen.CreateWorld();
        }
    }
}
