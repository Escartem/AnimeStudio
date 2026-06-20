using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace AnimeStudio
{
    public static class SpriteHelper
    {
        public static SKBitmap GetImage(this Sprite m_Sprite)
        {
            if (m_Sprite.m_SpriteAtlas != null && m_Sprite.m_SpriteAtlas.TryGet(out var m_SpriteAtlas))
            {
                if (m_SpriteAtlas.m_RenderDataMap.TryGetValue(m_Sprite.m_RenderDataKey, out var spriteAtlasData) && spriteAtlasData.texture.TryGet(out var m_Texture2D))
                {
                    return CutImage(m_Sprite, m_Texture2D, spriteAtlasData.textureRect, spriteAtlasData.textureRectOffset, spriteAtlasData.downscaleMultiplier, spriteAtlasData.settingsRaw);
                }
            }
            else
            {
                if (m_Sprite.m_RD.texture.TryGet(out var m_Texture2D))
                {
                    return CutImage(m_Sprite, m_Texture2D, m_Sprite.m_RD.textureRect, m_Sprite.m_RD.textureRectOffset, m_Sprite.m_RD.downscaleMultiplier, m_Sprite.m_RD.settingsRaw);
                }
            }
            return null;
        }

        private static SKBitmap CutImage(Sprite m_Sprite, Texture2D m_Texture2D, Rectf textureRect, Vector2 textureRectOffset, float downscaleMultiplier, SpriteSettings settingsRaw)
        {
            var originalImage = m_Texture2D.ConvertToImage(false);
            if (originalImage != null)
            {
                try
                {
                    if (downscaleMultiplier > 0f && downscaleMultiplier != 1f)
                    {
                        var width = (int)(m_Texture2D.m_Width / downscaleMultiplier);
                        var height = (int)(m_Texture2D.m_Height / downscaleMultiplier);
                        var resizedImage = originalImage.Resize(width, height);
                        originalImage.Dispose();
                        originalImage = resizedImage;
                    }
                    var rectX = (int)Math.Floor(textureRect.x);
                    var rectY = (int)Math.Floor(textureRect.y);
                    var rectRight = (int)Math.Ceiling(textureRect.x + textureRect.width);
                    var rectBottom = (int)Math.Ceiling(textureRect.y + textureRect.height);
                    rectRight = Math.Min(rectRight, originalImage.Width);
                    rectBottom = Math.Min(rectBottom, originalImage.Height);
                    var rect = new SKRectI(rectX, rectY, rectRight, rectBottom);
                    var spriteImage = originalImage.Crop(rect);
                    if (settingsRaw.packed == 1)
                    {
                        //RotateAndFlip
                        switch (settingsRaw.packingRotation)
                        {
                            case SpritePackingRotation.FlipHorizontal:
                                spriteImage = ReplaceImage(spriteImage, spriteImage.FlipHorizontal());
                                break;
                            case SpritePackingRotation.FlipVertical:
                                spriteImage = ReplaceImage(spriteImage, spriteImage.FlipVertical());
                                break;
                            case SpritePackingRotation.Rotate180:
                                spriteImage = ReplaceImage(spriteImage, spriteImage.Rotate180());
                                break;
                            case SpritePackingRotation.Rotate90:
                                spriteImage = ReplaceImage(spriteImage, spriteImage.Rotate270());
                                break;
                        }
                    }

                    //Tight
                    if (settingsRaw.packingMode == SpritePackingMode.Tight)
                    {
                        try
                        {
                            var triangles = GetTriangles(m_Sprite.m_RD);
                            using (var path = BuildSpritePath(m_Sprite, textureRectOffset, triangles))
                            {
                                ApplyTightMask(spriteImage, path);
                            }
                            spriteImage = ReplaceImage(spriteImage, spriteImage.FlipVertical());
                            return spriteImage;
                        }
                        catch (Exception e)
                        {
                            Logger.Warning($"{m_Sprite.m_Name} Unable to render the packed sprite correctly.\n{e}");
                        }
                    }

                    //Rectangle
                    spriteImage = ReplaceImage(spriteImage, spriteImage.FlipVertical());
                    return spriteImage;
                }
                finally
                {
                    originalImage.Dispose();
                }
            }

            return null;
        }

        private static SKBitmap ReplaceImage(SKBitmap oldImage, SKBitmap newImage)
        {
            oldImage.Dispose();
            return newImage;
        }

        private static SKPath BuildSpritePath(Sprite m_Sprite, Vector2 textureRectOffset, Vector2[][] triangles)
        {
            var scale = m_Sprite.m_PixelsToUnits;
            var offsetX = m_Sprite.m_Rect.width * m_Sprite.m_Pivot.X - textureRectOffset.X;
            var offsetY = m_Sprite.m_Rect.height * m_Sprite.m_Pivot.Y - textureRectOffset.Y;
            var path = new SKPath
            {
                FillType = SKPathFillType.Winding
            };

            foreach (var triangle in triangles)
            {
                if (triangle.Length == 0)
                {
                    continue;
                }

                path.MoveTo(triangle[0].X * scale + offsetX, triangle[0].Y * scale + offsetY);
                for (int i = 1; i < triangle.Length; i++)
                {
                    path.LineTo(triangle[i].X * scale + offsetX, triangle[i].Y * scale + offsetY);
                }
                path.Close();
            }

            return path;
        }

        private static void ApplyTightMask(SKBitmap spriteImage, SKPath path)
        {
            using (var mask = new SKBitmap(new SKImageInfo(spriteImage.Width, spriteImage.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul)))
            using (var canvas = new SKCanvas(mask))
            using (var paint = new SKPaint
            {
                Color = SKColors.White,
                IsAntialias = false,
                Style = SKPaintStyle.Fill
            })
            {
                canvas.Clear(SKColors.Transparent);
                canvas.DrawPath(path, paint);

                var spriteBytes = spriteImage.ConvertToBytes();
                var maskBytes = mask.ConvertToBytes();
                var pixelCount = spriteImage.Width * spriteImage.Height;
                for (int i = 0; i < pixelCount; i++)
                {
                    if (maskBytes[(i * 4) + 3] == 0)
                    {
                        var offset = i * 4;
                        spriteBytes[offset] = 0;
                        spriteBytes[offset + 1] = 0;
                        spriteBytes[offset + 2] = 0;
                        spriteBytes[offset + 3] = 0;
                    }
                }

                ImageExtensions.CopyBgraToBitmap(spriteBytes, spriteImage);
            }
        }

        private static Vector2[][] GetTriangles(SpriteRenderData m_RD)
        {
            if (m_RD.vertices != null) //5.6 down
            {
                var vertices = m_RD.vertices.Select(x => (Vector2)x.pos).ToArray();
                var triangleCount = m_RD.indices.Length / 3;
                var triangles = new Vector2[triangleCount][];
                for (int i = 0; i < triangleCount; i++)
                {
                    var first = m_RD.indices[i * 3];
                    var second = m_RD.indices[i * 3 + 1];
                    var third = m_RD.indices[i * 3 + 2];
                    var triangle = new[] { vertices[first], vertices[second], vertices[third] };
                    triangles[i] = triangle;
                }
                return triangles;
            }
            else //5.6 and up
            {
                var triangles = new List<Vector2[]>();
                var m_VertexData = m_RD.m_VertexData;
                var m_Channel = m_VertexData.m_Channels[0]; //kShaderChannelVertex
                var m_Stream = m_VertexData.m_Streams[m_Channel.stream];
                using (var vertexReader = new EndianBinaryReader(new MemoryStream(m_VertexData.m_DataSize), EndianType.LittleEndian))
                {
                    using (var indexReader = new EndianBinaryReader(new MemoryStream(m_RD.m_IndexBuffer), EndianType.LittleEndian))
                    {
                        foreach (var subMesh in m_RD.m_SubMeshes)
                        {
                            vertexReader.BaseStream.Position = m_Stream.offset + subMesh.firstVertex * m_Stream.stride + m_Channel.offset;

                            var vertices = new Vector2[subMesh.vertexCount];
                            for (int v = 0; v < subMesh.vertexCount; v++)
                            {
                                vertices[v] = new Vector3(vertexReader.ReadSingle(), vertexReader.ReadSingle(), vertexReader.ReadSingle());
                                vertexReader.BaseStream.Position += m_Stream.stride - 12;
                            }

                            indexReader.BaseStream.Position = subMesh.firstByte;

                            var triangleCount = subMesh.indexCount / 3u;
                            for (int i = 0; i < triangleCount; i++)
                            {
                                var first = indexReader.ReadUInt16() - subMesh.firstVertex;
                                var second = indexReader.ReadUInt16() - subMesh.firstVertex;
                                var third = indexReader.ReadUInt16() - subMesh.firstVertex;
                                var triangle = new[] { vertices[first], vertices[second], vertices[third] };
                                triangles.Add(triangle);
                            }
                        }
                    }
                }
                return triangles.ToArray();
            }
        }
    }
}
