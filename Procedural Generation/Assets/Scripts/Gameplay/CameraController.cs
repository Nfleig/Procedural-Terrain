using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    // Public properties
    public float MovementSpeed;
    public float ZoomSpeed;
    public float MinZoomDistance;
    public float MaxZoomDistance;
    public float XRotateThreshold;
    public int VisibleChunks;
    public bool screenSaver;
    
    // Private properties
    private static WorldGenerator _worldGenerator;
    private static GameManager _gameManager;
    private Camera _camera;
    private bool _isClicking;
    private bool _start = false;
    private bool _firstFrame = true;
    private float _rotX = 0;
    private int[] _cosValues = { 1, 0, -1, 0, 1, -1, -1, 1 };
    private int[] _sinValues = { 0, 1, 0, -1, 1, 1, -1, -1 };

    /*
     * This function finds some objects that will be needed for later
     */
    void Awake()
    {
        _camera = Camera.main;
        _worldGenerator = GameObject.FindWithTag("World Generator").GetComponent<WorldGenerator>();
        _gameManager = GameObject.FindWithTag("GameController").GetComponent<GameManager>();
    }

    void Update()
    {
        // The fog of war is the closest thing to actual gameplay right now.
        // If it is enabled then only load one chunk at (0,0) and settle it

        if (_gameManager.fogOfWar)
        {
            if (_firstFrame)
            {
                _firstFrame = false;
                Chunk starterChunk = _worldGenerator.GPUGenerateChunk(0, 0);
                starterChunk.Settle();
            }
        }

        // Load all nearby chunks

        GenerateChunks();

        // If the world is first loading, then freeze the camera

        if (_start || !_worldGenerator.IsGenerating())
        {
            _start = true;
        } else
        {
            return;
        }

        // Get player inputs

        float xInput = Input.GetAxis("Horizontal");
        float zInput = Input.GetAxis("Vertical");
        if (Input.GetButtonDown("Fire2")){
            _isClicking = true;
        }
        if(Input.GetButtonUp("Fire2")){
            _isClicking = false;
        }
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Application.Quit();
        }

        // Move the camera with the keyboard inputs

        Vector3 dir = transform.forward * zInput + transform.right * xInput;

        // If the screen saver option is on, then override the user input and move the camera automatically

        if (screenSaver && _start)
        {
            dir = new Vector3(1, 0, 1);
        }

        // Move the camera

        transform.position += dir * MovementSpeed * Time.deltaTime;

        if(_isClicking){
            
            // If the player is right clicking, then rotate the camera with their mouse

            // Get the mouse inputs

            float rotY = Input.GetAxis("Mouse X") * 5;
            _rotX += Input.GetAxis("Mouse Y") * 5;

            // Clamp the X rotation

            _rotX = Mathf.Clamp(_rotX, -XRotateThreshold, XRotateThreshold);

            // Apply the Y rotation to the camera parent object

            transform.eulerAngles = new Vector3(0, transform.eulerAngles.y + rotY, 0);

            // Apply the X rotation to the camera itself

            _camera.transform.eulerAngles = new Vector3(50 - _rotX, _camera.transform.eulerAngles.y, 0);
        }

        // Get the scroll wheel input

        float scroll = Input.GetAxis("Mouse ScrollWheel");

        // Zoom in the camera, while clamping the zoom

        float distance = Vector3.Distance(transform.position, _camera.transform.position);
        if(!((distance < MinZoomDistance && scroll > 0f) || (distance > MaxZoomDistance && scroll < 0f))){
            _camera.transform.position += _camera.transform.forward * scroll * 5;
        }
    }

    /*
     * This function will recursively reveal any chunks around the given position using the given depth.
     * It is only used for the fog of war setting, and needs to be reworked
     */

    public void ShowChunks(Vector2 position, int depth)
    {
        // Get the chunk generator at the given position, or generate the chunk if it doesn't exist

        ChunkGenerator newChunkGenerator;
        if (!_worldGenerator.chunkDictionary.ContainsKey(position))
        {
            Chunk newChunk = _worldGenerator.GPUGenerateChunk((int)position.x, (int)position.y);
            newChunkGenerator = newChunk.GetComponent<ChunkGenerator>();
        } else
        {
            newChunkGenerator = _worldGenerator.chunkDictionary[position].GetComponent<ChunkGenerator>();
        }

        // If the depth is too far or the elevation is too high, then return

        if (depth >= 10 || (newChunkGenerator.averageElevation > 0.05f && depth != 0))
        {
            return;
        }

        // Otherwise generate the chunks around this chunk

        for (int dir = 0; dir < 4; dir++)
        {
            Vector2 newPosition = new Vector2(position.x + _cosValues[dir], position.y + _sinValues[dir]);
            ShowChunks(newPosition, depth + 1);
        }
    }

    /*
     * This function will load or generate all nearby chunks, and unload chunks that are too far away
     */

    void GenerateChunks()
    {
        // Get the coordinates of the chunk that the camera is in

        int chunkCoordX = Mathf.RoundToInt(transform.position.x / _worldGenerator.chunkSize);
        int chunkCoordY = Mathf.RoundToInt(transform.position.z / _worldGenerator.chunkSize);

        // Get all currently loaded chunks

        Chunk[] allChunks = (Chunk[])GameObject.FindObjectsOfType(typeof(Chunk));

        // Iterate over all loaded chunks

        foreach (Chunk chunk in allChunks)
        {
            if (Mathf.Abs(chunk.position.x - (float) chunkCoordX) > VisibleChunks || Mathf.Abs(chunk.position.y - (float)chunkCoordY) > VisibleChunks)
            {
                // If the chunk is too far from the camera, then unload it

                chunk.Unload();
                if (screenSaver)
                {

                    // If the screen saver is on, then we can fully delete the chunk to save memory

                    _worldGenerator.chunkDictionary.Remove(chunk.position);
                    Destroy(chunk.gameObject);
                }
            }
        }

        // Iterate through the area of chunks around the camera

        for (int x = -VisibleChunks; x < VisibleChunks; x++)
        {
            for (int y = -VisibleChunks; y < VisibleChunks; y++)
            {
                // Get the current chunk coordinate

                Vector2 chunkCoord = new Vector2(chunkCoordX + x, chunkCoordY + y);

                // If the current chunk has already been generated, then load it from the chunk dictionary

                if (_worldGenerator.chunkDictionary.ContainsKey(chunkCoord))
                {
                    if (!_worldGenerator.chunkDictionary[chunkCoord].isLoaded())
                    {

                        _worldGenerator.chunkDictionary[chunkCoord].Load();
                    }
                }

                // Otherwise have the world generator generate it
                
                else
                {
                    if (!_gameManager.fogOfWar)
                    {
                        Chunk newChunk = _worldGenerator.GPUGenerateChunk((int)chunkCoord.x, (int)chunkCoord.y);
                    }

                }
            }
        }
    }
}
