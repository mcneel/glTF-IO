using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Import_glTF
{
  enum ArgbChannel : int
  {
    Alpha = 0,
    Red = 1,
    Green = 2,
    Blue = 3,
  }

  class ImageHolder
  {
    public ImageHolder(GltfRhinoConverter converter, System.Drawing.Bitmap originalBmp, string name)
    {
      this.converter = converter;
      this.originalBmp = originalBmp;
      this.name = name;

      if (string.IsNullOrEmpty(this.name))
      {
        this.name = converter.GetUniqueName(this.name);
      }
    }

    GltfRhinoConverter converter = null;
    System.Drawing.Bitmap originalBmp = null;
    string name = null;

    string rgbaImagePath = null;

    string[] channelPaths = new string[]
    {
        null,
        null,
        null,
        null,
    };

    public string RgbaImagePath()
    {
      if (string.IsNullOrEmpty(rgbaImagePath))
      {
        string unpackedPath = converter.GetUnpackedTexturePath();

        string textureFilename = Path.Combine(unpackedPath, name + ".png");

        originalBmp.Save(textureFilename);

        rgbaImagePath = textureFilename;
      }

      return rgbaImagePath;
    }

    public string ImagePathForChannel(ArgbChannel channel)
    {
      int idx = (int)channel;

      if (string.IsNullOrEmpty(channelPaths[idx]))
      {
        string unpackedPath = converter.GetUnpackedTexturePath();

        string channelName = name + StemForChannel(channel);

        string textureFilename = Path.Combine(unpackedPath, channelName + ".png");

        int width = originalBmp.Width;
        int height = originalBmp.Height;

        System.Drawing.Bitmap resolvedBmp = new System.Drawing.Bitmap(width, height);

        for (int i = 0; i < width; i++)
        {
          for (int j = 0; j < height; j++)
          {
            System.Drawing.Color color = originalBmp.GetPixel(i, j);

            System.Drawing.Color colorResolved = GetColorFromChannel(color, channel);

            resolvedBmp.SetPixel(i, j, colorResolved);
          }
        }

        resolvedBmp.Save(textureFilename);

        channelPaths[idx] = textureFilename;
      }

      return channelPaths[idx];
    }

    string StemForChannel(ArgbChannel channel)
    {
      switch (channel)
      {
        case ArgbChannel.Red:
          return "-r";
        case ArgbChannel.Green:
          return "-g";
        case ArgbChannel.Blue:
          return "-b";
        case ArgbChannel.Alpha:
          return "-a";
        default:
          return "";
      }
    }

    private System.Drawing.Color GetColorFromChannel(System.Drawing.Color color, ArgbChannel channel)
    {
      switch (channel)
      {
        case ArgbChannel.Red:
          return System.Drawing.Color.FromArgb(color.R, color.R, color.R);
        case ArgbChannel.Green:
          return System.Drawing.Color.FromArgb(color.G, color.G, color.G);
        case ArgbChannel.Blue:
          return System.Drawing.Color.FromArgb(color.B, color.B, color.B);
        case ArgbChannel.Alpha:
          return System.Drawing.Color.FromArgb(color.A, color.A, color.A);
        default:
          return color;
      }
    }

  }
}
