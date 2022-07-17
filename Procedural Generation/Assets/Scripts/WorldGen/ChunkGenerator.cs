using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class ChunkGenerator : MonoBehaviour
{

    // Public properties
    public Shader terrainShader;
    public static float depth;
    public RenderTexture heightMapTexture;
    public RenderTexture colorMapTexture;
    public float averageElevation;
    public float[] biomeData;
    public int biome;

    // Private properties
    private Mesh _mesh;
    private Vector3[] _vertices;
    private Vector2[] _uvs;
    private int[] _triangles;
    private MeshRenderer _meshRenderer;
    private static WorldGenerator _worldGenerator;
    private Chunk _chunk;
    private Animator _animator;
    private int _xSize;
    private int _ySize;
    private float _scale;

    /*
     * This function finds the world generator object and the chunk generator's cooresponding chunk object
     */

    private void Awake()
    {
        _worldGenerator = GameObject.FindWithTag("WorldGenerator").GetComponent<WorldGenerator>();
        _chunk = GetComponent<Chunk>();
        _animator = GetComponent<Animator>();
    }

    /*
     * This function prepares the chunk to have it's height and color maps written to by the
     * terrain shader. It also sets the vertex resolution and scale of the chunk
     */

    public void InitializeTerrain(int xSize, int ySize, float scale)
    {

        // Set the size values for the chunk generator

        this._xSize = xSize;
        this._ySize = xSize;
        this._scale = scale;

        // Create the height map texture

        heightMapTexture = new RenderTexture(xSize + 1, ySize + 1, 1);
        heightMapTexture.enableRandomWrite = true;
        heightMapTexture.Create();

        // Create the color map texture

        colorMapTexture = new RenderTexture(xSize + 1, ySize + 1, 1);
        colorMapTexture.enableRandomWrite = true;
        colorMapTexture.Create();
    }

    /*
     * This function will use the height map texture to generate a new mesh for the chunk.
     * It will also draw the color texture onto the mesh.
     *
     * For more information, see this Unity chunk generation tutorial: https://www.youtube.com/watch?v=eJEpeUH1EMg
     */

    public void DeformMesh()
    {

        // Read the height map texture into a texture2D object

        Texture2D texture = new Texture2D(heightMapTexture.width, heightMapTexture.height, TextureFormat.RGB24, false);
        RenderTexture.active = heightMapTexture;
        texture.ReadPixels(new Rect(0, 0, heightMapTexture.width, heightMapTexture.height), 0, 0);

        // Create a new mesh and set the mesh filter's mesh

        _mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = _mesh;

        // Initialize the vertex array

        _vertices = new Vector3[(_xSize + 1) * (_ySize + 1)];

        // Initialize the triangle array

        _triangles = new int[_xSize * _ySize * 6];

        // Initialize the UV array

        _uvs = new Vector2[_vertices.Length];

        int i = 0;
        int vert = 0;
        int tris = 0;

        // Iterate over every pixel in the texture
        for (int z = 0; z <= _ySize; z++)
        {
            for (int x = 0; x <= _xSize; x++)
            {

                // Create a new vertex in a grid with the height of it's cooresponding pixel in the height map

                float height = texture.GetPixel(x, z).r;
                _vertices[i] = new Vector3(x * _scale, height * depth, z * _scale);

                // Get the average elevation for the chunk

                averageElevation = averageElevation + height;

                // If this pixel isn't at the edge, calculate what triangles it's in and add them to the triangle array

                if (x != _xSize && z != _ySize) {
                    _triangles[tris] = vert;
                    _triangles[tris + 1] = vert + 1 + _xSize;
                    _triangles[tris + 2] = vert + 1;
                    _triangles[tris + 3] = vert + 1;
                    _triangles[tris + 4] = vert + 1 + _xSize;
                    _triangles[tris + 5] = vert + 2 + _xSize;

                    vert++;
                    tris += 6;
                }
                
                // Generate the UV for this vertex

                _uvs[i] = new Vector2((float)x / _xSize, (float)z / _ySize);
                i++;
            }
            vert++;
        }

        // Calculate the averate elevation

        averageElevation /= _vertices.Length;

        _meshRenderer = GetComponent<MeshRenderer>();

        // Apply these changes to the mesh

        UpdateMesh();

        // Set the mesh for the mesh collider

        GetComponent<MeshCollider>().sharedMesh = _mesh;
        
        // Finish generating

        FinishedGenerating();
    }

    /*
     * This function applies the vertex, triangle, and UV arrays to the mesh and sets some values in the chunk shader
     * For more information on Unity chunk generation, see this tutorial: https://www.youtube.com/watch?v=eJEpeUH1EMg
     */

    void UpdateMesh()
    {
        // Set the vertices, triangles, and UVs in the mesh

        _mesh.SetVertices(_vertices);
        _mesh.SetTriangles(_triangles, 0);
        _mesh.SetUVs(0, _uvs);

        // Recalculate the normals of the mesh to correct shading

        _mesh.RecalculateNormals();
        
        // Set the chunk's texture to the colormap
        
        if (Application.isEditor) {

            // Unity complains if you try to mess with MeshRenderer.material in the editor, so this gets around that

            Material terrainMaterial = new Material(_meshRenderer.sharedMaterial);
            terrainMaterial.SetTexture("ColorMap", colorMapTexture);
            _meshRenderer.sharedMaterial = terrainMaterial;
        }
        else {
            _meshRenderer.material.SetTexture("ColorMap", colorMapTexture);
        }
    }

    /*
     * This function is called when the chunk finishes generating, and can be used for any gameplay
     * things that need to be done. Right now all it does is calculate if the chunk can be settled
     */

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
        _animator.SetTrigger("Place");
    }

    /*
     * This function will calculate if a chunk can be settled.
     * Right now that is only based on the average elevation
     */

    void CalculateIsHabitable()
    {
        if (biomeData[3] > 0.66 || averageElevation > 0.1)
        {
            if (_chunk)
            {
                _chunk.habitable = false;
            }
        }
    }
}
