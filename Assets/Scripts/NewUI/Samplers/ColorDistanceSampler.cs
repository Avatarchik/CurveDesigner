﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Assets.NewUI
{
    [System.Serializable]
    public class ColorDistanceSampler : ValueDistanceSampler<Color, ColorSamplerPoint, ColorDistanceSampler>
    {
        public ColorDistanceSampler(string fieldDisplayName): base(fieldDisplayName) { }

        public ColorDistanceSampler(ColorDistanceSampler objToClone) : base(objToClone) { }

        public override Color GetDefaultVal()
        {
            return Color.white;
        }
        public override Color Lerp(Color val1, Color val2, float lerp)
        {
            return Color.Lerp(val1,val2,lerp);
        }
    }
    [System.Serializable]
    public class ColorSamplerPoint : FieldEditableSamplerPoint<Color, ColorSamplerPoint, ColorDistanceSampler>
    {
        public override Color Field(string displayName, Color originalValue)
        {
            var label = new GUIContent();
            label.text = displayName;
            return EditorGUILayout.ColorField(label, originalValue,showEyedropper:false,showAlpha:true,hdr:false);
        }

        public override Color Add(Color v1, Color v2) { return v1 + v2; }

        public override Color Subtract(Color v1, Color v2) { return v1 - v2; }

        public override Color Zero() { return Color.black; }

        public override Color CloneValue(Color value)
        {
            return value;
        }
    }
}
