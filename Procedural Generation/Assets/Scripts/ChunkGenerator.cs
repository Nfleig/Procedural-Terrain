using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class ChunkGenerator : MonoBehaviour
{
    Mesh mesh;

    Vector3[] vertices;
    int[] triangles;
    public int xSize;
    public int zSize;
    public float scale;
    public float depth;
    public int resolution;
    public float xOffset;
    public float yOffset;
    public int octaves;
    public float persistance;
    public float lacunarity;
    private float[,] heightMap;
    // Start is called before the first frame update
    void Update()
    {
        GenerateTerrain();
        
    }

    public void GenerateTerrain()
    {
        mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;
        generateMesh();
        updateMesh();
    }

    public void setHeightMap(float[,] heightMap)
    {
        this.heightMap = heightMap;
    }


    void generateMesh()
    {
        vertices = new Vector3[(xSize + 1) * (zSize + 1)];
        int i = 0;
        float[,] heightMap = GenerateHeightMap(xSize + 1, zSize + 1, xOffset, yOffset, octaves, persistance, lacunarity);
        for (int z = 0; z <= zSize; z++)
        {
            for (int x = 0; x <= xSize; x++)
            {
                float y = heightMap[x, z] * depth;
                //print(y);

                vertices[i++] = new Vector3(x * scale, y, z * scale);
            }
        }
        triangles = new int[xSize * zSize * 6];
        int vert = 0;
        int tris = 0;
        for (int z = 0; z < zSize; z++)
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

    }

    void updateMesh()
    {
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
    }

    float calculateNoise(int x, int y, int octaves, float persistance, float lacunarity)
    {
        float noise = 0;
        float amplitude = 1;
        float frequency = 1;
        float noiseHeight = 0;
        float halfWidth = xSize / 2;
        float halfHeight = zSize / 2;
        for (int i = 0; i < octaves; i++)
        {
            float xCoord = ((float) (x - halfWidth) / resolution * frequency * scale) + xOffset;
            float yCoord = ((float) (y - halfHeight) / resolution * frequency * scale) + yOffset;

            noise = Mathf.PerlinNoise(xCoord, yCoord) * 2 - 1;

            noiseHeight += noise * amplitude;

            amplitude *= persistance;
            frequency *= lacunarity;
        }
        noise *= noiseHeight;
        return noise;
    }

    float[,] GenerateHeightMap(int xSize, int ySize, float xOffset, float yOffset, int octaves, float persistance, float lacunarity)
    {
        
        float[,] heightMap = new float[xSize, ySize];
        float maxHeightValue = float.MinValue;
        float minHeightValue = float.MaxValue;
        for (int x = 0; x < xSize; x++)
        {
            for (int y = 0; y < ySize; y++)
            {
                float amplitude = 1;
                float frequency = 1;
                float noiseHeight = 0;
                for (int i = 0; i < octaves; i++)
                {
                    float xCoord = (((float)x + xOffset) /  frequency * scale);
                    float yCoord = (((float)y + yOffset)/  frequency * scale);

                    float noise = Mathf.PerlinNoise(xCoord, yCoord) * 2 - 1;
                    //print(xCoord + " " + yCoord);

                    noiseHeight += noise * amplitude;

                    amplitude *= persistance;
                    frequency *= lacunarity;
                    //noise *= noiseHeight;
                }
                maxHeightValue = Mathf.Max(maxHeightValue, noiseHeight);
                minHeightValue = Mathf.Min(minHeightValue, noiseHeight);
                heightMap[x, y] = noiseHeight;
            }
        }
        for (int x = 0; x < xSize; x++)
        {
            for (int y = 0; y < ySize; y++)
            {
                heightMap[x, y] = Mathf.InverseLerp(minHeightValue, maxHeightValue, heightMap[x, y]);
            }
        }
        return heightMap;
    }
}
