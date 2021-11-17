﻿#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;

namespace ChaseMacMillan.CurveDesigner
{
    public static class WindowDrawer
    {
        public static void Draw<T>(IEnumerable<T> selectables,Curve3D curve) where T : class, ISelectEditable<T>
        {
            List<SelectableGUID> selectedPoints = curve.selectedPoints;
            if (selectedPoints.Count == 0)
                return;
            T primaryPoint=default;
            for (int i = 0; i < selectedPoints.Count && primaryPoint==default; i++)
                foreach (var j in selectables)
                    if (j.GUID == selectedPoints[i] && j.IsInsideVisibleCurve(curve.positionCurve))
                    {
                        primaryPoint = j;
                        break;
                    }
            if (primaryPoint == default)
                return;
            EditorGUI.BeginChangeCheck();
            List<T> selected = new List<T>();
            foreach (var j in selectables)
                if (selectedPoints.Contains(j.GUID))
                    selected.Add(j);
            primaryPoint.SelectEdit(curve, selected);
            if (EditorGUI.EndChangeCheck())
            {
                curve.RequestMeshUpdate();
            }
        }
    }
}
#endif
