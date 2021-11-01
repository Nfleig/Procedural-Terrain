using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Module
{
    public struct Attribute
    {
        public string name;
        public float value;

        public Attribute(string name, float value)
        {
            this.name = name;
            this.value = value;
        }
    }

    List<Attribute> Attributes;


}
