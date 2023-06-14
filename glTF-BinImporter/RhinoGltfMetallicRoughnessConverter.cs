using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Import_glTF
{
  class RhinoGltfMetallicRoughnessConverter
  {
    GltfRhinoConverter converter = null;

    public RhinoGltfMetallicRoughnessConverter(GltfRhinoConverter converter, System.Drawing.Bitmap bmp, Rhino.RhinoDoc doc, string name)
    {
      this.converter = converter;

      System.Drawing.Bitmap metalnessBitmap = new System.Drawing.Bitmap(bmp.Width, bmp.Height);

      System.Drawing.Bitmap roughnessBitmap = new System.Drawing.Bitmap(bmp.Width, bmp.Height);

      for (int i = 0; i < bmp.Width; i++)
      {
        for (int j = 0; j < bmp.Height; j++)
        {
          System.Drawing.Color color = bmp.GetPixel(i, j);

          byte metalness = color.B;
          byte roughness = color.G;

          metalnessBitmap.SetPixel(i, j, System.Drawing.Color.FromArgb(metalness, metalness, metalness));
          roughnessBitmap.SetPixel(i, j, System.Drawing.Color.FromArgb(roughness, roughness, roughness));
        }
      }

      string metallicName = name + "-Metallic";
      string roughnessName = name + "-Roughness";

      MetallicTexture = converter.GetRenderTextureFromBitmap(metalnessBitmap, metallicName);

      RoughnessTexture = converter.GetRenderTextureFromBitmap(roughnessBitmap, roughnessName);
    }

    public Rhino.Render.RenderTexture MetallicTexture
    {
      get;
      private set;
    } = null;

    public Rhino.Render.RenderTexture RoughnessTexture
    {
      get;
      private set;
    } = null;

  }
}
