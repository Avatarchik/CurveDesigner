﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Curve3D : MonoBehaviour
{
    public BeizerCurve curve;
    [Range(.01f,3)]
    public float sampleRate = 1.0f;
    public Texture2D lineTex;
    public Texture2D circleIcon;
    public Texture2D squareIcon;
    void Start()
    {
    }

    void Update()
    {
        
    }
}
