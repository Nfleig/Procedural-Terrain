using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class City : MonoBehaviour
{
    public int level;

    public float morale;
    public float order;
    public float money;
    public float taxRate;
    public int population;

    public Structure[] structures;

    public City()
    {
        level = 1;
        morale = 100;
        order = 50;
        taxRate = 2.5f;
        structures = new Structure[5];
    }
}
