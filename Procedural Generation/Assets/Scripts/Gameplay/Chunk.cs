using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Chunk : MonoBehaviour
{
    public GameObject houseObject;
    private MeshRenderer renderer;
    private ChunkGenerator generator;
    public Vector2 position;
    public bool hovering = false;
    public bool selected = false;
    public bool claimed = false;
    // Start is called before the first frame update
    void Start()
    {
        renderer = GetComponent<MeshRenderer>();
        generator = GetComponent<ChunkGenerator>();
        //position = new Vector2(transform.position.x / 8, transform.position.y / 8);
    }

    public void Load()
    {
        renderer.enabled = true;
    }

    public void Unload()
    {
        renderer.enabled = false;
    }

    public void Settle()
    {
        claimed = true;
        Deselect();
    }

    public void SpawnHouses()
    {
        Instantiate(houseObject, transform);
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (!hovering && selected)
            {
                Deselect();
            }
        }
    }

    private void Select()
    {
        transform.position = new Vector3(transform.position.x, 5, transform.position.z);
        //Settle();
    }

    private void Deselect()
    {
        transform.position = new Vector3(transform.position.x, 0, transform.position.z);
        selected = false;
    }

    private void OnMouseDown()
    {
        selected = !selected;
        if (selected)
        {
            Select();
        } else
        {
            Deselect();
        }
    }

    private void OnMouseOver()
    {
        if (!hovering)
        {
            //transform.position = new Vector3(transform.position.x, 3, transform.position.z);
            hovering = true;
        }
    }

    private void OnMouseExit()
    {
        hovering = false;
        transform.position = new Vector3(transform.position.x, 0, transform.position.z);
    }
}
