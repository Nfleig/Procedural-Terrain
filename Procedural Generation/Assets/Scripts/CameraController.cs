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
    public float ChunkArea;
    public WorldGenerator worldGen;

    private Camera camera;
    private bool isClicking;

    private float rotX = 0;
    // Start is called before the first frame update
    void Awake()
    {
        camera = Camera.main;
    }

    private void Start()
    {
        GenerateChunks(true);
        //worldGen.GenerateChunk(0, 0);
    }

    // Update is called once per frame
    void Update()
    {
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

    void GenerateChunks(bool generateAll)
    {
        Vector2 chunkAreaTopCorner = new Vector2(transform.position.x + ChunkArea, transform.position.z + ChunkArea);
        Vector2 chunkAreaBottomCorner = new Vector2(transform.position.x - ChunkArea, transform.position.z - ChunkArea);

        if(generateAll)
        {
            for (int x = (int)(chunkAreaBottomCorner.x / 8); x * 8 < chunkAreaTopCorner.x; x++)
            {
                for (int y = (int)(chunkAreaBottomCorner.y / 8); y * 8 < chunkAreaTopCorner.y; y++)
                {
                    //print(x + " " + y);
                    worldGen.GenerateChunk(x, y);
                }
            }
        }
    }
}
