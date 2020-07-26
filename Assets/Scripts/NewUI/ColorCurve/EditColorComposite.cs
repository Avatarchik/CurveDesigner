﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Assets.NewUI
{
    public class EditColorComposite : IClickable
    {
        public PointAlongCurveComposite centerPoint;
        private ColorSamplerPoint _point;
        private Curve3D _curve;
        private EditColorClickCommand clickCommand;
        public override SelectableGUID GUID => _point.GUID;
        public EditColorComposite(IComposite parent,ColorSamplerPoint point,ColorDistanceSampler sampler,Color color,PositionCurveComposite positionCurveComposite,Curve3D curve) : base(parent)
        {
            _curve = curve;
            _point = point;
            clickCommand = new EditColorClickCommand(this);
            centerPoint = new PointAlongCurveComposite(this,point,positionCurveComposite,color,point.GUID,sampler);
        }
        public override IEnumerable<IComposite> GetChildren()
        {
            yield return centerPoint;
        }
        public override IClickCommand GetClickCommand()
        {
            return clickCommand;
        }
        public override SelectableGUID Guid => _point.GUID;
        public override void Draw(List<IDraw> drawList, ClickHitData closestElementToCursor)
        {
            drawList.Add(new EditColorDraw(this));
            base.Draw(drawList, closestElementToCursor);
        }
        public override void Click(Vector2 mousePosition, List<ClickHitData> clickHits,EventType eventType)
        {
            GUITools.WorldToGUISpace(centerPoint.Position,out Vector2 guiPosition,out float screenDepth);
            float distance = Vector2.Distance(mousePosition,guiPosition);
            clickHits.Add(new ClickHitData(this,distance,screenDepth,guiPosition-mousePosition));
            base.Click(mousePosition, clickHits,eventType);
        }
        public void IMGUIElement(EventType eventType)
        {
            if (eventType == EventType.ScrollWheel)
                return;
            if (GUITools.WorldToGUISpace(centerPoint.Position, out Vector2 guiPos, out float distFromCamera))
            {
                var colorRect = new Rect(guiPos.x, guiPos.y, 28, 28);
                Handles.BeginGUI();
                GUILayout.BeginArea(colorRect, _curve.settings.colorPickerBoxStyle);
                void WrapUp()
                {
                    GUILayout.EndArea();
                    MouseEater.EatMouseInput(colorRect);
                    Handles.EndGUI();
                }
                try
                {
                    _point.value = EditorGUILayout.ColorField(GUIContent.none, _point.value, showEyedropper: false, showAlpha: true, hdr: false);
                }
                catch (ExitGUIException e) {
                    WrapUp();
                    throw e;
                }
                WrapUp();
            }
        }
    }
    public interface ILayout
    {
        void Layout();
    }
    public class EditColorClickCommand : IClickCommand
    {
        private EditColorComposite creator;
        public EditColorClickCommand(EditColorComposite creator)
        {
            this.creator = creator;


        }
        public void ClickDown(Vector2 mousePos, Curve3D curve, List<SelectableGUID> selected)
        {
            creator.IMGUIElement(EventType.MouseDown);
        }

        public void ClickDrag(Vector2 mousePos, Curve3D curve, ClickHitData clicked, List<SelectableGUID> selected)
        {
            creator.IMGUIElement(EventType.MouseDrag);
        }

        public void ClickUp(Vector2 mousePos, Curve3D curve, List<SelectableGUID> selected)
        {
            creator.IMGUIElement(EventType.MouseUp);
        }
    }
    public class EditColorDraw : IDraw, ILayout
    {
        private EditColorComposite creator;
        private float _distFromCamera;
        public EditColorDraw(EditColorComposite creator)
        {
            this.creator = creator;
            GUITools.WorldToGUISpace(creator.centerPoint.Position, out Vector2 _guiPos, out _distFromCamera);
        }
        public IComposite Creator()
        {
            return creator;
        }

        public float DistFromCamera()
        {
            return _distFromCamera;
        }

        public void Draw(DrawMode mode, SelectionState selectionState)
        {
            creator.IMGUIElement(EventType.Repaint);
        }
        public void Layout()
        {
            creator.IMGUIElement(EventType.Layout);
        }
    }
}
