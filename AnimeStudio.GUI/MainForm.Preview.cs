using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AnimeStudio.App;
using AnimeStudio.GUI.Core.Audio;
using AnimeStudio.GUI.Core.Preview;
using OpenTK.Graphics.OpenGL;

namespace AnimeStudio.GUI
{
    using ImageFormat = System.Drawing.Imaging.ImageFormat;
    using MeshRenderer = AnimeStudio.GUI.Core.Preview.MeshRenderer;
    using PixelFormat = System.Drawing.Imaging.PixelFormat;

    partial class MainForm
    {
        private readonly TextureChannels channels = new();
        private readonly AudioPlayer audio = new();
        private readonly MeshRenderer meshRenderer = new();
        private bool audioReady;
        private bool glReady;
        private MeshData pendingMesh;
        private bool seeking;

        private AssetRow selectedRow;
        private PreviewResult currentPreview;
        private CancellationTokenSource previewCts;
        private Bitmap previewBitmap;
        private string infoText;

        private PrivateFontCollection fontCollection;
        private IntPtr fontMemory;
        private IntPtr fontResource;

        private int mdx, mdy;
        private bool lmdown, rmdown;

        private void InitializePreview()
        {
            audioReady = audio.Initialize();
            audio.Error += message => { SetStatus(message); ResetAudioControls(); };
            audio.Volume = FMODvolumeBar.Value / 10f;
        }

        private void DisposePreview()
        {
            ClearPreview();
            audio.Dispose();
            if (glReady)
            {
                glControl.MakeCurrent();
                meshRenderer.Dispose();
            }
        }

        private void OnAssetSelected(AssetRow row)
        {
            selectedRow = row;
            if (tabControl2.SelectedIndex == 1)
                _ = ShowDumpAsync(row);
            if (Settings.EnablePreview)
                _ = ShowPreviewAsync(row);
            else
                ClearPreview();
        }

        private void OnPreviewToggled()
        {
            if (Settings.EnablePreview && selectedRow != null)
                _ = ShowPreviewAsync(selectedRow);
            else
                ClearPreview();
        }

        private async Task ShowPreviewAsync(AssetRow row)
        {
            previewCts?.Cancel();
            var cts = previewCts = new CancellationTokenSource();
            ClearPreview();
            SetStatus(string.Empty);

            var task = session.Previews.GetPreviewAsync(row, cts.Token);
            if (await Task.WhenAny(task, Task.Delay(200)) != task)
                SetStatus($"Loading preview of {row.Name}...");

            PreviewResult result;
            try
            {
                result = await task;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            if (cts.IsCancellationRequested || selectedRow != row)
                return;

            DisplayPreview(result);
        }

        private void DisplayPreview(PreviewResult result)
        {
            currentPreview = result;
            infoText = result.InfoText;
            SetStatus(result.Status ?? string.Empty);
            switch (result)
            {
                case TexturePreview texture:
                    RenderTexture(texture);
                    break;
                case TextPreview text:
                    textPreviewBox.Text = text.Text?.ReplaceLineEndings("\r\n");
                    textPreviewBox.Visible = true;
                    break;
                case FontPreview font:
                    ShowFont(font.Data);
                    break;
                case AudioPreview clip:
                    ShowAudio(clip);
                    break;
                case MeshPreview mesh:
                    ShowMesh(mesh.Mesh);
                    break;
            }
            UpdateInfoLabel();
        }

        private void ClearPreview()
        {
            currentPreview = null;
            infoText = null;
            previewPanel.BackgroundImage = Properties.Resources.preview;
            previewPanel.BackgroundImageLayout = ImageLayout.Center;
            previewPanel.ContextMenuStrip = null;
            previewBitmap?.Dispose();
            previewBitmap = null;
            classTextBox.Visible = false;
            assetInfoLabel.Visible = false;
            assetInfoLabel.Text = null;
            textPreviewBox.Visible = false;
            textPreviewBox.Text = string.Empty;
            fontPreviewBox.Visible = false;
            ReleaseFont();
            FMODpanel.Visible = false;
            audio.Unload();
            ResetAudioControls();
            pendingMesh = null;
            if (glControl.Visible || meshRenderer.HasMesh)
            {
                glControl.Visible = false;
                if (glReady)
                {
                    glControl.MakeCurrent();
                    meshRenderer.SetMesh(null);
                }
            }
        }

        private void UpdateInfoLabel()
        {
            var text = infoText;
            if (currentPreview is TexturePreview { ShowChannels: true })
                text += "\n" + channels.Describe();
            assetInfoLabel.Text = text;
            assetInfoLabel.Visible = Settings.DisplayInfo && !string.IsNullOrEmpty(text);
        }

        private async Task ShowDumpAsync(AssetRow row)
        {
            dumpTextBox.Text = string.Empty;
            try
            {
                var dump = await session.Previews.GetDumpAsync(row, CancellationToken.None);
                if (selectedRow == row)
                    dumpTextBox.Text = dump?.ReplaceLineEndings("\r\n");
            }
            catch (Exception e)
            {
                Logger.Error($"Dump {row.Type}:{row.Name} error\r\n{e.Message}");
            }
        }

        private void tabControl2_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tabControl2.SelectedIndex == 1 && selectedRow != null)
                _ = ShowDumpAsync(selectedRow);
        }

        #region Texture
        private void RenderTexture(TexturePreview texture)
        {
            var bitmap = new Bitmap(texture.Width, texture.Height, PixelFormat.Format32bppArgb);
            var data = bitmap.LockBits(new Rectangle(0, 0, texture.Width, texture.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                unsafe
                {
                    var stride = texture.Width * 4;
                    for (var y = 0; y < texture.Height; y++)
                    {
                        var source = new ReadOnlySpan<byte>(texture.Pixels, y * stride, stride);
                        var target = new Span<byte>((byte*)data.Scan0 + (long)y * data.Stride, stride);
                        if (texture.ShowChannels)
                            channels.Apply(source, target);
                        else
                            source.CopyTo(target);
                    }
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }

            previewBitmap?.Dispose();
            previewBitmap = bitmap;
            previewPanel.BackgroundImage = bitmap;
            previewPanel.ContextMenuStrip = previewContextMenuStrip;
            previewPanel.BackgroundImageLayout = bitmap.Width > previewPanel.Width || bitmap.Height > previewPanel.Height ? ImageLayout.Zoom : ImageLayout.Center;
        }

        private void copyImageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (previewBitmap == null)
                return;
            var data = new DataObject();
            using var ms = new MemoryStream();
            previewBitmap.Save(ms, ImageFormat.Png);
            data.SetData("PNG", false, ms);
            data.SetImage(previewBitmap);
            Clipboard.SetDataObject(data, true);
        }
        #endregion

        #region Font
        [DllImport("gdi32.dll")]
        private static extern bool RemoveFontMemResourceEx(IntPtr handle);

        // The RichTextBox only renders fonts registered with GDI, the PrivateFontCollection gives us the family.
        private void ShowFont(byte[] fontData)
        {
            ReleaseFont();
            fontMemory = Marshal.AllocCoTaskMem(fontData.Length);
            Marshal.Copy(fontData, 0, fontMemory, fontData.Length);
            uint count = 0;
            fontResource = FontHelper.AddFontMemResourceEx(fontMemory, (uint)fontData.Length, IntPtr.Zero, ref count);
            if (fontResource == IntPtr.Zero)
            {
                ReleaseFont();
                SetStatus("Unsupported font for preview. Try to export.");
                return;
            }

            fontCollection = new PrivateFontCollection();
            fontCollection.AddMemoryFont(fontMemory, fontData.Length);
            if (fontCollection.Families.Length == 0)
            {
                ReleaseFont();
                SetStatus("Unsupported font for preview. Try to export.");
                return;
            }

            var family = fontCollection.Families[0];
            (int start, int length, float size)[] samples =
            {
                (0, 80, 16), (81, 56, 12), (138, 56, 18), (195, 56, 24),
                (252, 56, 36), (309, 56, 48), (366, 56, 60), (423, 55, 72),
            };
            foreach (var (start, length, size) in samples)
            {
                fontPreviewBox.SelectionStart = start;
                fontPreviewBox.SelectionLength = length;
                fontPreviewBox.SelectionFont = new System.Drawing.Font(family, size, FontStyle.Regular);
            }
            fontPreviewBox.Visible = true;
        }

        private void ReleaseFont()
        {
            fontCollection?.Dispose();
            fontCollection = null;
            if (fontResource != IntPtr.Zero)
            {
                RemoveFontMemResourceEx(fontResource);
                fontResource = IntPtr.Zero;
            }
            if (fontMemory != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(fontMemory);
                fontMemory = IntPtr.Zero;
            }
        }
        #endregion

        #region Audio
        private void ShowAudio(AudioPreview clip)
        {
            if (!audioReady || !audio.Load(clip.Data, clip.Length))
                return;
            FMODpanel.Visible = true;
            FMODinfoLabel.Text = audio.Frequency + " Hz";
            FMODloopButton.Checked = audio.Loop;
            UpdateAudio();
        }

        private void ResetAudioControls()
        {
            FMODprogressBar.Value = 0;
            FMODtimerLabel.Text = "0:00.0 / 0:00.0";
            FMODstatusLabel.Text = "Stopped";
            FMODpauseButton.Text = "Pause";
            FMODinfoLabel.Text = "";
        }

        private void UpdateAudio()
        {
            if (!audio.IsLoaded)
                return;
            audio.Update();
            var state = audio.State;
            FMODstatusLabel.Text = state.ToString();
            FMODpauseButton.Text = state == PlaybackState.Paused ? "Resume" : "Pause";
            if (seeking)
                return;
            var position = state == PlaybackState.Stopped ? 0 : audio.PositionMs;
            FMODtimerLabel.Text = $"{AudioPlayer.FormatTime(position)} / {AudioPlayer.FormatTime(audio.LengthMs)}";
            FMODprogressBar.Value = audio.LengthMs == 0 ? 0 : (int)Math.Min(FMODprogressBar.Maximum, (long)position * FMODprogressBar.Maximum / audio.LengthMs);
        }

        private uint SeekPosition => (uint)((long)audio.LengthMs * FMODprogressBar.Value / FMODprogressBar.Maximum);

        private void FMODplayButton_Click(object sender, EventArgs e) => audio.Play(audio.State == PlaybackState.Stopped ? SeekPosition : 0);

        private void FMODpauseButton_Click(object sender, EventArgs e) => audio.TogglePause();

        private void FMODstopButton_Click(object sender, EventArgs e)
        {
            audio.Stop();
            FMODprogressBar.Value = 0;
        }

        private void FMODloopButton_CheckedChanged(object sender, EventArgs e) => audio.Loop = FMODloopButton.Checked;

        private void FMODvolumeBar_ValueChanged(object sender, EventArgs e) => audio.Volume = FMODvolumeBar.Value / 10f;

        private void FMODprogressBar_Scroll(object sender, EventArgs e)
        {
            FMODtimerLabel.Text = $"{AudioPlayer.FormatTime(SeekPosition)} / {AudioPlayer.FormatTime(audio.LengthMs)}";
        }

        private void FMODprogressBar_MouseDown(object sender, MouseEventArgs e) => seeking = true;

        private void FMODprogressBar_MouseUp(object sender, MouseEventArgs e)
        {
            seeking = false;
            if (audio.State != PlaybackState.Stopped)
                audio.PositionMs = SeekPosition;
        }
        #endregion

        #region Model
        private void ShowMesh(MeshData mesh)
        {
            glControl.Visible = true;
            if (!glReady)
            {
                pendingMesh = mesh;
                return;
            }
            glControl.MakeCurrent();
            meshRenderer.SetMesh(mesh);
            meshRenderer.Resize(glControl.Width, glControl.Height);
            glControl.Invalidate();
            SetStatus("Using OpenGL Version: " + GL.GetString(StringName.Version) + "\n" + currentPreview?.Status);
        }

        private void glControl_Load(object sender, EventArgs e)
        {
            glControl.MakeCurrent();
            meshRenderer.Initialize();
            meshRenderer.Resize(glControl.Width, glControl.Height);
            glReady = true;
            if (pendingMesh != null)
            {
                meshRenderer.SetMesh(pendingMesh);
                pendingMesh = null;
                glControl.Invalidate();
            }
        }

        private void preview_Resize(object sender, EventArgs e)
        {
            if (glReady && glControl.Visible)
            {
                glControl.MakeCurrent();
                meshRenderer.Resize(glControl.Width, glControl.Height);
                glControl.Invalidate();
            }
        }

        private void glControl_Paint(object sender, PaintEventArgs e)
        {
            if (!glReady)
                return;
            glControl.MakeCurrent();
            meshRenderer.Render();
            glControl.SwapBuffers();
        }

        private void glControl_MouseWheel(object sender, MouseEventArgs e)
        {
            meshRenderer.Zoom(e.Delta);
            glControl.Invalidate();
        }

        private void glControl_MouseDown(object sender, MouseEventArgs e)
        {
            mdx = e.X;
            mdy = e.Y;
            lmdown |= e.Button == MouseButtons.Left;
            rmdown |= e.Button == MouseButtons.Right;
        }

        private void glControl_MouseMove(object sender, MouseEventArgs e)
        {
            if (!lmdown && !rmdown)
                return;
            float dx = mdx - e.X;
            float dy = mdy - e.Y;
            mdx = e.X;
            mdy = e.Y;
            if (lmdown)
                meshRenderer.Rotate(dx, dy);
            if (rmdown)
                meshRenderer.Pan(dx, dy);
            glControl.Invalidate();
        }

        private void glControl_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                lmdown = false;
            if (e.Button == MouseButtons.Right)
                rmdown = false;
        }
        #endregion

        private void AnimeStudioForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (!e.Control)
                return;

            if (glControl.Visible && glReady)
            {
                glControl.MakeCurrent();
                switch (e.KeyCode)
                {
                    case Keys.W:
                        meshRenderer.CycleWireframe();
                        break;
                    case Keys.S:
                        meshRenderer.ToggleShade();
                        break;
                    case Keys.N:
                        meshRenderer.ToggleNormals();
                        break;
                    default:
                        return;
                }
                glControl.Invalidate();
            }
            else if (currentPreview is TexturePreview { ShowChannels: true } texture)
            {
                var channel = e.KeyCode switch
                {
                    Keys.B => 0,
                    Keys.G => 1,
                    Keys.R => 2,
                    Keys.A => 3,
                    _ => -1,
                };
                if (channel < 0)
                    return;
                channels.Toggle(channel);
                RenderTexture(texture);
                UpdateInfoLabel();
            }
        }
    }
}
