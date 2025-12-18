using AnimeStudio;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace L2DSpineExporter
{
    public static class StaticUtil
    {
        public static void SaveTextures(this IEnumerable<Texture2D> textures, string folder, ToolStripStatusLabel toolStripStatusLabel)
        {
            foreach (var texture2D in textures)
            {
                SaveTexture(texture2D, folder,toolStripStatusLabel);
            }
        }

        public static void SaveTexture(this Texture2D texture2D, string folder, ToolStripStatusLabel toolStripStatusLabel)
        {
            var texture2dConverter = new Texture2DConverter(texture2D);
            var buff = ArrayPool<byte>.Shared.Rent(texture2D.m_Width * texture2D.m_Height * 4);
            try
            {
                if (texture2dConverter.DecodeTexture2D(buff))
                {
                    //textures.Add($"textures/{texture2D.m_Name}.png");
                    var image = Image.LoadPixelData<Bgra32>(buff, texture2D.m_Width, texture2D.m_Height);
                    using (image)
                    {
                        string savePath = Path.Combine(folder, texture2D.m_Name + ".png");
                        using var file = File.OpenWrite(savePath);
                        image.Mutate(x => x.Flip(FlipMode.Vertical));
                        image.WriteToStream(file, ImageFormat.Png);
                        Console.WriteLine($"Save {savePath} successfully.");
                        if(toolStripStatusLabel != null) toolStripStatusLabel.Text = $"Save {savePath} successfully.";
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buff);
            }
        }
    }
}
