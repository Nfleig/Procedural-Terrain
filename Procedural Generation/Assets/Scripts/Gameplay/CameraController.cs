using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    public float MovementSpeed;
    public float ZoomSpeed;

    public float MinZoomDistance;
    public float MaxZoomDistance;

    public float XRotateThreshold;
    public int VisibleChunks;
    public WorldGenerator worldGen;

    private Camera camera;
    private bool isClicking;

    private float rotX = 0;

    private Dictionary<Vector2, Chunk> chunkDictionary = new Dictionary<Vector2, Chunk>();
    // Start is called before the first frame update
    void Awake()
    {
        camera = Camera.main;
    }

    private void Start()
    {
        //GenerateChunks(true);
        //worldGen.GenerateChunk(0, 0);
    }

    // Update is called once per frame
    void Update()
    {
        GenerateChunks();
        float xInput = Input.GetAxis("Horizontal");
        float zInput = Input.GetAxis("Vertical");
        if(Input.GetButtonDown("Fire2")){
            isClicking = true;
        }
        if(Input.GetButtonUp("Fire2")){
            isClicking = false;
        }
        
        Vector3 dir = transform.forward * zInput + transform.right * xInput;
        transform.position += dir * MovementSpeed * Time.deltaTime;
        if(isClicking){
            float rotY = Input.GetAxis("Mouse X") * 5;
            rotX += Input.GetAxis("Mouse Y") * 5;
            rotX = Mathf.Clamp(rotX, -XRotateThreshold, XRotateThreshold);
            //print(rotX);
            transform.eulerAngles = new Vector3(0, transform.eulerAngles.y + rotY, 0);
            camera.transform.eulerAngles = new Vector3(50 - rotX, camera.transform.eulerAngles.y, 0);
        }
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        float distance = Vector3.Distance(transform.position, camera.transform.position);
        if(!((distance < MinZoomDistance && scroll > 0f) || (distance > MaxZoomDistance && scroll < 0f))){
            camera.transform.position += camera.transform.forward * scroll * 5;
        }

    }

    void GenerateChunks()
    {

        int chunkCoordX = Mathf.RoundToInt(transform.position.x / worldGen.chunkSize);
        int chunkCoordY = Mathf.RoundToInt(transform.position.z / worldGen.chunkSize);

        Chunk[] allChunks = (Chunk[])GameObject.FindObjectsOfType(typeof(Chunk));
        foreach (Chunk chunk in allChunks)
        {
            if (Vector3.Distance(transform.position, chunk.transform.position) > VisibleChunks * 8)
            {
                chunk.Unload();
            }
        }
        for (int x = -VisibleChunks; x < VisibleChunks; x++)
        {
            for (int y = -VisibleChunks; y < VisibleChunks; y++)
            {
                Vector2 chunkCoord = new Vector2(chunkCoordX + x, chunkCoordY + y);
                if (chunkDictionary.ContainsKey(chunkCoord))
                {
                    chunkDictionary[chunkCoord].Load();
                }
                else
                {
                    Chunk newChunk = worldGen.GenerateChunkImmediate((int)chunkCoord.x, (int)chunkCoord.y);
                    chunkDictionary.Add(chunkCoord, newChunk);
                    //print(chunkCoordX + " " + chunkCoordY + " " + chunkCoord);

                }
            }
        }
    }
}
