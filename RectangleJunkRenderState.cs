using System;
using System.Collections.Generic;
using HarmonyLib;
using ShipyardExpansion.Scripts;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnstayedJunkSailMast
{
    internal static class RectangleJunkRenderState
    {
        private const string MainTextureProperty = "_MainTex";
        private const string ColorProperty = "_Color";
        private const string RenderTypeTag = "RenderType";

        private static RendererState clothState;
        private static RendererState furledState;
        private static bool correctionLogged;

        internal static void CaptureAndApply(
            Renderer targetCloth,
            Renderer sourceCloth,
            Renderer targetFurled,
            Renderer sourceFurled)
        {
            clothState = RendererState.Capture(sourceCloth);
            furledState = RendererState.Capture(sourceFurled);
            clothState.ApplyShared(targetCloth);
            furledState.ApplyShared(targetFurled);
        }

        internal static void Normalize(GameObject sailObject)
        {
            if (clothState == null || furledState == null ||
                !RectangleJunkSails.IsRectangle(sailObject))
            {
                return;
            }

            Sail sail = sailObject.GetComponent<Sail>();
            Renderer cloth = sail != null && sail.cloth != null
                ? sail.cloth.GetComponent<Renderer>()
                : null;
            ReefEffectAnimUniversal reef =
                sailObject.GetComponentInChildren<
                    ReefEffectAnimUniversal>(true);
            Renderer furled = reef != null ? reef.furledSail : null;
            if (cloth == null || furled == null)
            {
                return;
            }

            bool corrected = clothState.ApplyInstances(cloth);
            corrected |= furledState.ApplyInstances(furled);
            if (corrected && !correctionLogged)
            {
                correctionLogged = true;
                Plugin.LogSource?.LogWarning(
                    "Corrected Rectangle Junk renderer state after a " +
                    "runtime material change so Sailwind fog can use the " +
                    "stock Junk Square shader and depth settings.");
            }
        }

        internal static void Reset()
        {
            clothState = null;
            furledState = null;
            correctionLogged = false;
        }

        private sealed class RendererState
        {
            private readonly Material[] materials;
            private readonly ShadowCastingMode shadowCastingMode;
            private readonly bool receiveShadows;
            private readonly LightProbeUsage lightProbeUsage;
            private readonly ReflectionProbeUsage reflectionProbeUsage;
            private readonly MotionVectorGenerationMode motionVectors;
            private readonly bool allowOcclusionWhenDynamic;
            private readonly int sortingLayerId;
            private readonly int sortingOrder;
            private readonly int layer;

            private RendererState(Renderer source)
            {
                materials = source.sharedMaterials;
                shadowCastingMode = source.shadowCastingMode;
                receiveShadows = source.receiveShadows;
                lightProbeUsage = source.lightProbeUsage;
                reflectionProbeUsage = source.reflectionProbeUsage;
                motionVectors = source.motionVectorGenerationMode;
                allowOcclusionWhenDynamic =
                    source.allowOcclusionWhenDynamic;
                sortingLayerId = source.sortingLayerID;
                sortingOrder = source.sortingOrder;
                layer = source.gameObject.layer;
            }

            internal static RendererState Capture(Renderer source)
            {
                if (source == null ||
                    source.sharedMaterials.Length == 0 ||
                    source.sharedMaterials[0] == null)
                {
                    throw new InvalidOperationException(
                        "the live Junk Square renderer state is incomplete");
                }

                return new RendererState(source);
            }

            internal void ApplyShared(Renderer target)
            {
                target.sharedMaterials = materials;
                ApplyRendererSettings(target);
            }

            internal bool ApplyInstances(Renderer target)
            {
                bool corrected = RendererSettingsDiffer(target);
                Material[] instances = target.materials;
                for (int i = 0; i < instances.Length; i++)
                {
                    Material targetMaterial = instances[i];
                    Material sourceMaterial = materials[
                        Math.Min(i, materials.Length - 1)];
                    if (targetMaterial == null || sourceMaterial == null)
                    {
                        continue;
                    }

                    corrected |= MaterialStateDiffers(
                        targetMaterial,
                        sourceMaterial);
                    ApplyMaterialState(targetMaterial, sourceMaterial);
                }

                target.materials = instances;
                ApplyRendererSettings(target);
                return corrected;
            }

            private void ApplyRendererSettings(Renderer target)
            {
                target.shadowCastingMode = shadowCastingMode;
                target.receiveShadows = receiveShadows;
                target.lightProbeUsage = lightProbeUsage;
                target.reflectionProbeUsage = reflectionProbeUsage;
                target.motionVectorGenerationMode = motionVectors;
                target.allowOcclusionWhenDynamic =
                    allowOcclusionWhenDynamic;
                target.sortingLayerID = sortingLayerId;
                target.sortingOrder = sortingOrder;
                target.gameObject.layer = layer;
            }

            private bool RendererSettingsDiffer(Renderer target)
            {
                return target.shadowCastingMode != shadowCastingMode ||
                       target.receiveShadows != receiveShadows ||
                       target.lightProbeUsage != lightProbeUsage ||
                       target.reflectionProbeUsage !=
                       reflectionProbeUsage ||
                       target.motionVectorGenerationMode != motionVectors ||
                       target.allowOcclusionWhenDynamic !=
                       allowOcclusionWhenDynamic ||
                       target.sortingLayerID != sortingLayerId ||
                       target.sortingOrder != sortingOrder ||
                       target.gameObject.layer != layer;
            }

            private static bool MaterialStateDiffers(
                Material target,
                Material source)
            {
                if (target.shader != source.shader ||
                    target.renderQueue != source.renderQueue ||
                    target.enableInstancing != source.enableInstancing ||
                    target.doubleSidedGI != source.doubleSidedGI ||
                    target.GetTag(RenderTypeTag, false, string.Empty) !=
                    source.GetTag(RenderTypeTag, false, string.Empty) ||
                    !KeywordsMatch(
                        target.shaderKeywords,
                        source.shaderKeywords))
                {
                    return true;
                }

                return FloatPropertyDiffers(target, source, "_ZWrite") ||
                       FloatPropertyDiffers(target, source, "_SrcBlend") ||
                       FloatPropertyDiffers(target, source, "_DstBlend") ||
                       FloatPropertyDiffers(target, source, "_Mode");
            }

            private static void ApplyMaterialState(
                Material target,
                Material source)
            {
                bool hasTexture = target.HasProperty(MainTextureProperty);
                Texture texture = hasTexture ? target.mainTexture : null;
                Vector2 textureScale = hasTexture
                    ? target.mainTextureScale
                    : Vector2.one;
                Vector2 textureOffset = hasTexture
                    ? target.mainTextureOffset
                    : Vector2.zero;
                bool hasColor = target.HasProperty(ColorProperty);
                Color color = hasColor
                    ? target.GetColor(ColorProperty)
                    : Color.white;

                target.shader = source.shader;
                target.CopyPropertiesFromMaterial(source);
                target.shaderKeywords = source.shaderKeywords;
                target.renderQueue = source.renderQueue;
                target.enableInstancing = source.enableInstancing;
                target.doubleSidedGI = source.doubleSidedGI;
                target.globalIlluminationFlags =
                    source.globalIlluminationFlags;
                target.SetOverrideTag(
                    RenderTypeTag,
                    source.GetTag(RenderTypeTag, false, string.Empty));

                if (hasTexture && target.HasProperty(MainTextureProperty))
                {
                    target.mainTexture = texture;
                    target.mainTextureScale = textureScale;
                    target.mainTextureOffset = textureOffset;
                }

                if (hasColor && target.HasProperty(ColorProperty))
                {
                    target.SetColor(ColorProperty, color);
                }
            }

            private static bool FloatPropertyDiffers(
                Material target,
                Material source,
                string propertyName)
            {
                if (!target.HasProperty(propertyName) ||
                    !source.HasProperty(propertyName))
                {
                    return false;
                }

                return !Mathf.Approximately(
                    target.GetFloat(propertyName),
                    source.GetFloat(propertyName));
            }

            private static bool KeywordsMatch(
                string[] first,
                string[] second)
            {
                first = first ?? Array.Empty<string>();
                second = second ?? Array.Empty<string>();
                if (first.Length != second.Length)
                {
                    return false;
                }

                HashSet<string> expected = new HashSet<string>(second);
                for (int i = 0; i < first.Length; i++)
                {
                    if (!expected.Contains(first[i]))
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }

    [HarmonyPatch(typeof(Sail), "Start")]
    [HarmonyPriority(Priority.Last)]
    internal static class RectangleJunkSailRenderStartPatch
    {
        private static void Postfix(Sail __instance)
        {
            RectangleJunkRenderState.Normalize(__instance.gameObject);
        }
    }

    [HarmonyPatch(typeof(Sail), "ChangeSailColor")]
    [HarmonyPriority(Priority.Last)]
    internal static class RectangleJunkSailColorRenderPatch
    {
        private static void Postfix(Sail __instance)
        {
            RectangleJunkRenderState.Normalize(__instance.gameObject);
        }
    }

    [HarmonyPatch(typeof(SailTextureChanger), "UpdateMaterial")]
    [HarmonyAfter(Plugin.ShipyardExpansionGuid)]
    [HarmonyPriority(Priority.Last)]
    internal static class RectangleJunkSailTextureRenderPatch
    {
        private static void Postfix(SailTextureChanger __instance)
        {
            RectangleJunkRenderState.Normalize(__instance.gameObject);
        }
    }
}
