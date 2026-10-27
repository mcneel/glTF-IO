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
    public ImageHolder(GltfRhinoConverter converter, string name, int imgIdx)
    {
      // Make sure name is valid before using to set this.name below.
      if (string.IsNullOrEmpty(name))
        name = converter.GetUniqueName(name);

      this.converter = converter;
      this.name = name;
      this.imgIdx = imgIdx;
    }

    GltfRhinoConverter converter = null;
    int imgIdx = -1;
    string name = null;

    string rgbaImagePath = null;

    string[] channelPaths = new string[]
    {
        null,
        null,
        null,
        null,
    };

    System.Drawing.Bitmap GetOriginalBitmap()
    {
      Stream stream = glTFLoader.Interface.OpenImageFile(converter.glTF, imgIdx, converter.FilePath);
      return new System.Drawing.Bitmap(stream);
    }

    public string RgbaImagePath()
    {
      if (string.IsNullOrEmpty(rgbaImagePath))
      {
        using (System.Drawing.Bitmap originalBmp = GetOriginalBitmap())
        {
          string unpackedPath = converter.GetUnpackedTexturePath();

          string textureFilename = Path.Combine(unpackedPath, name + ".png");

          int counter = 0;
          while (File.Exists(textureFilename))
          {
            counter++;
            textureFilename = Path.Combine(unpackedPath, name + "-" + counter.ToString() + ".png");
          }

          originalBmp.Save(textureFilename);

          rgbaImagePath = textureFilename;
        }
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

        using (System.Drawing.Bitmap resolvedBmp = GetSingleChannelImage(channel))
        {
          resolvedBmp.Save(textureFilename);

          channelPaths[idx] = textureFilename;
        }
      }

      return channelPaths[idx];
    }

    System.Drawing.Bitmap GetSingleChannelImage(ArgbChannel channel)
    {
      using (System.Drawing.Bitmap originalBmp = GetOriginalBitmap())
      {
        int width = originalBmp.Width;
        int height = originalBmp.Height;

        int countPixels = width * height;

        int[] resolvedPixels = new int[countPixels];

        System.Drawing.Rectangle bmpRectangle = new System.Drawing.Rectangle(0, 0, width, height);

        //Fetch original image pixels
        {
          var originalBitmapData = originalBmp.LockBits(bmpRectangle, System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

          System.Runtime.InteropServices.Marshal.Copy(originalBitmapData.Scan0, resolvedPixels, 0, countPixels);

          originalBmp.UnlockBits(originalBitmapData);
        }

        //Get single channel image
        //Use raw ints, System.Drawing.Color allocates a CGColor per instance on Mac
        Parallel.For(0, countPixels, i =>
        {
          resolvedPixels[i] = GetArgbFromChannel(resolvedPixels[i], channel);
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

    private static int GetArgbFromChannel(int argb, ArgbChannel channel)
    {
      int value;

      switch (channel)
      {
        case ArgbChannel.Red:
          value = (argb >> 16) & 0xFF;
          break;
        case ArgbChannel.Green:
          value = (argb >> 8) & 0xFF;
          break;
        case ArgbChannel.Blue:
          value = argb & 0xFF;
          break;
        case ArgbChannel.Alpha:
          value = (argb >> 24) & 0xFF;
          break;
        default:
          return argb;
      }

      return unchecked((int)0xFF000000) | (value << 16) | (value << 8) | value;
    }

  }
}
