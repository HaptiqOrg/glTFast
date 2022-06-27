using System;
using GLTFast.Materials;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace GLTFast.Export
{
    using Schema;

    /// <summary>
    /// Still WIP
    /// Class for exporting gltf materials instead of the default Standard material
    /// </summary>
    public class GltfMaterialExporter : MaterialExportBase
    {
        static readonly int k_roughness = Shader.PropertyToID("_Roughness");
        static readonly int k_metallic = Shader.PropertyToID("_Metallic");

        const string k_KeywordEmission = "_EMISSION";
        static readonly int k_EmissionColor = Shader.PropertyToID("_EmissionColor");
        static readonly int k_EmissionMap = Shader.PropertyToID("_EmissionMap");

        private void ExportBaseColor(ref UnityEngine.Material uMaterial, ref Schema.Material material, IGltfWritable gltf)
        {
            var mainTex = uMaterial.GetTexture(k_MainTex);
            if (mainTex != null)
            {
                material.pbrMetallicRoughness.baseColorTexture = ExportTextureInfo(mainTex, gltf);
                ExportTextureTransformNoFlip(material.pbrMetallicRoughness.baseColorTexture, uMaterial, k_MainTex, gltf);
            }
        }

        protected static void ExportTextureTransformNoFlip(TextureInfo def, UnityEngine.Material mat, int texPropertyId, IGltfWritable gltf)
        {
            var offset = mat.GetTextureOffset(texPropertyId);
            var scale = mat.GetTextureScale(texPropertyId);

            if (offset != Vector2.zero || scale != Vector2.one)
            {
                gltf.RegisterExtensionUsage(Extension.TextureTransform);
                def.extensions = def.extensions ?? new TextureInfoExtension();
                def.extensions.KHR_texture_transform = new TextureTransform
                {
                    scale = new[] { scale.x, scale.y },
                    offset = new[] { offset.x, offset.y }
                };
            }
        }

        public override bool ConvertMaterial(UnityEngine.Material uMaterial, out Schema.Material material, IGltfWritable gltf, ICodeLogger logger)
        {
            var success = true;
            material = new Schema.Material
            {
                name = uMaterial.name,
                pbrMetallicRoughness = new PbrMetallicRoughness
                {
                    metallicFactor = uMaterial.GetFloat(k_metallic),
                    roughnessFactor = uMaterial.GetFloat(k_roughness),
                    baseColor = uMaterial.GetColor(k_Color)
                }
            };

            SetAlphaModeAndCutoff(uMaterial, material);
            material.doubleSided = IsDoubleSided(uMaterial);

            ExportBaseColor(ref uMaterial, ref material, gltf);


            if (uMaterial.IsKeywordEnabled(k_KeywordEmission))
            {
                if (uMaterial.HasProperty(k_EmissionColor))
                {
                    var emissionColor = uMaterial.GetColor(k_EmissionColor);

                    // Clamp emissionColor to 0..1
                    var maxFactor = math.max(emissionColor.r, math.max(emissionColor.g, emissionColor.b));
                    if (maxFactor > 1f)
                    {
                        emissionColor.r /= maxFactor;
                        emissionColor.g /= maxFactor;
                        emissionColor.b /= maxFactor;
                        // TODO: use maxFactor as emissiveStrength (KHR_materials_emissive_strength)
                    }

                    material.emissive = emissionColor;
                }

                if (uMaterial.HasProperty(k_EmissionMap))
                {
                    var emissionTex = uMaterial.GetTexture(k_EmissionMap);

                    if (emissionTex != null)
                    {
                        if (emissionTex is Texture2D)
                        {
                            material.emissiveTexture = ExportTextureInfo(emissionTex, gltf);
                            ExportTextureTransform(material.emissiveTexture, uMaterial, k_EmissionMap, gltf);
                        }
                        else
                        {
                            logger?.Error(LogCode.TextureInvalidType, "emission", material.name);
                            success = false;
                        }
                    }
                }
            }

            return success;
        }
    }
}