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

        int counter = 0;
        while(File.Exists(textureFilename))
        {
          counter++;
          textureFilename = Path.Combine(unpackedPath, name + "-" + counter.ToString() + ".png");
        }

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

        System.Drawing.Bitmap resolvedBmp = GetSingleChannelImage(channel);

        resolvedBmp.Save(textureFilename);

        channelPaths[idx] = textureFilename;
      }

      return channelPaths[idx];
    }

    System.Drawing.Bitmap GetSingleChannelImage(ArgbChannel channel)
    {
      int width = originalBmp.Width;
      int height = originalBmp.Height;

      int countPixels = width * height;

      int[] resolvedPixels = new int[countPixels];

      System.Drawing.Rectangle bmpRectangle = new System.Drawing.Rectangle(0, 0, width, height);

      //Fetch original image pixels
      {
        var originalBitmapData = originalBmp.LockBits(bmpRectangle, System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);

        System.Runtime.InteropServices.Marshal.Copy(originalBitmapData.Scan0, resolvedPixels, 0, countPixels);

        originalBmp.UnlockBits(originalBitmapData);
      }

      //Get single channel image
      Parallel.For(0, countPixels, i =>
      {
        System.Drawing.Color color = System.Drawing.Color.FromArgb(resolvedPixels[i]);

        resolvedPixels[i] = GetColorFromChannel(color, channel).ToArgb();
      });

      System.Drawing.Bitmap resolvedBmp = new System.Drawing.Bitmap(width, height);

      //Dump in the new bitmap
      {
        var resolvedBmpData = resolvedBmp.LockBits(bmpRectangle, System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        System.Runtime.InteropServices.Marshal.Copy(resolvedPixels, 0, resolvedBmpData.Scan0, countPixels);

        resolvedBmp.UnlockBits(resolvedBmpData);
      }

      return resolvedBmp;
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
