using System;
using System.Text;

namespace AnimeStudio.GUI.Core.Preview
{
    // BGRA channel visibility for the texture viewer.
    public sealed class TextureChannels
    {
        private static readonly char[] names = { 'B', 'G', 'R', 'A' };
        private readonly bool[] enabled = { true, true, true, true };

        public bool this[int channel]
        {
            get => enabled[channel];
            set => enabled[channel] = value;
        }

        public bool AllEnabled => enabled[0] && enabled[1] && enabled[2] && enabled[3];

        public void Toggle(int channel) => enabled[channel] = !enabled[channel];

        public void Reset() => Array.Fill(enabled, true);

        public string Describe()
        {
            var sb = new StringBuilder("Channels: ");
            var any = false;
            for (int i = 0; i < 4; i++)
            {
                if (enabled[i])
                {
                    sb.Append(names[i]);
                    any = true;
                }
            }
            if (!any)
                sb.Append("None");
            return sb.ToString();
        }

        // A single color channel plus alpha shows the color channel as white so it can be read as a mask.
        public void Apply(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            if (AllEnabled)
            {
                source.CopyTo(destination);
                return;
            }

            var count = 0;
            foreach (var on in enabled)
                count += on ? 1 : 0;
            var hidden = count == 1 && enabled[3] ? byte.MaxValue : byte.MinValue;
            bool b = enabled[0], g = enabled[1], r = enabled[2], a = enabled[3];
            for (int i = 0; i + 3 < source.Length; i += 4)
            {
                destination[i] = b ? source[i] : hidden;
                destination[i + 1] = g ? source[i + 1] : hidden;
                destination[i + 2] = r ? source[i + 2] : hidden;
                destination[i + 3] = a ? source[i + 3] : byte.MaxValue;
            }
        }
    }
}
