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


    public void InitializeTerrain(int xSize, int ySize, float scale)
    {
        meshRenderer = GetComponent<MeshRenderer>();
        mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;
        vertices = new Vector3[(xSize + 1) * (ySize + 1)];
        int i = 0;
        for (int z = 0; z <= ySize; z++)
        {
            for (int x = 0; x <= xSize; x++)
            {
                vertices[i++] = new Vector3(x * scale, 0, z * scale);
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

        heightMapTexture = new RenderTexture(xSize + 1, ySize + 1, 1);
        heightMapTexture.enableRandomWrite = true;
        heightMapTexture.Create();

        colorMapTexture = new RenderTexture(xSize + 1, ySize + 1, 1);
        colorMapTexture.enableRandomWrite = true;
        colorMapTexture.Create();

        updateMesh();

        GetComponent<MeshCollider>().sharedMesh = mesh;
    }

    public void GenerateTerrain(Texture2D heightMap)
    {
        GetComponent<MeshRenderer>().sharedMaterial.SetTexture("HeightMap", heightMap);
    }

    public void GenerateTerrain(float[,] heightMap, int xSize, int ySize, float scale, float depth, Color[] colors)
    {
        mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;
        vertices = new Vector3[(xSize + 1) * (ySize + 1)];
        int i = 0;
        for (int z = 0; z <= ySize; z++)
        {
            for (int x = 0; x <= xSize; x++)
            {
                float y = heightMap[x, z] * depth;

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
        /*
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
        */

        this.colors = colors;

        updateMesh();

        GetComponent<MeshCollider>().sharedMesh = mesh;
    }

    public void DeformMesh()
    {
        Texture2D texture = new Texture2D(heightMapTexture.width, heightMapTexture.height, TextureFormat.RGB24, false);
        RenderTexture.active = heightMapTexture;
        texture.ReadPixels(new Rect(0, 0, heightMapTexture.width, heightMapTexture.height), 0, 0);
        int i = 0;
        for (int z = 0; z <= heightMapTexture.width; z++)
        {
            for (int x = 0; x <= heightMapTexture.width; x++)
            {
                vertices[i++].y = texture.GetPixel(x, z).r;
            }
        }
    }

    void updateMesh()
    {
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.colors = colors;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        Material chunkMaterial = new Material(terrainShader);
        chunkMaterial.SetTexture("HeightMap", heightMapTexture);
        chunkMaterial.SetTexture("ColorMap", colorMapTexture);
        chunkMaterial.SetFloat("Depth", depth);

        meshRenderer.sharedMaterial = chunkMaterial;
    }

    
}
