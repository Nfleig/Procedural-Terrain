using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Chunk : MonoBehaviour
{
    public enum Biome {MOUNTAIN, PLAINS, DESERT, TUNDRA}

    private MeshRenderer renderer;
    private Outline outline;
    public int xPosition;
    public int yPosition;
    public bool hovering = false;
    public Biome biome { get; }
    // Start is called before the first frame update
    void Start()
    {
        renderer = GetComponent<MeshRenderer>();
        outline = GetComponent<Outline>();
        xPosition = (int)(transform.position.x / 8);
        yPosition = (int)(transform.position.z / 8);
    }

    public void Load()
    {
        renderer.enabled = true;
    }

    public void Unload()
    {
        renderer.enabled = false;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnMouseOver()
    {
        if (!hovering)
        {
            transform.Translate(new Vector3(0f, 3f, 0f));
            hovering = true;
        }
    }

    private void OnMouseExit()
    {
        hovering = false;
        transform.Translate(new Vector3(0f, -3f, 0f));
    }
}
