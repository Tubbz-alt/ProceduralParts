﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSPAPIExtensions;

namespace ProceduralParts
{

    public class ProceduralShapeCone : ProceduralAbstractSoRShape
    {
        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Top", guiFormat = "F3"),
         UI_FloatEdit(scene = UI_Scene.Editor, minValue = 0.25f, maxValue = 10.0f, incrementLarge = 1.25f, incrementSmall = 0.25f, incrementSlide = 0.001f)]
        public float topDiameter = 1.25f;
        protected float oldTopDiameter;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Bottom", guiFormat = "F3"),
         UI_FloatEdit(scene = UI_Scene.Editor, incrementSlide = 0.001f)]
        public float bottomDiameter = 1.25f;
        protected float oldBottomDiameter;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Length", guiFormat = "F3"),
         UI_FloatEdit(scene = UI_Scene.Editor, incrementSlide = 0.001f)]
        public float length = 1f;
        protected float oldLength;

        public ConeTopMode coneTopMode = ConeTopMode.CanZero;

        public enum ConeTopMode
        {
            CanZero, LimitMin, Constant,
        }

        private UI_FloatEdit topDiameterEdit;

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            try
            {
                coneTopMode = (ConeTopMode)Enum.Parse(typeof(ConeTopMode), node.GetValue("coneTopMode"), true);
            }
            catch { }
        }

        public override void OnStart(StartState state)
        {
            if (!HighLogic.LoadedSceneIsEditor)
                return;

            if (pPart.lengthMin == pPart.lengthMax)
                Fields["length"].guiActiveEditor = false;
            else
            {
                UI_FloatEdit lengthEdit = (UI_FloatEdit)Fields["length"].uiControlEditor;
                lengthEdit.maxValue = pPart.lengthMax;
                lengthEdit.minValue = pPart.lengthMin;
                lengthEdit.incrementLarge = pPart.lengthLargeStep;
                lengthEdit.incrementSmall = pPart.lengthSmallStep;
            }

            if (pPart.diameterMin == pPart.diameterMax)
            {
                Fields["topDiameter"].guiActiveEditor = false;
                Fields["bottomDiameter"].guiActiveEditor = false;
                return;
            }

            Debug.LogWarning("coneTopMode=" + coneTopMode);

            UI_FloatEdit bottomDiameterEdit = (UI_FloatEdit)Fields["bottomDiameter"].uiControlEditor;
            bottomDiameterEdit.incrementLarge = pPart.diameterLargeStep;
            bottomDiameterEdit.incrementSmall = pPart.diameterSmallStep;
            bottomDiameterEdit.minValue = pPart.diameterMin;
            bottomDiameterEdit.maxValue = pPart.diameterMax;

            if (coneTopMode == ConeTopMode.Constant)
            {
                // Fixed aspect - deactivate the top diameter
                Fields["topDiameter"].guiActiveEditor = false;
                Fields["bottomDiameter"].guiName = "Diameter";
            }
            else
            {
                topDiameterEdit = (UI_FloatEdit)Fields["topDiameter"].uiControlEditor;
                topDiameterEdit.incrementLarge = pPart.diameterLargeStep;
                topDiameterEdit.incrementSmall = pPart.diameterSmallStep;
                topDiameterEdit.minValue = (coneTopMode == ConeTopMode.CanZero) ? 0 : pPart.diameterMin;
                topDiameterEdit.maxValue = bottomDiameter;
            }
        }

        protected void MaintainApectRatio()
        {
            if (pPart.aspectMin == 0 && float.IsPositiveInfinity(pPart.aspectMax))
                return;

            float aspect = (bottomDiameter - topDiameter) / length;

            if (!MathUtils.TestClamp(ref aspect, pPart.aspectMin, pPart.aspectMax))
                return;

            // The bottom can push the top, otherwise its fixed.
            if (bottomDiameter != oldBottomDiameter)
            {
                try
                {
                    length = MathUtils.RoundTo((bottomDiameter - topDiameter) / aspect, 0.001f);

                    if (!MathUtils.TestClamp(ref length, pPart.lengthMin, pPart.lengthMax))
                        return;

                    // Bottom has gone out of range, push back.
                    bottomDiameter = MathUtils.RoundTo(aspect * length + topDiameter, 0.001f);
                }
                finally
                {
                    oldLength = length;
                }
            }
            else if (length != oldLength)
            {
                // Reset the length back in range.
                length = MathUtils.RoundTo((bottomDiameter - topDiameter) / aspect, 0.001f);
            }
            else if (topDiameter != oldTopDiameter)
            {
                // Just reset top diameter to extremum. 
                topDiameter = MathUtils.RoundTo(aspect * length + bottomDiameter, 0.001f);
            }
        }

        protected void UpdateTopDiameterLimit()
        {
            if (topDiameterEdit == null)
                return;

            topDiameterEdit.maxValue = bottomDiameter;
        }

        protected override void UpdateShape(bool force)
        {
            if (!force && oldTopDiameter == topDiameter && oldBottomDiameter == bottomDiameter && oldLength == length)
                return;

            //Debug.LogWarning("Cone.UpdateShape");

            // Maxmin the volume.
            if (HighLogic.LoadedSceneIsEditor)
            {
                MaintainApectRatio();

                float volume = (Mathf.PI * length * (topDiameter * topDiameter + topDiameter * bottomDiameter + bottomDiameter * bottomDiameter)) / 12f;

                if (MathUtils.TestClamp(ref volume, pPart.volumeMin, pPart.volumeMax))
                {
                    if (oldLength != length)
                        length = volume * 12f / (Mathf.PI * (topDiameter * topDiameter + topDiameter * bottomDiameter + bottomDiameter * bottomDiameter));
                    else if (oldTopDiameter != topDiameter)
                    {
                        // this becomes solving the quadratic on topDiameter
                        float a = length * Mathf.PI;
                        float b = length * Mathf.PI * bottomDiameter;
                        float c = length * Mathf.PI * bottomDiameter * bottomDiameter - volume * 12f;

                        float det = Mathf.Sqrt(b * b - 4 * a * c);
                        topDiameter = (det - b) / (2f * a);
                    }
                    else
                    {
                        // this becomes solving the quadratic on bottomDiameter
                        float a = length * Mathf.PI;
                        float b = length * Mathf.PI * topDiameter;
                        float c = length * Mathf.PI * topDiameter * topDiameter - volume * 12f;

                        float det = Mathf.Sqrt(b * b - 4 * a * c);
                        bottomDiameter = (det - b) / (2f * a);
                    }
                }
                this.volume = volume;

                UpdateTopDiameterLimit();
            }
            else
            {
                volume = (Mathf.PI * length * (topDiameter * topDiameter + topDiameter * bottomDiameter + bottomDiameter * bottomDiameter)) / 12f;
            }

            // Perpendicular.
            Vector2 norm = new Vector2(length, (bottomDiameter - topDiameter) / 2f);
            norm.Normalize();

            WriteMeshes(
                new ProfilePoint(bottomDiameter, -0.5f * length, 0f, norm),
                new ProfilePoint(topDiameter, 0.5f * length, 1f, norm)
                );

            oldTopDiameter = topDiameter;
            oldBottomDiameter = bottomDiameter;
            oldLength = length;
        }
    }

}