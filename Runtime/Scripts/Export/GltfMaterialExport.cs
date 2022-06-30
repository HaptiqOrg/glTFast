using Unity.Mathematics;
using UnityEngine;

namespace GLTFast.Export
{
    using Logging;
    using Schema;

    /// <summary>
    /// Still WIP
    /// Class for exporting gltf materials instead of the default Standard material
    /// </summary>
    public class GltfMaterialExporter : MaterialExportBase
    {
        const string k_KeywordEmission = "_EMISSION";
        const string k_KeywordBumpMap = "_BUMPMAP";

        static readonly int k_BumpMap = Shader.PropertyToID("_BumpMap");
        static readonly int k_roughness = Shader.PropertyToID("_Roughness");
        static readonly int k_metallic = Shader.PropertyToID("_Metallic");
        static readonly int k_EmissionColor = Shader.PropertyToID("_EmissionColor");
        static readonly int k_EmissionMap = Shader.PropertyToID("_EmissionMap");
        static readonly int k_MetallicGlossMap = Shader.PropertyToID("_MetallicGlossMap");
        static readonly int k_OcclusionMap = Shader.PropertyToID("_OcclusionMap");
        static readonly int k_OcclusionStrength = Shader.PropertyToID("_OcclusionStrength");

        static readonly int k_BaseColorUVSet = Shader.PropertyToID("_MainTexUVChannel");
        static readonly int k_BumpMapUVSet = Shader.PropertyToID("_BumpMapUVChannel");
        static readonly int k_MetallicGlossUVSet = Shader.PropertyToID("_MetallicGlossMapUVChannel");
        static readonly int k_EmissionUVSet = Shader.PropertyToID("_EmissionMapUVChannel");
        static readonly int k_OcclusionUVSet = Shader.PropertyToID("_OcclusionMapUVChannel");

        public override bool ConvertMaterial(UnityEngine.Material unityMaterial, out Schema.Material gltfMaterial, IGltfWritable gltf, ICodeLogger logger)
        {
            var success = true;
            gltfMaterial = new Schema.Material
            {
                name = unityMaterial.name,
                pbrMetallicRoughness = new PbrMetallicRoughness
                {
                    metallicFactor = unityMaterial.GetFloat(k_metallic),
                    roughnessFactor = unityMaterial.GetFloat(k_roughness),
                    baseColor = unityMaterial.GetColor(k_Color)
                }
            };

            SetAlphaModeAndCutoff(unityMaterial, gltfMaterial);
            gltfMaterial.doubleSided = IsDoubleSided(unityMaterial);

            success &= ExportBaseColor(unityMaterial, gltfMaterial, gltf, logger);
            success &= ExportEmissive(unityMaterial, gltfMaterial, gltf, logger);
            success &= ExportNormal(unityMaterial, gltfMaterial, gltf, logger);
            success &= ExportOcclusionRoughnessMetallic(unityMaterial, gltfMaterial, gltf, logger);


            return success;
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

        private bool ExportBaseColor(UnityEngine.Material unityMaterial, Schema.Material material, IGltfWritable gltf, ICodeLogger logger)
        {
            bool success = true;
            var mainTex = unityMaterial.GetTexture(k_MainTex);
            if (mainTex != null)
            {
                if (mainTex is Texture2D)
                {
                    material.pbrMetallicRoughness.baseColorTexture = ExportTextureInfo(mainTex, gltf);
                    material.pbrMetallicRoughness.baseColorTexture.texCoord = (int)unityMaterial.GetFloat(k_BaseColorUVSet);
                    ExportTextureTransformNoFlip(material.pbrMetallicRoughness.baseColorTexture, unityMaterial, k_MainTex, gltf);
                }
                else
                {
                    logger?.Error(LogCode.TextureInvalidType, "baseColor", material.name);
                    success = false;
                }
                
            }
            return success;
        }

        private bool ExportEmissive(UnityEngine.Material unityMaterial, Schema.Material material, IGltfWritable gltf, ICodeLogger logger)
        {
            bool success = true;
            if (unityMaterial.IsKeywordEnabled(k_KeywordEmission))
            {
                if (unityMaterial.HasProperty(k_EmissionColor))
                {
                    var emissionColor = unityMaterial.GetColor(k_EmissionColor);

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

                if (unityMaterial.HasProperty(k_EmissionMap))
                {
                    var emissionTex = unityMaterial.GetTexture(k_EmissionMap);

                    if (emissionTex != null)
                    {
                        if (emissionTex is Texture2D)
                        {
                            material.emissiveTexture = ExportTextureInfo(emissionTex, gltf);
                            material.emissiveTexture.texCoord = (int)unityMaterial.GetFloat(k_EmissionUVSet);
                            ExportTextureTransformNoFlip(material.emissiveTexture, unityMaterial, k_EmissionMap, gltf);
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

        private bool ExportNormal(UnityEngine.Material unityMaterial, Schema.Material material, IGltfWritable gltf, ICodeLogger logger)
        {
            bool success = true;
            if (unityMaterial.HasProperty(k_BumpMap)
                && (unityMaterial.IsKeywordEnabled(Materials.Constants.kwNormalMap)
                || unityMaterial.IsKeywordEnabled(k_KeywordBumpMap)))
            {
                var normalTex = unityMaterial.GetTexture(k_BumpMap);

                if (normalTex != null)
                {
                    if (normalTex is Texture2D)
                    {
                        material.normalTexture = ExportNormalTextureInfo(normalTex, unityMaterial, gltf);
                        material.normalTexture.texCoord = (int)unityMaterial.GetFloat(k_BumpMapUVSet);
                        ExportTextureTransformNoFlip(material.normalTexture, unityMaterial, k_BumpMap, gltf);
                    }
                    else
                    {
                        logger?.Error(LogCode.TextureInvalidType, "normal", unityMaterial.name);
                        success = false;
                    }
                }
            }
            return success;
        }

        private bool ExportOcclusionRoughnessMetallic(UnityEngine.Material unityMaterial, Schema.Material gltfMaterial, IGltfWritable gltf, ICodeLogger logger)
        {
            bool success = true;
            var metallicRoughnessTex = unityMaterial.GetTexture(k_MetallicGlossMap);
            if (metallicRoughnessTex != null)
            {
                if (metallicRoughnessTex is Texture2D)
                {
                    gltfMaterial.pbrMetallicRoughness.metallicRoughnessTexture = ExportTextureInfo(metallicRoughnessTex, gltf);
                    gltfMaterial.pbrMetallicRoughness.metallicRoughnessTexture.texCoord = (int)unityMaterial.GetFloat(k_MetallicGlossUVSet);
                    ExportTextureTransformNoFlip(gltfMaterial.pbrMetallicRoughness.metallicRoughnessTexture, unityMaterial, k_MetallicGlossMap, gltf);
                }
                else
                {
                    logger?.Error(LogCode.TextureInvalidType, "metallicRoughness", unityMaterial.name);
                    success = false;
                }
            }

            var occlusionTex = unityMaterial.GetTexture(k_OcclusionMap);
            if (occlusionTex != null)
            {
                if (occlusionTex is Texture2D)
                {
                    int occlusionTextureID = -1;
                    if (occlusionTex == metallicRoughnessTex)
                    {
                        occlusionTextureID = gltfMaterial.pbrMetallicRoughness.metallicRoughnessTexture.index;
                    }
                    else
                    {
                        var imageExport = new ImageExport(occlusionTex as Texture2D);
                        AddImageExport(gltf, imageExport, out occlusionTextureID);
                    }

                    gltfMaterial.occlusionTexture = new OcclusionTextureInfo {
                        index = occlusionTextureID,
                        strength = unityMaterial.GetFloat(k_OcclusionStrength),
                        texCoord = (int)unityMaterial.GetFloat(k_OcclusionUVSet)
                    };
                    
                    ExportTextureTransformNoFlip(gltfMaterial.occlusionTexture, unityMaterial, k_OcclusionMap, gltf);
                }
                else
                {
                    logger?.Error(LogCode.TextureInvalidType, "occlusion", gltfMaterial.name);
                    success = false;
                }
            }

            return success;
        }
    }
}