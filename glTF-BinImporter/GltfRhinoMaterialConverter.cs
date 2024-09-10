using Rhino.Render;
using Rhino.Render.ChildSlotNames;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Import_glTF
{
  class GltfRhinoMaterialConverter
  {
    public GltfRhinoMaterialConverter(glTFLoader.Schema.Material material, Rhino.RhinoDoc doc, GltfRhinoConverter converter)
    {
      this.material = material;
      this.doc = doc;
      this.converter = converter;
    }

    glTFLoader.Schema.Material material = null;
    Rhino.RhinoDoc doc = null;
    GltfRhinoConverter converter = null;

    public Rhino.Render.RenderMaterial Convert()
    {
      RenderMaterial pbr = RenderContentType.NewContentFromTypeId(ContentUuids.PhysicallyBasedMaterialType, doc) as RenderMaterial;

      pbr.BeginChange(RenderContent.ChangeContexts.Program);

      pbr.Name = converter.GetUniqueName(material.Name);

      if (material.PbrMetallicRoughness != null)
      {
        Rhino.Display.Color4f baseColor = material.PbrMetallicRoughness.BaseColorFactor.ToColor4f();

        if (material.PbrMetallicRoughness.BaseColorTexture != null)
        {
          int index = material.PbrMetallicRoughness.BaseColorTexture.Index;

          RenderTexture texture = converter.GetRenderTexture(index);
          if (texture != null)
          {
            texture.SetMappingChannel(GltfUtils.GltfTexCoordIndexToRhinoMappingChannel(material.PbrMetallicRoughness.BaseColorTexture.TexCoord), RenderContent.ChangeContexts.Program);

            TryHandleTextureTransform(texture, material.PbrMetallicRoughness.BaseColorTexture.Extensions);

            RenderTexture child = null;

            if (baseColor.R == 1.0 && baseColor.G == 1.0 && baseColor.B == 1.0 && baseColor.A == 1.0)
            {
              child = texture;
            }
            else
            {
              child = converter.CreateMultiplyTexture(texture, baseColor);
            }

            pbr.SetChild(child, Rhino.Render.ParameterNames.PhysicallyBased.BaseColor);
            pbr.SetChildSlotOn(Rhino.Render.ParameterNames.PhysicallyBased.BaseColor, true, RenderContent.ChangeContexts.Program);

            pbr.SetParameter("alpha-transparency", true);
          }
        }
        else
        {
          baseColor = GltfUtils.UnapplyGamma(baseColor);

          pbr.SetParameter(PhysicallyBased.BaseColor, baseColor);
        }

        double roughness = material.PbrMetallicRoughness.RoughnessFactor;

        double metalness = material.PbrMetallicRoughness.MetallicFactor;

        if (material.PbrMetallicRoughness.MetallicRoughnessTexture != null)
        {
          int index = material.PbrMetallicRoughness.MetallicRoughnessTexture.Index;

          RenderTexture metallicTexture = converter.GetRenderTexture(index, ArgbChannel.Blue);
          if(metallicTexture != null)
          {
            metallicTexture.SetMappingChannel(GltfUtils.GltfTexCoordIndexToRhinoMappingChannel(material.PbrMetallicRoughness.MetallicRoughnessTexture.TexCoord), RenderContent.ChangeContexts.Program);

            TryHandleTextureTransform(metallicTexture, material.PbrMetallicRoughness.MetallicRoughnessTexture.Extensions);

            pbr.SetChild(metallicTexture, PhysicallyBased.Metallic);
            pbr.SetChildSlotOn(PhysicallyBased.Metallic, true, RenderContent.ChangeContexts.Program);
            pbr.SetChildSlotAmount(PhysicallyBased.Metallic, metalness * 100.0, RenderContent.ChangeContexts.Program);
          }

          RenderTexture roughnessTexture = converter.GetRenderTexture(index, ArgbChannel.Green);
          if(roughnessTexture != null)
          {
            roughnessTexture.SetMappingChannel(GltfUtils.GltfTexCoordIndexToRhinoMappingChannel(material.PbrMetallicRoughness.MetallicRoughnessTexture.TexCoord), RenderContent.ChangeContexts.Program);

            TryHandleTextureTransform(roughnessTexture, material.PbrMetallicRoughness.MetallicRoughnessTexture.Extensions);

            pbr.SetChild(roughnessTexture, PhysicallyBased.Roughness);
            pbr.SetChildSlotOn(PhysicallyBased.Roughness, true, RenderContent.ChangeContexts.Program);
            pbr.SetChildSlotAmount(PhysicallyBased.Roughness, roughness * 100.0, RenderContent.ChangeContexts.Program);
          }
        }
        else
        {
          pbr.SetParameter(PhysicallyBased.Roughness, roughness);

          pbr.SetParameter(PhysicallyBased.Metallic, metalness);
        }
      }

      Rhino.Display.Color4f emissionColor = material.EmissiveFactor.ToColor4f();

      emissionColor = GltfUtils.UnapplyGamma(emissionColor);

      pbr.SetParameter(PhysicallyBased.Emission, emissionColor);

      if (material.EmissiveTexture != null)
      {
        RenderTexture emissiveTexture = converter.GetRenderTexture(material.EmissiveTexture.Index);
        if(emissiveTexture != null)
        {
          emissiveTexture.SetMappingChannel(GltfUtils.GltfTexCoordIndexToRhinoMappingChannel(material.EmissiveTexture.TexCoord), RenderContent.ChangeContexts.Program);

          TryHandleTextureTransform(emissiveTexture, material.EmissiveTexture.Extensions);

          pbr.SetChild(emissiveTexture, PhysicallyBased.Emission);
          pbr.SetChildSlotOn(PhysicallyBased.Emission, true, RenderContent.ChangeContexts.Program);
        }
      }

      if (material.OcclusionTexture != null)
      {
        //Occlusion texture is only the R channel
        //https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.html#_material_occlusiontexture
        RenderTexture occlusionTexture = converter.GetRenderTexture(material.OcclusionTexture.Index, ArgbChannel.Red);
        if(occlusionTexture != null)
        {
          occlusionTexture.SetMappingChannel(GltfUtils.GltfTexCoordIndexToRhinoMappingChannel(material.OcclusionTexture.TexCoord), RenderContent.ChangeContexts.Program);

          TryHandleTextureTransform(occlusionTexture, material.OcclusionTexture.Extensions);

          pbr.SetChild(occlusionTexture, PhysicallyBased.AmbientOcclusion);
          pbr.SetChildSlotOn(PhysicallyBased.AmbientOcclusion, true, RenderContent.ChangeContexts.Program);
          pbr.SetChildSlotAmount(PhysicallyBased.AmbientOcclusion, material.OcclusionTexture.Strength * 100.0, RenderContent.ChangeContexts.Program);
        }
      }

      if (material.NormalTexture != null)
      {
        RenderTexture normalTexture = converter.GetRenderTexture(material.NormalTexture.Index);
        if(normalTexture != null)
        {
          normalTexture.SetMappingChannel(GltfUtils.GltfTexCoordIndexToRhinoMappingChannel(material.NormalTexture.TexCoord), RenderContent.ChangeContexts.Program);

          TryHandleTextureTransform(normalTexture, material.NormalTexture.Extensions);

          pbr.SetChild(normalTexture, PhysicallyBased.Bump);
          pbr.SetChildSlotOn(PhysicallyBased.Bump, true, RenderContent.ChangeContexts.Program);
        }
      }

      string clearcoatText = "";
      string transmissionText = "";
      string iorText = "";
      string specularText = "";

      if (material.Extensions != null)
      {
        if (material.Extensions.TryGetValue(glTFExtensions.KHR_materials_clearcoat.Tag, out object clearcoatValue))
        {
          clearcoatText = clearcoatValue.ToString();
        }

        if (material.Extensions.TryGetValue(glTFExtensions.KHR_materials_transmission.Tag, out object transmissionValue))
        {
          transmissionText = transmissionValue.ToString();
        }

        if (material.Extensions.TryGetValue(glTFExtensions.KHR_materials_ior.Tag, out object iorValue))
        {
          iorText = iorValue.ToString();
        }

        if (material.Extensions.TryGetValue(glTFExtensions.KHR_materials_specular.Tag, out object specularValue))
        {
          specularText = specularValue.ToString();
        }
      }

      HandleClearcoat(clearcoatText, pbr);

      HandleTransmission(transmissionText, pbr);

      HandleIor(iorText, pbr);

      HandleSpecular(specularText, pbr);

      pbr.EndChange();

      doc.RenderMaterials.BeginChange(RenderContent.ChangeContexts.Program);

      doc.RenderMaterials.Add(pbr);

      doc.RenderMaterials.EndChange();

      return pbr;
    }

    void HandleClearcoat(string text, RenderMaterial pbr)
    {
      glTFExtensions.KHR_materials_clearcoat clearcoat = Newtonsoft.Json.JsonConvert.DeserializeObject<glTFExtensions.KHR_materials_clearcoat>(text);

      if (clearcoat == null)
      {
        pbr.SetParameter(PhysicallyBased.Clearcoat, 0.0);

        pbr.SetParameter(PhysicallyBased.ClearcoatRoughness, 0.0);
      }
      else
      {
        if (clearcoat.ClearcoatTexture != null)
        {
          RenderTexture clearcoatTexture = converter.GetRenderTexture(clearcoat.ClearcoatTexture.Index);
          if(clearcoatTexture != null)
          {
            clearcoatTexture.SetMappingChannel(GltfUtils.GltfTexCoordIndexToRhinoMappingChannel(clearcoat.ClearcoatTexture.TexCoord), RenderContent.ChangeContexts.Program);

            TryHandleTextureTransform(clearcoatTexture, clearcoat.ClearcoatTexture.Extensions);

            pbr.SetChild(clearcoatTexture, PhysicallyBased.Clearcoat);
            pbr.SetChildSlotOn(PhysicallyBased.Clearcoat, true, RenderContent.ChangeContexts.Program);
            pbr.SetChildSlotAmount(PhysicallyBased.Clearcoat, clearcoat.ClearcoatFactor * 100.0, RenderContent.ChangeContexts.Program);
          }
        }
        else
        {
          pbr.SetParameter(PhysicallyBased.Clearcoat, clearcoat.ClearcoatFactor);
        }

        if (clearcoat.ClearcoatRoughnessTexture != null)
        {
          RenderTexture clearcoatRoughnessTexture = converter.GetRenderTexture(clearcoat.ClearcoatRoughnessTexture.Index);
          if(clearcoatRoughnessTexture != null)
          {
            clearcoatRoughnessTexture.SetMappingChannel(GltfUtils.GltfTexCoordIndexToRhinoMappingChannel(clearcoat.ClearcoatRoughnessTexture.TexCoord), RenderContent.ChangeContexts.Program);

            TryHandleTextureTransform(clearcoatRoughnessTexture, clearcoat.ClearcoatRoughnessTexture.Extensions);

            pbr.SetChild(clearcoatRoughnessTexture, PhysicallyBased.ClearcoatRoughness);
            pbr.SetChildSlotOn(PhysicallyBased.ClearcoatRoughness, true, RenderContent.ChangeContexts.Program);
            pbr.SetChildSlotAmount(PhysicallyBased.ClearcoatRoughness, clearcoat.ClearcoatRoughnessFactor * 100.0, RenderContent.ChangeContexts.Program);
          }
        }
        else
        {
          pbr.SetParameter(PhysicallyBased.ClearcoatRoughness, clearcoat.ClearcoatRoughnessFactor);
        }

        if (clearcoat.ClearcoatNormalTexture != null)
        {
          RenderTexture clearcoatNormalTexture = converter.GetRenderTexture(clearcoat.ClearcoatNormalTexture.Index);
          if(clearcoatNormalTexture != null)
          {
            clearcoatNormalTexture.SetMappingChannel(GltfUtils.GltfTexCoordIndexToRhinoMappingChannel(clearcoat.ClearcoatNormalTexture.TexCoord), RenderContent.ChangeContexts.Program);

            TryHandleTextureTransform(clearcoatNormalTexture, clearcoat.ClearcoatNormalTexture.Extensions);

            pbr.SetChild(clearcoatNormalTexture, PhysicallyBased.ClearcoatBump);
            pbr.SetChildSlotOn(PhysicallyBased.ClearcoatBump, true, RenderContent.ChangeContexts.Program);
          }
        }
      }
    }

    void HandleTransmission(string text, RenderMaterial pbr)
    {
      glTFExtensions.KHR_materials_transmission transmission = Newtonsoft.Json.JsonConvert.DeserializeObject<glTFExtensions.KHR_materials_transmission>(text);

      if (transmission == null)
      {
        pbr.SetParameter(PhysicallyBased.Opacity, 1.0);
      }
      else
      {
        if (transmission.TransmissionTexture != null)
        {
          //Transmission is stored in the textures red channel
          RenderTexture transmissionTexture = converter.GetRenderTexture(transmission.TransmissionTexture.Index, ArgbChannel.Red);
          if(transmissionTexture != null)
          {
            transmissionTexture.SetMappingChannel(GltfUtils.GltfTexCoordIndexToRhinoMappingChannel(transmission.TransmissionTexture.TexCoord), RenderContent.ChangeContexts.Program);

            TryHandleTextureTransform(transmissionTexture, transmission.TransmissionTexture.Extensions);

            pbr.SetChild(transmissionTexture, PhysicallyBased.Opacity);
            pbr.SetChildSlotOn(PhysicallyBased.Opacity, true, RenderContent.ChangeContexts.Program);
            pbr.SetChildSlotAmount(PhysicallyBased.Opacity, transmission.TransmissionFactor * 100.0, RenderContent.ChangeContexts.Program);
          }
        }
        else
        {
          pbr.SetParameter(PhysicallyBased.Opacity, 1.0 - transmission.TransmissionFactor);
        }
      }
    }

    void HandleIor(string text, RenderMaterial pbr)
    {
      glTFExtensions.KHR_materials_ior ior = Newtonsoft.Json.JsonConvert.DeserializeObject<glTFExtensions.KHR_materials_ior>(text);

      if (ior != null)
      {
        pbr.SetParameter(PhysicallyBased.OpacityIor, ior.Ior);
      }
    }

    void HandleSpecular(string text, RenderMaterial pbr)
    {
      glTFExtensions.KHR_materials_specular specular = Newtonsoft.Json.JsonConvert.DeserializeObject<glTFExtensions.KHR_materials_specular>(text);

      if (specular == null)
      {
        pbr.SetParameter(PhysicallyBased.Specular, 1.0);
      }
      else
      {
        if (specular.SpecularTexture != null)
        {
          RenderTexture specularTexture = converter.GetRenderTexture(specular.SpecularTexture.Index, ArgbChannel.Alpha);
          if(specularTexture != null)
          {
            specularTexture.SetMappingChannel(GltfUtils.GltfTexCoordIndexToRhinoMappingChannel(specular.SpecularTexture.TexCoord), RenderContent.ChangeContexts.Program);

            TryHandleTextureTransform(specularTexture, specular.SpecularTexture.Extensions);

            pbr.SetChild(specularTexture, PhysicallyBased.Specular);
            pbr.SetChildSlotOn(PhysicallyBased.Specular, true, RenderContent.ChangeContexts.Program);
            pbr.SetChildSlotAmount(PhysicallyBased.Specular, specular.SpecularFactor * 100.0, RenderContent.ChangeContexts.Program);
          }
        }
        else
        {
          pbr.SetParameter(PhysicallyBased.Specular, specular.SpecularFactor);
        }
      }
    }

    bool TryHandleTextureTransform(RenderTexture texture, Dictionary<string, object> extensions)
    {
      if (extensions == null)
      {
        //No extensions
        return true;
      }

      if(!extensions.TryGetValue(glTFExtensions.KHR_texture_transform.Tag, out object t))
      {
        //No texture transform extension
        return true;
      }

      string transformText = t.ToString();

      glTFExtensions.KHR_texture_transform transform = Newtonsoft.Json.JsonConvert.DeserializeObject<glTFExtensions.KHR_texture_transform>(transformText);

      if (transform == null)
      {
        //Failed to deserialize the texture transform
        return false;
      }

      Rhino.Geometry.Transform translate = Rhino.Geometry.Transform.Identity;
      Rhino.Geometry.Transform rotate = Rhino.Geometry.Transform.Identity;
      Rhino.Geometry.Transform scale = Rhino.Geometry.Transform.Identity;

      //glTF textures have an upper left origin
      //https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.html#images
      //Rhino textures use a lower left origin
      //So here we apply the transforms in glTF space and then decompose the mapping transform into Rhinos UV space
      Rhino.Geometry.Point3d gltfTextureOrigin = new Rhino.Geometry.Point3d(0.0, 1.0, 0.0);

      Rhino.Geometry.Plane gltfBasePlane = new Rhino.Geometry.Plane(gltfTextureOrigin, Rhino.Geometry.Vector3d.ZAxis);

      if (transform.Offset != null)
      {
        //glTF has flipped V texcoords
        translate = Rhino.Geometry.Transform.Translation(new Rhino.Geometry.Vector3d(transform.Offset[0], -transform.Offset[1], 0.0));
      }

      if (transform.Rotation != 0.0f)
      {
        //Use the glTF origin as rotation origin
        rotate = Rhino.Geometry.Transform.Rotation(transform.Rotation, Rhino.Geometry.Vector3d.ZAxis, gltfTextureOrigin);
      }

      if (transform.Scale != null)
      {
        //Use the glTF origin and the center of scaling
        scale = Rhino.Geometry.Transform.Scale(gltfBasePlane, transform.Scale[0], transform.Scale[1], 1.0);
      }

      //Compile the TRS transform
      Rhino.Geometry.Transform compiled = translate * rotate * scale;

      //Decompose to Rhino
      compiled.DecomposeTextureMapping(out Rhino.Geometry.Vector3d offset, out Rhino.Geometry.Vector3d repeat, out Rhino.Geometry.Vector3d rotation);

      if (
        rotation == Rhino.Geometry.Vector3d.Unset ||
        repeat == Rhino.Geometry.Vector3d.Unset
        )
      {
        //Unset vectors means it failed to decompose the texture mapping transform
        return false;
      }

      //The rotation comes out in radians but it needs to be degrees when set on the texture
      rotation.X = Rhino.RhinoMath.ToDegrees(rotation.X);
      rotation.Y = Rhino.RhinoMath.ToDegrees(rotation.Y);
      rotation.Z = Rhino.RhinoMath.ToDegrees(rotation.Z);

      texture.SetOffset(offset, RenderContent.ChangeContexts.Program);
      texture.SetRepeat(repeat, RenderContent.ChangeContexts.Program);
      texture.SetRotation(rotation, RenderContent.ChangeContexts.Program);

      return true;
    }
  }
}
