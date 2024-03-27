using Rhino.FileIO;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace Export_glTF
{
  enum RgbaChannel
  {
    Red = 0,
    Green = 1,
    Blue = 2,
    Alpha = 3,
  }

  class RhinoMaterialGltfConverter
  {
    public RhinoMaterialGltfConverter(FileGltfWriteOptions options, bool binary, gltfSchemaDummy dummy, List<byte> binaryBuffer, MeshMaterialPair pair, Dictionary<int, int> mappingToGltfTexCoord, Rhino.Render.LinearWorkflow workflow)
    {
      this.options = options;
      this.binary = binary;
      this.dummy = dummy;
      this.binaryBuffer = binaryBuffer;
      this.pair = pair;
      this.workflow = workflow;
      this.mappingToGltfTexCoord = mappingToGltfTexCoord;
    }

    private FileGltfWriteOptions options = null;
    private bool binary = false;
    private gltfSchemaDummy dummy = null;
    private List<byte> binaryBuffer = null;
    private Rhino.Render.LinearWorkflow workflow = null;

    private MeshMaterialPair pair = null;

    private Dictionary<int, int> mappingToGltfTexCoord = null;

    public int AddMaterial()
    {
      // Prep
      glTFLoader.Schema.Material material = new glTFLoader.Schema.Material()
      {
        Name = pair.RenderMaterial.Name,
        PbrMetallicRoughness = new glTFLoader.Schema.MaterialPbrMetallicRoughness(),
        DoubleSided = !options.CullBackfaces,
        Extensions = new Dictionary<string, object>(),
      };

      // Textures
      Rhino.DocObjects.Texture metallicTexture = pair.PBR.GetTexture(Rhino.DocObjects.TextureType.PBR_Metallic);
      Rhino.DocObjects.Texture roughnessTexture = pair.PBR.GetTexture(Rhino.DocObjects.TextureType.PBR_Roughness);
      Rhino.DocObjects.Texture normalTexture = pair.PBR.GetTexture(Rhino.DocObjects.TextureType.Bump);
      Rhino.DocObjects.Texture occlusionTexture = pair.PBR.GetTexture(Rhino.DocObjects.TextureType.PBR_AmbientOcclusion);
      Rhino.DocObjects.Texture emissiveTexture = pair.PBR.GetTexture(Rhino.DocObjects.TextureType.PBR_Emission);
      Rhino.DocObjects.Texture opacityTexture = pair.PBR.GetTexture(Rhino.DocObjects.TextureType.Opacity);
      Rhino.DocObjects.Texture clearcoatTexture = pair.PBR.GetTexture(Rhino.DocObjects.TextureType.PBR_Clearcoat);
      Rhino.DocObjects.Texture clearcoatRoughessTexture = pair.PBR.GetTexture(Rhino.DocObjects.TextureType.PBR_ClearcoatRoughness);
      Rhino.DocObjects.Texture clearcoatNormalTexture = pair.PBR.GetTexture(Rhino.DocObjects.TextureType.PBR_ClearcoatBump);
      Rhino.DocObjects.Texture specularTexture = pair.PBR.GetTexture(Rhino.DocObjects.TextureType.PBR_Specular);

      HandleBaseColor(pair.Material, material);

      bool hasMetalTexture = metallicTexture == null ? false : metallicTexture.Enabled;
      bool hasRoughnessTexture = roughnessTexture == null ? false : roughnessTexture.Enabled;

      if (hasMetalTexture || hasRoughnessTexture)
      {
        material.PbrMetallicRoughness.MetallicRoughnessTexture = GetMetallicRoughnessTextureInfo(pair.Material);

        float metallic = metallicTexture == null ? (float)pair.PBR.Metallic : GetTextureWeight(metallicTexture);
        float roughness = roughnessTexture == null ? (float)pair.PBR.Roughness : GetTextureWeight(roughnessTexture);

        material.PbrMetallicRoughness.MetallicFactor = metallic;
        material.PbrMetallicRoughness.RoughnessFactor = roughness;
      }
      else
      {
        material.PbrMetallicRoughness.MetallicFactor = (float)pair.PBR.Metallic;
        material.PbrMetallicRoughness.RoughnessFactor = (float)pair.PBR.Roughness;
      }

      if (normalTexture != null && normalTexture.Enabled)
      {
        material.NormalTexture = GetNormalTextureInfo(normalTexture);
      }

      if (occlusionTexture != null && occlusionTexture.Enabled)
      {
        material.OcclusionTexture = GetOcclusionTextureInfo(occlusionTexture);
      }

      //Emission

      if (emissiveTexture != null && emissiveTexture.Enabled)
      {
        material.EmissiveTexture = GetTextureInfo(emissiveTexture);

        float emissionMultiplier = 1.0f;

        var param = pair.RenderMaterial.GetParameter("emission-multiplier");

        if (param != null)
        {
          emissionMultiplier = (float)Convert.ToDouble(param);
        }

        glTFExtensions.KHR_materials_emissive_strength emissiveStrength = new glTFExtensions.KHR_materials_emissive_strength();

        emissiveStrength.EmissiveStrength = emissionMultiplier;

        material.Extensions.Add(glTFExtensions.KHR_materials_emissive_strength.Tag, emissiveStrength);
      }
      else
      {
        Rhino.Display.Color4f emissionColor = pair.PBR.Emission;

        if(emissionColor.R > 1.0f || emissionColor.G > 1.0f || emissionColor.B > 1.0f)
        {
          //The emission color is a system drawing color so always [0-1.0] with the multiplier unapplied
          //So we can get that multiplier back by dividing
          Rhino.Display.Color4f original = new Rhino.Display.Color4f(pair.Material.EmissionColor);

          float intensity = emissionColor.R / original.R;

          glTFExtensions.KHR_materials_emissive_strength emissiveStrength = new glTFExtensions.KHR_materials_emissive_strength();

          emissiveStrength.EmissiveStrength = intensity;

          material.Extensions.Add(glTFExtensions.KHR_materials_emissive_strength.Tag, emissiveStrength);

          emissionColor = new Rhino.Display.Color4f(
            GlTFUtils.Clampf(emissionColor.R / intensity, 0.0f, 1.0f),
            GlTFUtils.Clampf(emissionColor.G / intensity, 0.0f, 1.0f),
            GlTFUtils.Clampf(emissionColor.B / intensity, 0.0f, 1.0f),
            1.0f
          );
        }

        material.EmissiveFactor = new float[]
        {
          emissionColor.R,
          emissionColor.G,
          emissionColor.B,
        };
      }

      //Opacity => Transmission https://github.com/KhronosGroup/glTF/blob/master/extensions/2.0/Khronos/KHR_materials_transmission/README.md

      glTFExtensions.KHR_materials_transmission transmission = new glTFExtensions.KHR_materials_transmission();

      if (opacityTexture != null && opacityTexture.Enabled)
      {
        //Transmission texture is stored in an images R channel
        //https://github.com/KhronosGroup/glTF/blob/master/extensions/2.0/Khronos/KHR_materials_transmission/README.md#properties
        transmission.TransmissionTexture = GetSingleChannelTexture(opacityTexture, RgbaChannel.Red, true);
        transmission.TransmissionFactor = GetTextureWeight(opacityTexture);
      }
      else
      {
        transmission.TransmissionFactor = 1.0f - (float)pair.PBR.Opacity;
      }

      material.Extensions.Add(glTFExtensions.KHR_materials_transmission.Tag, transmission);

      //Clearcoat => Clearcoat https://github.com/KhronosGroup/glTF/blob/master/extensions/2.0/Khronos/KHR_materials_clearcoat/README.md

      glTFExtensions.KHR_materials_clearcoat clearcoat = new glTFExtensions.KHR_materials_clearcoat();

      if (clearcoatTexture != null && clearcoatTexture.Enabled)
      {
        clearcoat.ClearcoatTexture = GetTextureInfo(clearcoatTexture);
        clearcoat.ClearcoatFactor = GetTextureWeight(clearcoatTexture);
      }
      else
      {
        clearcoat.ClearcoatFactor = (float)pair.PBR.Clearcoat;
      }

      if (clearcoatRoughessTexture != null && clearcoatRoughessTexture.Enabled)
      {
        clearcoat.ClearcoatRoughnessTexture = GetTextureInfo(clearcoatRoughessTexture);
        clearcoat.ClearcoatRoughnessFactor = GetTextureWeight(clearcoatRoughessTexture);
      }
      else
      {
        clearcoat.ClearcoatRoughnessFactor = (float)pair.PBR.ClearcoatRoughness;
      }

      if (clearcoatNormalTexture != null && clearcoatNormalTexture.Enabled)
      {
        clearcoat.ClearcoatNormalTexture = GetNormalTextureInfo(clearcoatNormalTexture);
      }

      material.Extensions.Add(glTFExtensions.KHR_materials_clearcoat.Tag, clearcoat);

      //Opacity IOR -> IOR https://github.com/KhronosGroup/glTF/tree/master/extensions/2.0/Khronos/KHR_materials_ior

      glTFExtensions.KHR_materials_ior ior = new glTFExtensions.KHR_materials_ior()
      {
        Ior = (float)pair.PBR.OpacityIOR,
      };

      material.Extensions.Add(glTFExtensions.KHR_materials_ior.Tag, ior);

      //Specular -> Specular https://github.com/KhronosGroup/glTF/tree/master/extensions/2.0/Khronos/KHR_materials_specular

      glTFExtensions.KHR_materials_specular specular = new glTFExtensions.KHR_materials_specular();

      if (specularTexture != null && specularTexture.Enabled)
      {
        //Specular is stored in the textures alpha channel
        specular.SpecularTexture = GetSingleChannelTexture(specularTexture, RgbaChannel.Alpha, false);
        specular.SpecularFactor = GetTextureWeight(specularTexture);
      }
      else
      {
        specular.SpecularFactor = (float)pair.PBR.Specular;
      }

      material.Extensions.Add(glTFExtensions.KHR_materials_specular.Tag, specular);

      return dummy.Materials.AddAndReturnIndex(material);
    }

    glTFLoader.Schema.TextureInfo GetSingleChannelTexture(Rhino.DocObjects.Texture texture, RgbaChannel channel, bool invert)
    {
      string path = texture.FileReference.FullPath;

      Bitmap bmp = new Bitmap(path);

      Bitmap final = new Bitmap(bmp.Width, bmp.Height);

      for (int i = 0; i < bmp.Width; i++)
      {
        for (int j = 0; j < bmp.Height; j++)
        {
          Rhino.Display.Color4f color = new Rhino.Display.Color4f(bmp.GetPixel(i, j));

          float value = color.L;

          if (invert)
          {
            value = 1.0f - value;
          }

          Color colorFinal = GetSingleChannelColor(value, channel);

          final.SetPixel(i, j, colorFinal);
        }
      }

      int textureIndex = GetTextureFromBitmap(final);

      glTFLoader.Schema.TextureInfo textureInfo = new glTFLoader.Schema.TextureInfo()
      {
        Index = textureIndex,
        TexCoord = GetTexCoord(texture.MappingChannelId),
      };

      glTFExtensions.KHR_texture_transform transform = GetTextureTransform(texture);

      if(transform != null)
      {
        textureInfo.Extensions = new Dictionary<string, object>()
        {
          {
            glTFExtensions.KHR_texture_transform.Tag, transform
          }
        };
      }

      return textureInfo;
    }

    private Color GetSingleChannelColor(float value, RgbaChannel channel)
    {
      int i = (int)(value * 255.0f);

      i = Math.Max(Math.Min(i, 255), 0);

      switch (channel)
      {
        case RgbaChannel.Alpha:
          return Color.FromArgb(i, 0, 0, 0);
        case RgbaChannel.Red:
          return Color.FromArgb(0, i, 0, 0);
        case RgbaChannel.Green:
          return Color.FromArgb(0, 0, i, 0);
        case RgbaChannel.Blue:
          return Color.FromArgb(0, 0, 0, i);
      }

      return Color.FromArgb(i, i, i, i);
    }

    void HandleBaseColor(Rhino.DocObjects.Material rhinoMaterial, glTFLoader.Schema.Material gltfMaterial)
    {
      Rhino.DocObjects.Texture baseColorDoc = rhinoMaterial.GetTexture(Rhino.DocObjects.TextureType.PBR_BaseColor);
      Rhino.DocObjects.Texture alphaTextureDoc = rhinoMaterial.GetTexture(Rhino.DocObjects.TextureType.PBR_Alpha);

      Rhino.Render.RenderTexture baseColorTexture = pair.RenderMaterial.GetTextureFromUsage(Rhino.Render.RenderMaterial.StandardChildSlots.PbrBaseColor);
      Rhino.Render.RenderTexture alphaTexture = pair.RenderMaterial.GetTextureFromUsage(Rhino.Render.RenderMaterial.StandardChildSlots.PbrAlpha);

      bool baseColorLinear = baseColorTexture == null ? false : baseColorDoc.TreatAsLinear;

      bool hasBaseColorTexture = baseColorDoc == null ? false : baseColorDoc.Enabled;
      bool hasAlphaTexture = alphaTextureDoc == null ? false : alphaTextureDoc.Enabled;

      bool baseColorDiffuseAlphaForTransparency = pair.PBR.UseBaseColorTextureAlphaForObjectAlphaTransparencyTexture;

      Rhino.Display.Color4f baseColor = pair.PBR.BaseColor;

      if (workflow.PreProcessColors)
      {
        baseColor = Rhino.Display.Color4f.ApplyGamma(baseColor, workflow.PreProcessGamma);
      }

      if (!hasBaseColorTexture && !hasAlphaTexture)
      {
        gltfMaterial.PbrMetallicRoughness.BaseColorFactor = new float[]
        {
          baseColor.R,
          baseColor.G,
          baseColor.B,
          (float)pair.PBR.Alpha,
        };

        if (pair.PBR.Alpha == 1.0)
        {
          gltfMaterial.AlphaMode = glTFLoader.Schema.Material.AlphaModeEnum.OPAQUE;
        }
        else
        {
          gltfMaterial.AlphaMode = glTFLoader.Schema.Material.AlphaModeEnum.BLEND;
        }
      }
      else
      {
        baseColorTexture = hasBaseColorTexture ? baseColorTexture : null;
        alphaTexture = hasAlphaTexture ? alphaTexture : null;

        glTFLoader.Schema.TextureInfo info = CombineBaseColorAndAlphaTexture(baseColorTexture, alphaTexture, baseColorDiffuseAlphaForTransparency, baseColor, baseColorLinear, (float)pair.PBR.Alpha, out bool hasAlpha); ;

        glTFExtensions.KHR_texture_transform textureTransform = null;

        if (baseColorTexture != null)
        {
          textureTransform = GetTextureTransform(baseColorDoc);
        }
        else if (alphaTexture != null)
        {
          textureTransform = GetTextureTransform(alphaTextureDoc);
        }

        if (textureTransform != null)
        {
          info.Extensions = new Dictionary<string, object>
          {
            { glTFExtensions.KHR_texture_transform.Tag, textureTransform }
          };
        }

        gltfMaterial.PbrMetallicRoughness.BaseColorTexture = info;
        if(baseColorTexture != null)
        {
          info.TexCoord = GetTexCoord(baseColorTexture.GetMappingChannel());
        }
        else if(alphaTexture != null)
        {
          info.TexCoord = GetTexCoord(alphaTexture.GetMappingChannel());
        }

        if (hasAlpha)
        {
          gltfMaterial.AlphaMode = glTFLoader.Schema.Material.AlphaModeEnum.BLEND;
        }
        else
        {
          gltfMaterial.AlphaMode = glTFLoader.Schema.Material.AlphaModeEnum.OPAQUE;
        }
      }
    }

    glTFLoader.Schema.TextureInfo CombineBaseColorAndAlphaTexture(Rhino.Render.RenderTexture baseColorTexture, Rhino.Render.RenderTexture alphaTexture, bool baseColorDiffuseAlphaForTransparency, Rhino.Display.Color4f baseColor, bool baseColorLinear, float alpha, out bool hasAlpha)
    {
      hasAlpha = false;

      bool hasBaseColorTexture = baseColorTexture != null;
      bool hasAlphaTexture = alphaTexture != null;

      int baseColorWidth, baseColorHeight;
      baseColorWidth = baseColorHeight = 0;

      int alphaWidth, alphaHeight;
      alphaWidth = alphaHeight = 0;

      if (hasBaseColorTexture)
      {
        baseColorTexture.PixelSize(out baseColorWidth, out baseColorHeight, out _);
      }

      if (hasAlphaTexture)
      {
        alphaTexture.PixelSize(out alphaWidth, out alphaHeight, out _);
      }

      int width = Math.Max(baseColorWidth, alphaWidth);
      int height = Math.Max(baseColorHeight, alphaHeight);

      if (width <= 0)
      {
        width = 1024;
      }

      if (height <= 0)
      {
        height = 1024;
      }

      Rhino.Render.TextureEvaluator baseColorEvaluator = null;

      if (hasBaseColorTexture)
      {
        //Disable local mapping since that's handled by the texture transform
        baseColorEvaluator = baseColorTexture.CreateEvaluator(Rhino.Render.RenderTexture.TextureEvaluatorFlags.DisableLocalMapping);
      }

      Rhino.Render.TextureEvaluator alphaTextureEvaluator = null;

      if (hasAlphaTexture)
      {
        //Disable local mapping since that's handled by the texture transform
        alphaTextureEvaluator = alphaTexture.CreateEvaluator(Rhino.Render.RenderTexture.TextureEvaluatorFlags.DisableLocalMapping);
      }

      Bitmap bitmap = new Bitmap(width, height);

      for (int i = 0; i < width; i++)
      {
        for (int j = 0; j < height; j++)
        {
          double x = (double)i / ((double)(width - 1));
          double y = (double)j / ((double)(height - 1));

          y = 1.0 - y;

          Rhino.Geometry.Point3d uvw = new Rhino.Geometry.Point3d(x, y, 0.0);

          Rhino.Display.Color4f baseColorOut = baseColor;

          if (hasBaseColorTexture)
          {
            baseColorOut = baseColorEvaluator.GetColor(uvw, Rhino.Geometry.Vector3d.Zero, Rhino.Geometry.Vector3d.Zero);

            if(baseColorLinear)
            {
              baseColorOut = GlTFUtils.UnapplyGamma(baseColorOut, workflow.PreProcessGamma);
            }
          }

          if (!baseColorDiffuseAlphaForTransparency)
          {
            baseColorOut = new Rhino.Display.Color4f(baseColorOut.R, baseColorOut.G, baseColorOut.B, 1.0f);
          }

          float evaluatedAlpha = (float)alpha;

          if (hasAlphaTexture)
          {
            Rhino.Display.Color4f alphaColor = alphaTextureEvaluator.GetColor(uvw, Rhino.Geometry.Vector3d.Zero, Rhino.Geometry.Vector3d.Zero);
            evaluatedAlpha = alphaColor.L;
          }

          float alphaFinal = baseColor.A * evaluatedAlpha;

          hasAlpha = hasAlpha || alpha != 1.0f;

          //Clamp to deal with HDR values. Not scale.
          float r = Math.Min(1.0f, Math.Max(0.0f, baseColorOut.R));
          float g = Math.Min(1.0f, Math.Max(0.0f, baseColorOut.G));
          float b = Math.Min(1.0f, Math.Max(0.0f, baseColorOut.B));
          float a = Math.Min(1.0f, Math.Max(0.0f, alphaFinal));
          
          Rhino.Display.Color4f colorFinal = new Rhino.Display.Color4f(r, g, b, a);

          bitmap.SetPixel(i, j, colorFinal.AsSystemColor());
        }
      }

      //Testing
      //bitmap.Save(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "out.png"));

      return GetTextureInfoFromBitmap(bitmap);
    }

    private int AddTextureToBuffers(string texturePath)
    {
      var image = GetImageFromFile(texturePath);

      int imageIdx = dummy.Images.AddAndReturnIndex(image);

      var texture = new glTFLoader.Schema.Texture()
      {
        Source = imageIdx,
        Sampler = 0
      };

      return dummy.Textures.AddAndReturnIndex(texture);
    }

    private glTFLoader.Schema.Image GetImageFromFileText(string fileName)
    {
      byte[] imageBytes = GetImageBytesFromFile(fileName);

      var textureBuffer = new glTFLoader.Schema.Buffer()
      {
        Uri = Constants.TextBufferHeader + Convert.ToBase64String(imageBytes),
        ByteLength = imageBytes.Length,
      };

      int textureBufferIdx = dummy.Buffers.AddAndReturnIndex(textureBuffer);

      var textureBufferView = new glTFLoader.Schema.BufferView()
      {
        Buffer = textureBufferIdx,
        ByteOffset = 0,
        ByteLength = textureBuffer.ByteLength,
      };
      int textureBufferViewIdx = dummy.BufferViews.AddAndReturnIndex(textureBufferView);

      return new glTFLoader.Schema.Image()
      {
        BufferView = textureBufferViewIdx,
        MimeType = glTFLoader.Schema.Image.MimeTypeEnum.image_png,
      };
    }

    private glTFLoader.Schema.Image GetImageFromFile(string fileName)
    {
      if (binary)
      {
        return GetImageFromFileBinary(fileName);
      }
      else
      {
        return GetImageFromFileText(fileName);
      }
    }

    private glTFLoader.Schema.Image GetImageFromFileBinary(string fileName)
    {
      byte[] imageBytes = GetImageBytesFromFile(fileName);
      int imageBytesOffset = (int)binaryBuffer.Count;
      binaryBuffer.AddRange(imageBytes);

      var textureBufferView = new glTFLoader.Schema.BufferView()
      {
        Buffer = 0,
        ByteOffset = imageBytesOffset,
        ByteLength = imageBytes.Length,
      };
      int textureBufferViewIdx = dummy.BufferViews.AddAndReturnIndex(textureBufferView);

      return new glTFLoader.Schema.Image()
      {
        BufferView = textureBufferViewIdx,
        MimeType = glTFLoader.Schema.Image.MimeTypeEnum.image_png,
      };
    }

    private byte[] GetImageBytesFromFile(string fileName)
    {
      Bitmap bmp = new Bitmap(fileName);

      return GetImageBytes(bmp);
    }

    private glTFLoader.Schema.TextureInfo GetTextureInfo(Rhino.DocObjects.Texture texture)
    {
      int textureIdx = AddTextureToBuffers(texture.FileReference.FullPath);

      glTFLoader.Schema.TextureInfo rc = new glTFLoader.Schema.TextureInfo()
      {
        Index = textureIdx,
        TexCoord = GetTexCoord(texture.MappingChannelId),
      };

      glTFExtensions.KHR_texture_transform transform = GetTextureTransform(texture);

      if(transform != null)
      {
        rc.Extensions = new Dictionary<string, object>()
        {
          {
            glTFExtensions.KHR_texture_transform.Tag, transform
          }
        };
      }

      return rc;
    }

    private glTFLoader.Schema.MaterialNormalTextureInfo GetNormalTextureInfo(Rhino.DocObjects.Texture normalTexture)
    {
      int textureIdx = AddNormalTexture(normalTexture);

      float weight = GetTextureWeight(normalTexture);

      glTFLoader.Schema.MaterialNormalTextureInfo rc = new glTFLoader.Schema.MaterialNormalTextureInfo()
      {
        Index = textureIdx,
        TexCoord = GetTexCoord(normalTexture.MappingChannelId),
        Scale = weight,
      };

      glTFExtensions.KHR_texture_transform transform = GetTextureTransform(normalTexture);

      if(transform != null)
      {
        rc.Extensions = new Dictionary<string, object>()
        {
          {
            glTFExtensions.KHR_texture_transform.Tag, transform
          }
        };
      }

      return rc;
    }

    private int AddNormalTexture(Rhino.DocObjects.Texture normalTexture)
    {
      Bitmap bmp = new Bitmap(normalTexture.FileReference.FullPath);

      if (!Rhino.BitmapExtensions.IsNormalMap(bmp, true, out bool pZ))
      {
        bmp = Rhino.BitmapExtensions.ConvertToNormalMap(bmp, true, out pZ);
      }

      return GetTextureFromBitmap(bmp);
    }

    private glTFLoader.Schema.MaterialOcclusionTextureInfo GetOcclusionTextureInfo(Rhino.DocObjects.Texture occlusionTexture)
    {
      int textureIdx = AddTextureToBuffers(occlusionTexture.FileReference.FullPath);

      glTFLoader.Schema.MaterialOcclusionTextureInfo rc = new glTFLoader.Schema.MaterialOcclusionTextureInfo()
      {
        Index = textureIdx,
        TexCoord = GetTexCoord(occlusionTexture.MappingChannelId),
        Strength = GetTextureWeight(occlusionTexture),
      };

      glTFExtensions.KHR_texture_transform transform = GetTextureTransform(occlusionTexture);

      if (transform != null)
      {
        rc.Extensions = new Dictionary<string, object>()
        {
          {
            glTFExtensions.KHR_texture_transform.Tag, transform
          }
        };
      }

      return rc;
    }

    public glTFLoader.Schema.TextureInfo GetMetallicRoughnessTextureInfo(Rhino.DocObjects.Material rhinoMaterial)
    {
      Rhino.DocObjects.Texture metalTexture = pair.PBR.GetTexture(Rhino.DocObjects.TextureType.PBR_Metallic);
      Rhino.DocObjects.Texture roughnessTexture = pair.PBR.GetTexture(Rhino.DocObjects.TextureType.PBR_Roughness);

      bool hasMetalTexture = metalTexture == null ? false : metalTexture.Enabled;
      bool hasRoughnessTexture = roughnessTexture == null ? false : roughnessTexture.Enabled;

      Rhino.Render.RenderTexture renderTextureMetal = null;
      Rhino.Render.RenderTexture renderTextureRoughness = null;

      int mWidth = 0;
      int mHeight = 0;
      int rWidth = 0;
      int rHeight = 0;

      // Get the textures
      if (hasMetalTexture)
      {
        renderTextureMetal = pair.RenderMaterial.GetTextureFromUsage(Rhino.Render.RenderMaterial.StandardChildSlots.PbrMetallic);
        renderTextureMetal.PixelSize(out mWidth, out mHeight, out _);
      }

      if (hasRoughnessTexture)
      {
        renderTextureRoughness = pair.RenderMaterial.GetTextureFromUsage(Rhino.Render.RenderMaterial.StandardChildSlots.PbrRoughness);
        renderTextureRoughness.PixelSize(out rWidth, out rHeight, out _);
      }

      int width = Math.Max(mWidth, rWidth);
      int height = Math.Max(mHeight, rHeight);

      Rhino.Render.TextureEvaluator evalMetal = null;
      Rhino.Render.TextureEvaluator evalRoughness = null;

      // Metal
      if (hasMetalTexture)
      {
        //Disable local mapping since that's handled by the texture transform
        evalMetal = renderTextureMetal.CreateEvaluator(Rhino.Render.RenderTexture.TextureEvaluatorFlags.DisableLocalMapping);
      }

      // Roughness
      if (hasRoughnessTexture)
      {
        //Disable local mapping since that's handled by the texture transform
        evalRoughness = renderTextureRoughness.CreateEvaluator(Rhino.Render.RenderTexture.TextureEvaluatorFlags.DisableLocalMapping);
      }

      // Copy Metal to the blue channel, roughness to the green
      var bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

      for (var j = 0; j < height - 1; j += 1)
      {
        for (var i = 0; i < width - 1; i += 1)
        {
          double x = (double)i / (double)(width - 1);
          double y = (double)j / (double)(height - 1);

          Rhino.Geometry.Point3d uvw = new Rhino.Geometry.Point3d(x, y, 0.0);

          float g = 1.0f;
          float b = 1.0f;

          if (hasMetalTexture)
          {
            Rhino.Display.Color4f metal = evalMetal.GetColor(uvw, Rhino.Geometry.Vector3d.Zero, Rhino.Geometry.Vector3d.Zero);
            b = metal.L; //grayscale maps, so we want lumonosity
          }

          if (hasRoughnessTexture)
          {
            Rhino.Display.Color4f roughnessColor = evalRoughness.GetColor(uvw, Rhino.Geometry.Vector3d.ZAxis, Rhino.Geometry.Vector3d.Zero);
            g = roughnessColor.L; //grayscale maps, so we want lumonosity
          }

          Rhino.Display.Color4f color = new Rhino.Display.Color4f(0.0f, g, b, 1.0f);
          bitmap.SetPixel(i, height - j - 1, color.AsSystemColor());
        }
      }

      glTFLoader.Schema.TextureInfo textureInfo = GetTextureInfoFromBitmap(bitmap);

      glTFExtensions.KHR_texture_transform metallicTransform = hasMetalTexture ? GetTextureTransform(metalTexture) : null;
      glTFExtensions.KHR_texture_transform roughnessTransform = hasRoughnessTexture ? GetTextureTransform(roughnessTexture) : null;

      if(metallicTransform != null)
      {
        textureInfo.Extensions = new Dictionary<string, object>()
        {
          { 
            glTFExtensions.KHR_texture_transform.Tag, metallicTransform
          }
        };
      }
      else if(roughnessTransform != null)
      {
        textureInfo.Extensions = new Dictionary<string, object>()
        {
          {
            glTFExtensions.KHR_texture_transform.Tag, roughnessTransform
          }
        };
      }

      if(hasMetalTexture)
      {
        textureInfo.TexCoord = GetTexCoord(metalTexture.MappingChannelId);
      }
      else if(hasRoughnessTexture)
      {
        textureInfo.TexCoord = GetTexCoord(roughnessTexture.MappingChannelId);
      }

      return textureInfo;
    }

    private int GetTextureFromBitmap(Bitmap bitmap)
    {
      var image = GetImageFromBitmap(bitmap);

      int imageIdx = dummy.Images.AddAndReturnIndex(image);

      var texture = new glTFLoader.Schema.Texture()
      {
        Source = imageIdx,
        Sampler = 0
      };

      return dummy.Textures.AddAndReturnIndex(texture);
    }

    private glTFLoader.Schema.TextureInfo GetTextureInfoFromBitmap(Bitmap bitmap)
    {
      int textureIdx = GetTextureFromBitmap(bitmap);

      return new glTFLoader.Schema.TextureInfo()
      {
        Index = textureIdx,
        TexCoord = 0
      };
    }

    private glTFLoader.Schema.Image GetImageFromBitmap(Bitmap bitmap)
    {
      if (binary)
      {
        return GetImageFromBitmapBinary(bitmap);
      }
      else
      {
        return GetImageFromBitmapText(bitmap);
      }
    }

    private glTFLoader.Schema.Image GetImageFromBitmapText(Bitmap bitmap)
    {
      byte[] imageBytes = GetImageBytes(bitmap);

      var textureBuffer = new glTFLoader.Schema.Buffer();

      textureBuffer.Uri = Constants.TextBufferHeader + Convert.ToBase64String(imageBytes);
      textureBuffer.ByteLength = imageBytes.Length;

      int textureBufferIdx = dummy.Buffers.AddAndReturnIndex(textureBuffer);

      // Create bufferviews
      var textureBufferView = new glTFLoader.Schema.BufferView()
      {
        Buffer = textureBufferIdx,
        ByteOffset = 0,
        ByteLength = textureBuffer.ByteLength,
      };
      int textureBufferViewIdx = dummy.BufferViews.AddAndReturnIndex(textureBufferView);

      return new glTFLoader.Schema.Image()
      {
        BufferView = textureBufferViewIdx,
        MimeType = glTFLoader.Schema.Image.MimeTypeEnum.image_png,
      };
    }

    private glTFLoader.Schema.Image GetImageFromBitmapBinary(Bitmap bitmap)
    {
      byte[] imageBytes = GetImageBytes(bitmap);
      int imageBytesOffset = (int)binaryBuffer.Count;
      binaryBuffer.AddRange(imageBytes);

      // Create bufferviews
      var textureBufferView = new glTFLoader.Schema.BufferView()
      {
        Buffer = 0,
        ByteOffset = imageBytesOffset,
        ByteLength = imageBytes.Length,
      };
      int textureBufferViewIdx = dummy.BufferViews.AddAndReturnIndex(textureBufferView);

      return new glTFLoader.Schema.Image()
      {
        BufferView = textureBufferViewIdx,
        MimeType = glTFLoader.Schema.Image.MimeTypeEnum.image_png,
      };
    }

    private byte[] GetImageBytes(Bitmap bitmap)
    {
      using (MemoryStream imageStream = new MemoryStream(4096))
      {
        bitmap.Save(imageStream, System.Drawing.Imaging.ImageFormat.Png);

        //Zero pad so its 4 byte aligned
        long mod = imageStream.Position % 4;
        imageStream.Write(Constants.Paddings[mod], 0, Constants.Paddings[mod].Length);

        return imageStream.ToArray();
      }
    }

    private float GetTextureWeight(Rhino.DocObjects.Texture texture)
    {
      texture.GetAlphaBlendValues(out double constant, out double a0, out double a1, out double a2, out double a3);

      return (float)constant;
    }

    glTFExtensions.KHR_texture_transform GetTextureTransform(Rhino.DocObjects.Texture texture)
    {
      Rhino.Geometry.Transform uvw = texture.UvwTransform;
      if(!texture.ApplyUvwTransform)
      {
        uvw = Rhino.Geometry.Transform.Identity;
      }

      Rhino.Geometry.Transform toGltfTextureSpace = Rhino.Geometry.Transform.Identity;
      toGltfTextureSpace[1, 1] = -1;
      toGltfTextureSpace[1, 3] = 1;

      Rhino.Geometry.Transform gltfTextureTransform = toGltfTextureSpace * uvw;

      if(gltfTextureTransform.IsIdentity)
      {
        return null;
      }

      //Decompose to glTF
      if (!DecomposeGltfMappingTransform(gltfTextureTransform, out Rhino.Geometry.Vector2d offset, out double rotation, out Rhino.Geometry.Vector2d scale))
      {
        return null;
      }

      glTFExtensions.KHR_texture_transform textureTransform = new glTFExtensions.KHR_texture_transform();

      textureTransform.Offset = new float[]
      {
        (float)offset.X,
        (float)offset.Y
      };

      textureTransform.Rotation = (float)rotation;

      textureTransform.Scale = new float[]
      {
        (float)scale.X,
        (float)scale.Y,
      };

      return textureTransform;
    }

    public static bool DecomposeGltfMappingTransform(Rhino.Geometry.Transform textureTransform, out Rhino.Geometry.Vector2d offset, out double rotation, out Rhino.Geometry.Vector2d scale)
    {
      offset = new Rhino.Geometry.Vector2d(textureTransform.M03, textureTransform.M13);

      rotation = Math.Atan2(textureTransform.M01, textureTransform.M00);

      double cosTheta = Math.Cos(rotation);

      double sx = textureTransform.M00 / cosTheta;
      double sy = textureTransform.M11 / cosTheta;

      scale = new Rhino.Geometry.Vector2d(sx, sy);

      return true;
    }

    private int GetTexCoord(int mappingChannel)
    {
      if(mappingToGltfTexCoord.TryGetValue(mappingChannel, out int texCoord))
      {
        return texCoord;
      }

      return 0; //default
    }

  }
}
