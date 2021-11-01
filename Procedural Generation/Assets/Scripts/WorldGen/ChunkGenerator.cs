using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class ChunkGenerator : MonoBehaviour
{
    Mesh mesh;

    Vector3[] vertices;
    Vector2[] uvs;
    int[] triangles;
    public float xOffset;
    public float yOffset;
    public int octaves;
    public float persistance;
    public float lacunarity;
    private float[,] heightMap;
    // Start is called before the first frame update

    public void Start()
    {
        
    }

    public void GenerateTerrain(int xSize, int ySize, float scale, float depth, AnimationCurve heightCurve)
    {
        mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;
        generateMesh(xSize, ySize, scale, depth, heightCurve);
        updateMesh();

    }

    public void setHeightMap(float[,] heightMap)
    {
        this.heightMap = heightMap;
    }


    void generateMesh(int xSize, int ySize, float scale, float depth, AnimationCurve heightCurve)
    {
        vertices = new Vector3[(xSize + 1) * (ySize + 1)];
        int i = 0;
        //float[,] heightMap = GenerateHeightMap(xSize + 1, zSize + 1, xOffset, yOffset, octaves, persistance, lacunarity);
        for (int z = 0; z <= ySize; z++)
        {
            for (int x = 0; x <= xSize; x++)
            {
                float y = heightMap[z, x] * depth;
                //print(y);

                vertices[i++] = new Vector3(x * scale, y, z * scale);
            }
        }
        triangles = new int[xSize * ySize * 6];
        int vert = 0;
        int tris = 0;
        for (int z = 0; z < ySize; z++)
        {
            for (int x = 0; x < xSize; x++)
            {
                triangles[tris] = vert;
                triangles[tris + 1] = vert + 1 + xSize;
                triangles[tris + 2] = vert + 1;
                triangles[tris + 3] = vert + 1;
                triangles[tris + 4] = vert + 1 + xSize;
                triangles[tris + 5] = vert + 2 + xSize;

                vert++;
                tris += 6;
            }
            vert++;
        }
        Vector2[] uvs = new Vector2[vertices.Length];
        i = 0;
        for (int z = 0; z <= ySize; z++)
        {
            for (int x = 0; x <= xSize; x++)
            {
                uvs[i] = new Vector2(x / xSize, z / ySize);
                i++;
            }
        }

    }

    void updateMesh()
    {
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
    }

    
}
