using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace Yappity
{
    // Records the default microphone as 16 kHz mono 16-bit PCM, the format Whisper
    // works in. Uses the built-in Windows waveIn API.
    internal sealed class Recorder
    {
        public const int SampleRate = 16000;
        public const int BytesPerSecond = SampleRate * 2;
        const int BufferCount = 8;
        const int BufferBytes = BytesPerSecond / 10; // 100 ms per buffer

        static readonly int HeaderSize = Marshal.SizeOf(typeof(Native.WAVEHDR));

        IntPtr handle = IntPtr.Zero;
        IntPtr[] headers;
        AutoResetEvent signal;
        Thread worker;
        volatile bool running;
        int next;
        MemoryStream pcm;
        readonly object gate = new object();

        // Called on a background thread with the loudness (0..1) of each 100 ms chunk.
        public Action<float> LevelChanged;

        public bool IsRecording { get { return handle != IntPtr.Zero; } }

        public void Start()
        {
            if (IsRecording) return;

            var fmt = new Native.WAVEFORMATEX();
            fmt.wFormatTag = 1; // PCM
            fmt.nChannels = 1;
            fmt.nSamplesPerSec = SampleRate;
            fmt.wBitsPerSample = 16;
            fmt.nBlockAlign = 2;
            fmt.nAvgBytesPerSec = BytesPerSecond;
            fmt.cbSize = 0;

            signal = new AutoResetEvent(false);
            IntPtr h;
            int r = Native.waveInOpen(out h, Native.WAVE_MAPPER, ref fmt,
                signal.SafeWaitHandle.DangerousGetHandle(), IntPtr.Zero, Native.CALLBACK_EVENT);
            if (r != 0)
            {
                signal.Dispose();
                throw new InvalidOperationException(
                    "Could not open the microphone (error " + r + "). Check that a mic is plugged in and that " +
                    "Windows Settings > Privacy > Microphone allows desktop apps.");
            }
            handle = h;

            headers = new IntPtr[BufferCount];
            for (int i = 0; i < BufferCount; i++)
            {
                var hdr = new Native.WAVEHDR();
                hdr.lpData = Marshal.AllocHGlobal(BufferBytes);
                hdr.dwBufferLength = BufferBytes;
                IntPtr p = Marshal.AllocHGlobal(HeaderSize);
                Marshal.StructureToPtr(hdr, p, false);
                headers[i] = p;
                Native.waveInPrepareHeader(handle, p, HeaderSize);
                Native.waveInAddBuffer(handle, p, HeaderSize);
            }

            pcm = new MemoryStream();
            next = 0;
            running = true;
            worker = new Thread(Loop);
            worker.IsBackground = true;
            worker.Name = "Yappity recorder";
            worker.Start();
            Native.waveInStart(handle);
        }

        void Loop()
        {
            while (running)
            {
                signal.WaitOne(200);
                Harvest(true);
            }
        }

        // Copies finished buffers into the PCM stream, in order, and hands them back
        // to Windows when we are still recording.
        void Harvest(bool requeue)
        {
            lock (gate)
            {
                for (int n = 0; n < BufferCount; n++)
                {
                    IntPtr p = headers[next];
                    var hdr = (Native.WAVEHDR)Marshal.PtrToStructure(p, typeof(Native.WAVEHDR));
                    if ((hdr.dwFlags & Native.WHDR_DONE) == 0) break;

                    int count = (int)hdr.dwBytesRecorded;
                    if (count > 0)
                    {
                        var chunk = new byte[count];
                        Marshal.Copy(hdr.lpData, chunk, 0, count);
                        pcm.Write(chunk, 0, count);
                        var cb = LevelChanged;
                        if (cb != null) cb(Loudness(chunk, count));
                    }

                    if (requeue)
                    {
                        hdr.dwFlags &= ~Native.WHDR_DONE;
                        hdr.dwBytesRecorded = 0;
                        Marshal.StructureToPtr(hdr, p, false);
                        Native.waveInAddBuffer(handle, p, HeaderSize);
                    }
                    next = (next + 1) % BufferCount;
                }
            }
        }

        // Stops recording and returns everything captured as raw PCM.
        public byte[] Stop()
        {
            if (!IsRecording) return new byte[0];

            running = false;
            signal.Set();
            worker.Join();

            Native.waveInStop(handle);
            Native.waveInReset(handle); // marks every pending buffer as done
            Harvest(false);

            foreach (IntPtr p in headers)
            {
                Native.waveInUnprepareHeader(handle, p, HeaderSize);
                var hdr = (Native.WAVEHDR)Marshal.PtrToStructure(p, typeof(Native.WAVEHDR));
                Marshal.FreeHGlobal(hdr.lpData);
                Marshal.FreeHGlobal(p);
            }
            Native.waveInClose(handle);
            handle = IntPtr.Zero;
            headers = null;
            signal.Dispose();

            byte[] result = pcm.ToArray();
            pcm = null;
            return result;
        }

        static float Loudness(byte[] data, int count)
        {
            double sum = 0;
            int samples = count / 2;
            for (int i = 0; i + 1 < count; i += 2)
            {
                short s = (short)(data[i] | (data[i + 1] << 8));
                sum += (double)s * s;
            }
            if (samples == 0) return 0;
            double rms = Math.Sqrt(sum / samples) / 32768.0;
            // Square root makes quiet speech visible on the level bars.
            return (float)Math.Min(1.0, Math.Sqrt(rms) * 2.2);
        }

        // Loudest sample in the recording, used to skip silent clips (Whisper
        // tends to invent words like "Thank you." for silence).
        public static int Peak(byte[] pcm)
        {
            int peak = 0;
            for (int i = 0; i + 1 < pcm.Length; i += 2)
            {
                int s = Math.Abs((int)(short)(pcm[i] | (pcm[i + 1] << 8)));
                if (s > peak) peak = s;
            }
            return peak;
        }

        public static byte[] ToWav(byte[] pcm)
        {
            using (var ms = new MemoryStream(pcm.Length + 44))
            using (var w = new BinaryWriter(ms))
            {
                w.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
                w.Write(36 + pcm.Length);
                w.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E' });
                w.Write(new[] { (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
                w.Write(16);
                w.Write((short)1);          // PCM
                w.Write((short)1);          // mono
                w.Write(SampleRate);
                w.Write(BytesPerSecond);
                w.Write((short)2);          // block align
                w.Write((short)16);         // bits per sample
                w.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
                w.Write(pcm.Length);
                w.Write(pcm);
                w.Flush();
                return ms.ToArray();
            }
        }
    }
}
