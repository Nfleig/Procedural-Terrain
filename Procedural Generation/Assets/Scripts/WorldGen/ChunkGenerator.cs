using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class ChunkGenerator : MonoBehaviour
{
    Mesh mesh;

    Vector3[] vertices;
    Vector2[] uvs;
    Color[] colors;
    int[] triangles;
    public RenderTexture heightMapTexture;
    public RenderTexture colorMapTexture;
    MeshRenderer meshRenderer;
    public Shader terrainShader;
    public static float depth;
    private static WorldGenerator worldGen;
    private Chunk chunk;
    int xSize;
    int ySize;
    float scale;

    public float averageElevation;
    public float[] biomeData;
    public int biome;


    private void Awake()
    {
        worldGen = GameObject.FindWithTag("World Generator").GetComponent<WorldGenerator>();
        chunk = GetComponent<Chunk>();
    }

    public void InitializeTerrain(int xSize, int ySize, float scale)
    {
        this.xSize = xSize;
        this.ySize = xSize;
        this.scale = scale;

        heightMapTexture = new RenderTexture(xSize + 1, ySize + 1, 1);
        heightMapTexture.enableRandomWrite = true;
        heightMapTexture.Create();

        colorMapTexture = new RenderTexture(xSize + 1, ySize + 1, 1);
        colorMapTexture.enableRandomWrite = true;
        colorMapTexture.Create();

        //updateMesh();
    }

    public void GenerateTerrain(Texture2D heightMap)
    {
        GetComponent<MeshRenderer>().sharedMaterial.SetTexture("HeightMap", heightMap);
    }

    int[] cosValues = { 1, 0, -1, 0, 1, -1, -1, 1 };
    int[] sinValues = { 0, 1, 0, -1, 1, 1, -1, -1 };
    public void DeformMesh()
    {

        Texture2D texture = new Texture2D(heightMapTexture.width, heightMapTexture.height, TextureFormat.RGB24, false);
        RenderTexture.active = heightMapTexture;
        texture.ReadPixels(new Rect(0, 0, heightMapTexture.width, heightMapTexture.height), 0, 0);
        mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;
        vertices = new Vector3[(xSize + 1) * (ySize + 1)];
        int i = 0;
        for (int z = 0; z <= ySize; z++)
        {
            for (int x = 0; x <= xSize; x++)
            {
                //float height = Mathf.PerlinNoise(x * 0.3f, z * 0.3f);
                float height = texture.GetPixel(x, z).r;
                averageElevation = (averageElevation + height) / 2;
                vertices[i++] = new Vector3(x * scale, height * depth, z * scale);
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
        uvs = new Vector2[vertices.Length];
        i = 0;
        for (int z = 0; z <= ySize; z++)
        {
            for (int x = 0; x <= xSize; x++)
            {
                uvs[i] = new Vector2((float)x / xSize, (float)z / ySize);
                //print(uvs[i]);
                i++;
            }
        }
        meshRenderer = GetComponent<MeshRenderer>();
        updateMesh();
        GetComponent<MeshCollider>().sharedMesh = mesh;
        FinishedGenerating();
    }

    void updateMesh()
    {
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        Material chunkMaterial = new Material(terrainShader);
        chunkMaterial.SetTexture("HeightMap", heightMapTexture);
        chunkMaterial.SetTexture("ColorMap", colorMapTexture);
        chunkMaterial.SetFloat("Depth", depth);

        meshRenderer.sharedMaterial = chunkMaterial;
    }

    public void FinishedGenerating()
    {
        CalculateIsHabitable();
        float maxPercent = 0;
        for (int i = 0; i < biomeData.Length; i++)
        {
            if (biomeData[i] > maxPercent)
            {
                biome = i;
                maxPercent = biomeData[i];
            }
        }
    }

    void CalculateIsHabitable()
    {
        if (biomeData[3] > 0.66 || averageElevation > 0.1)
        {
            if (chunk)
            {
                chunk.habitable = false;
            }
        }
    }
}
