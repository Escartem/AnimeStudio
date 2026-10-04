using System;
using System.Runtime.InteropServices;

namespace AnimeStudio.GUI.Core.Audio
{
    public enum PlaybackState
    {
        Stopped,
        Playing,
        Paused
    }

    // FMOD playback for the audio preview. Must be pumped with Update() from a UI timer.
    public sealed class AudioPlayer : IDisposable
    {
        private FMOD.System system;
        private FMOD.SoundGroup masterGroup;
        private FMOD.Sound sound;
        private FMOD.Sound subSound;
        private FMOD.Channel channel;
        private bool loop;
        private float volume = 0.8f;

        public event Action<string> Error;

        public uint LengthMs { get; private set; }
        public int Frequency { get; private set; }
        public bool IsLoaded => Playable != null;
        private FMOD.Sound Playable => subSound ?? sound;

        public bool Initialize()
        {
            if (Check(FMOD.Factory.System_Create(out system)))
                return false;
            if (Check(system.getVersion(out var version)))
                return false;
            if (version < FMOD.VERSION.number)
            {
                Logger.Error($"Error!  You are using an old version of FMOD {version:X}.  This program requires {FMOD.VERSION.number:X}.");
                return false;
            }
            if (Check(system.init(2, FMOD.INITFLAGS.NORMAL, IntPtr.Zero)))
                return false;
            if (Check(system.getMasterSoundGroup(out masterGroup)))
                return false;
            return !Check(masterGroup.setVolume(volume));
        }

        public bool Load(byte[] data, uint length)
        {
            Unload();
            var exinfo = new FMOD.CREATESOUNDEXINFO();
            exinfo.cbsize = Marshal.SizeOf(exinfo);
            exinfo.length = length;

            if (Check(system.createSound(data, FMOD.MODE.OPENMEMORY | LoopMode, ref exinfo, out sound)))
                return false;
            if (sound.getNumSubSounds(out var count) == FMOD.RESULT.OK && count > 0 && sound.getSubSound(0, out var sub) == FMOD.RESULT.OK)
                subSound = sub;
            if (Check(Playable.getLength(out var lengthMs, FMOD.TIMEUNIT.MS)))
                return false;
            LengthMs = lengthMs;

            // Start paused to read the frequency without playing.
            if (Check(system.playSound(Playable, null, true, out channel)))
                return false;
            if (Check(channel.getFrequency(out var frequency)))
                return false;
            Frequency = (int)frequency;
            return true;
        }

        public void Unload()
        {
            channel?.stop();
            channel = null;
            // Releasing the parent releases its subsounds too.
            if (sound != null && sound.isValid())
                sound.release();
            sound = null;
            subSound = null;
            LengthMs = 0;
            Frequency = 0;
        }

        public PlaybackState State
        {
            get
            {
                if (channel == null || channel.isPlaying(out var playing) != FMOD.RESULT.OK || !playing)
                    return PlaybackState.Stopped;
                channel.getPaused(out var paused);
                return paused ? PlaybackState.Paused : PlaybackState.Playing;
            }
        }

        public uint PositionMs
        {
            get => channel != null && channel.getPosition(out var ms, FMOD.TIMEUNIT.MS) == FMOD.RESULT.OK ? ms : 0;
            set
            {
                if (channel != null)
                    IgnoreInvalid(channel.setPosition(Math.Min(value, LengthMs), FMOD.TIMEUNIT.MS));
            }
        }

        // Play restarts from the given position (the old Play button behaviour).
        public void Play(uint fromMs = 0)
        {
            if (!IsLoaded)
                return;
            channel?.stop();
            if (Check(system.playSound(Playable, null, false, out channel)))
                return;
            if (fromMs > 0)
                PositionMs = fromMs;
        }

        public void TogglePause()
        {
            var state = State;
            if (state == PlaybackState.Stopped)
                return;
            Check(channel.setPaused(state == PlaybackState.Playing));
        }

        public void Stop()
        {
            if (channel != null)
                IgnoreInvalid(channel.stop());
        }

        public bool Loop
        {
            get => loop;
            set
            {
                loop = value;
                if (IsLoaded)
                    Check(Playable.setMode(LoopMode));
                if (State != PlaybackState.Stopped)
                    Check(channel.setMode(LoopMode));
            }
        }

        // 0 to 1
        public float Volume
        {
            get => volume;
            set
            {
                volume = Math.Clamp(value, 0f, 1f);
                if (masterGroup != null)
                    Check(masterGroup.setVolume(volume));
            }
        }

        public void Update()
        {
            system?.update();
        }

        public static string FormatTime(uint ms) => $"{ms / 60000}:{ms / 1000 % 60:00}.{ms / 100 % 10}";

        private FMOD.MODE LoopMode => loop ? FMOD.MODE.LOOP_NORMAL : FMOD.MODE.LOOP_OFF;

        private void IgnoreInvalid(FMOD.RESULT result)
        {
            if (result != FMOD.RESULT.ERR_INVALID_HANDLE)
                Check(result);
        }

        private bool Check(FMOD.RESULT result)
        {
            if (result == FMOD.RESULT.OK)
                return false;
            Error?.Invoke($"FMOD error! {result} - {FMOD.Error.String(result)}");
            return true;
        }

        public void Dispose()
        {
            Unload();
            system?.release();
            system = null;
        }
    }
}
