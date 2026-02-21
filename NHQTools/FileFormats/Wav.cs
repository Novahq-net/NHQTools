using System;
using System.IO;
using System.Text;
using System.Diagnostics;

// NHQTools Libraries
using NHQTools.Extensions;

// ReSharper disable IdentifierTypo

namespace NHQTools.FileFormats
{
    public static class Wav
    {
        // Public
        public const int HeaderLen = 44;
        public const int MinExpectedLen = HeaderLen + 2;

        // Constants
        private const ushort FMT_WPCM = 1;
        private const ushort FMT_IEEE_FLOAT = 3;
        private const ushort FMT_ADPCM = 17;

        private const int FMT_CHUNK_SZ = 16;
        private const ushort TARGET_BPS = 16;

        // ADPCM tables
        private static readonly int[] ImaIndexTable =
        {
            -1, -1, -1, -1,  2,  4,  6,  8,
            -1, -1, -1, -1,  2,  4,  6,  8
        };

        private static readonly int[] ImaStepTable =
        {
            7, 8, 9, 10, 11, 12, 13, 14, 16, 17,
            19, 21, 23, 25, 28, 31, 34, 37, 41, 45,
            50, 55, 60, 66, 73, 80, 88, 97, 107, 118,
            130, 143, 157, 173, 190, 209, 230, 253, 279, 307,
            337, 371, 408, 449, 494, 544, 598, 658, 724, 796,
            876, 963, 1060, 1166, 1282, 1411, 1552, 1707, 1878, 2066,
            2272, 2499, 2749, 3024, 3327, 3660, 4026, 4428, 4871, 5358,
            5894, 6484, 7132, 7845, 8630, 9493, 10442, 11487, 12635, 13899,
            15289, 16818, 18500, 20350, 22385, 24623, 27086, 29794, 32767
        };

        ////////////////////////////////////////////////////////////////////////////////////
        public static readonly Encoding DefaultEnc = Encoding.ASCII;

        private static readonly byte[] WaveId = DefaultEnc.GetBytes("WAVE");

        ////////////////////////////////////////////////////////////////////////////////////
        private static readonly FormatDef Def;

        ////////////////////////////////////////////////////////////////////////////////////
        static Wav() => Def = Definitions.GetFormatDef(FileType.WAV);

        ////////////////////////////////////////////////////////////////////////////////////
        #region ToWav

        // Older games use uncompressed PCM WAV, newer ones use compressed formats (some are also BFC wrapped)
        public static byte[] ToWav(FileInfo file, Encoding enc = null)
        {
            enc = enc ?? DefaultEnc;

            if (file == null)
                throw new ArgumentNullException(nameof(file), "File cannot be null.");

            return !file.Exists
                ? throw new FileNotFoundException($"File '{file.FullName}' not found.", file.FullName)
                : ToWav(File.ReadAllBytes(file.FullName), enc);

        }

        public static byte[] ToWav(byte[] fileData, Encoding enc = null)
        {
            enc = enc ?? DefaultEnc;

            if (fileData == null || fileData.Length < MinExpectedLen)
                throw new InvalidDataException("Data is empty or too short.");

            var reader = new ByteReader(fileData, enc);

            // Header
            var magic = reader.ReadBytes(Def.MagicBytes.Length);

            // Verify Magic Bytes
            if (!magic.Matches(Def.MagicBytes))
                throw new InvalidDataException($"Invalid file signature. Expected '{Def.MagicBytes.AsString()}', got '{magic.AsString()}'");

            _ = reader.ReadUInt32(); // chunk size

            // Verify WAVE ID
            var waveId = reader.ReadBytes(4);

            if (!waveId.Matches(WaveId))
                throw new InvalidDataException($"Invalid waveId. Expected '{WaveId.AsString()}', got '{waveId.AsString()}'");

            ushort wFormatTag = 0;
            ushort nChannels = 0;
            uint nSamplesPerSec = 0;
            ushort nBlockAlign = 0;
            ushort wBitsPerSample = 0;
            ushort samplesPerBlock = 0; // ADPCM fmt extras (only used when wFormatTag == 17)

            byte[] data = null;

            // chunk header (4-byte ID + 4-byte size = 8 bytes)
            // data must be at least 8 bytes to contain a chunk header
            while (reader.Position + 8 <= reader.Length)
            {
                var ckId = reader.ReadString(4);
                var ckSize = reader.ReadUInt32();

                if (ckSize > int.MaxValue)
                    throw new InvalidDataException("WAV decode error: Chunk size exceeds maximum supported size.");

                switch (ckId)
                {
                    case "fmt ":
                        {
                            wFormatTag = reader.ReadUInt16();
                            nChannels = reader.ReadUInt16();
                            nSamplesPerSec = reader.ReadUInt32();
                            _ = reader.ReadUInt32(); // nAvgBytesPerSec
                            nBlockAlign = reader.ReadUInt16();
                            wBitsPerSample = reader.ReadUInt16();

                            Debug.WriteLine("AudioFormat = " + wFormatTag);
                            Debug.WriteLine("BitsPerSample = " + wBitsPerSample);
                            Debug.WriteLine("Channels = " + nChannels);
                            Debug.WriteLine("SampleRate = " + nSamplesPerSec);
                            Debug.WriteLine("BlockAlign = " + nBlockAlign);

                            // If fmt has extras, read cbSize and possibly SamplesPerBlock
                            if (ckSize > FMT_CHUNK_SZ)
                            {
                                // WAVEFORMATEX: cbSize
                                var cbSize = reader.ReadUInt16();

                                // For IMA ADPCM, fmt extension typically includes SamplesPerBlock (2 bytes)
                                if (wFormatTag == FMT_ADPCM && cbSize >= 2 && ckSize >= FMT_CHUNK_SZ + 2 + 2)
                                {
                                    samplesPerBlock = reader.ReadUInt16();
                                    Debug.WriteLine("SamplesPerBlock = " + samplesPerBlock);

                                    // skip any remaining extra fmt bytes beyond SamplesPerBlock
                                    var remaining = (int)ckSize - (FMT_CHUNK_SZ + 2 + 2);
                                    if (remaining > 0)
                                        reader.Skip(remaining);
                                }
                                else
                                {
                                    // skip rest of extras
                                    var remaining = (int)ckSize - (FMT_CHUNK_SZ + 2);
                                    if (remaining > 0)
                                        reader.Skip(remaining);
                                }
                            }

                            break;
                        }

                    case "data":
                        data = reader.ReadBytes((int)ckSize);
                        break;
                    default:
                        reader.Skip(ckSize); // RIFF chunks are padded to even sizes
                        break;
                }

                //if ((ckSize & 1) == 1)
                if ((ckSize & 1) == 1 && reader.Position < reader.Length)
                    reader.Skip(1);

            }

            if (data == null)
                throw new InvalidDataException("Missing data chunk.");

            // No conversion needed
            if (wFormatTag == FMT_WPCM && wBitsPerSample == TARGET_BPS)
                return fileData;

            // Convert to PCM16
            var pcm = ToPcm(wFormatTag, wBitsPerSample, nChannels, nBlockAlign, samplesPerBlock, data, enc);

            // Wrap into a PCM WAV for SoundPlayer
            return CreateWav(pcm, nChannels, nSamplesPerSec, enc);
        }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region ToPcm / CreateWav
        private static byte[] ToPcm(ushort format, ushort bits, ushort channels, ushort blockAlign, ushort samplesPerBlock, byte[] data, Encoding enc)
        {
            switch (format)
            {
                case FMT_WPCM:
                    {
                        var bytesPerSample = bits / 8;

                        if (bytesPerSample == 0)
                            throw new NotSupportedException($"WAV decode error: Unsupported PCM depth: {bits}");

                        var sampleCount = data.Length / bytesPerSample;
                        var writer = new ByteWriter(sampleCount * 2, enc);

                        if (bits == 16)
                        {
                            writer.Write(data);
                            return writer.ToArray();
                        }

                        DecodeLpcm(writer, bits, data);
                        return writer.ToArray();
                    }

                case FMT_IEEE_FLOAT:
                    {
                        var sampleCount = data.Length / 4;
                        var writer = new ByteWriter(sampleCount * 2, enc);

                        DecodeFloat(writer, data);
                        return writer.ToArray();
                    }

                case FMT_ADPCM:
                    {
                        if (bits != 4)
                            throw new NotSupportedException("Unsupported WAV format: ADPCM must be 4 bits per sample.");

                        if (channels == 0)
                            throw new NotSupportedException("Unsupported WAV format: Invalid channel count.");

                        if (blockAlign == 0)
                            throw new NotSupportedException("Unsupported WAV format: Invalid block align for ADPCM format");

                        // If SamplesPerBlock is missing, derive it (works for valid IMA ADPCM WAV)
                        // SamplesPerBlock = ((blockAlign - 4*channels) * 2 / channels) + 1
                        if (samplesPerBlock == 0)
                            samplesPerBlock = (ushort)((blockAlign - 4 * channels) * 2 / channels + 1);

                        return DecodeAdpcm(channels, blockAlign, samplesPerBlock, data, enc);
                    }

                default:
                    throw new NotSupportedException($"Unsupported WAV format: {format}");

            }
        }

        ////////////////////////////////////////////////////////////////////////////////////
        private static byte[] CreateWav(byte[] pcm, ushort channels, uint rate, Encoding enc)
        {
            var blockAlign = (ushort)(channels * (TARGET_BPS / 8));
            var byteRate = rate * blockAlign;

            // 44 byte header + PCM data
            var writer = new ByteWriter(44 + pcm.Length, enc);

            // RIFF header
            writer.Write(Def.MagicBytes);
            writer.Write(36 + pcm.Length);
            writer.Write(WaveId);

            // fmt chunk
            writer.WriteString("fmt ");
            writer.Write(FMT_CHUNK_SZ);
            writer.Write(FMT_WPCM);
            writer.Write(channels);
            writer.Write(rate);
            writer.Write(byteRate);
            writer.Write(blockAlign);
            writer.Write(TARGET_BPS);

            // data chunk
            writer.WriteString("data");
            writer.Write(pcm.Length);
            writer.Write(pcm);

            return writer.ToArray();
        }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Decoders
        private static void DecodeLpcm(ByteWriter writer, ushort bits, byte[] pcmData)
        {
            switch (bits)
            {
                case 8:
                    foreach (var t in pcmData)
                        writer.Write((short)((t - 128) << 8));
                    break;

                case 24:
                    for (var i = 0; i + 2 < pcmData.Length; i += 3)
                    {
                        var v = (pcmData[i + 2] << 24) | (pcmData[i + 1] << 16) | (pcmData[i] << 8);
                        writer.Write((short)(v >> 16));
                    }
                    break;

                case 32:
                    for (var i = 0; i + 3 < pcmData.Length; i += 4)
                    {
                        var v = pcmData.ReadInt32Le(i);
                        writer.Write((short)(v >> 16));
                    }
                    break;

                case 16:
                    writer.Write(pcmData);
                    break;

                default:
                    throw new InvalidDataException($"WAV decode error: Invalid LPCM depth: {bits}");
            }
        }

        ////////////////////////////////////////////////////////////////////////////////////
        private static void DecodeFloat(ByteWriter writer, byte[] floatData)
        {
            for (var i = 0; i + 3 < floatData.Length; i += 4)
            {
                var f = floatData.ReadFloat(i);

                if (f > 1f) f = 1f;
                else if (f < -1f) f = -1f;

                writer.Write((short)(f * short.MaxValue));
            }
        }

        ////////////////////////////////////////////////////////////////////////////////////
        private static byte[] DecodeAdpcm(ushort channels, ushort blockAlign, ushort samplesPerBlock, byte[] adpcmData, Encoding enc)
        {
            // Number of whole blocks present
            var blockCount = adpcmData.Length / blockAlign;
            if (blockCount <= 0)
                throw new InvalidDataException($"WAV decode error: Invalid ADPCM block count: {blockCount}");

            // Each block produces samplesPerBlock samples per channel,
            // and each sample is 2 bytes in output PCM16
            var outBytes = (long)blockCount * samplesPerBlock * channels * 2;

            if (outBytes > int.MaxValue)
                throw new InvalidDataException("WAV decode error: ADPCM output exceeds maximum buffer size.");

            var writer = new ByteWriter((int)outBytes, enc);
            var srcPos = 0;

            // Per-channel state
            var predictor = new short[channels];
            var index = new int[channels];

            for (var b = 0; b < blockCount; b++)
            {
                var blockStart = srcPos;
                var blockEnd = blockStart + blockAlign;

                // Read block header(s): 4 bytes per channel
                for (var ch = 0; ch < channels; ch++)
                {
                    if (srcPos + 4 > adpcmData.Length)
                        throw new InvalidDataException("WAV decode error: Truncated IMA ADPCM block header.");

                    predictor[ch] = (short)(adpcmData[srcPos] | (adpcmData[srcPos + 1] << 8));
                    index[ch] = adpcmData[srcPos + 2];
                    // byte 3 is reserved
                    srcPos += 4;

                    if (index[ch] < 0) index[ch] = 0;
                    if (index[ch] > 88) index[ch] = 88;
                }

                // Write initial predictor sample(s) for this block (interleaved by channel)
                for (var ch = 0; ch < channels; ch++)
                    writer.Write(predictor[ch]);

                // Compressed payload bytes for this block
                var payloadBytes = blockEnd - srcPos;

                if (payloadBytes < 0)
                    throw new InvalidDataException("WAV decode error: Corrupt IMA ADPCM block alignment.");

                var nibblesTotal = payloadBytes * 2;
                var nibblesNeeded = (samplesPerBlock - 1) * channels;

                // Some files may have padding; only decode what we can safely consume.
                var nibblesToDecode = nibblesTotal;

                if (nibblesToDecode > nibblesNeeded)
                    nibblesToDecode = nibblesNeeded;

                // Nibble reader over the block payload
                var nibbleIndex = 0;

                // Decode sample frames: each frame outputs one sample per channel
                var frames = nibblesToDecode / channels;
                for (var s = 0; s < frames; s++)
                {
                    for (var ch = 0; ch < channels; ch++)
                    {
                        if (nibbleIndex >= nibblesToDecode)
                            break;

                        var code = ReadNibble(adpcmData, srcPos, nibbleIndex);
                        nibbleIndex++;

                        predictor[ch] = DecodeNibble(predictor[ch], ref index[ch], code);
                        writer.Write(predictor[ch]);
                    }

                }

                // Advance srcPos to end of block (skip any remaining bytes/nibbles)
                srcPos = blockEnd;
            }

            return writer.ToArray();
        }

        private static int ReadNibble(byte[] data, int payloadStart, int nibbleIndex)
        {
            var byteIndex = payloadStart + (nibbleIndex >> 1);

            int b = data[byteIndex];

            // Standard IMA ADPCM WAV uses low nibble first, then high nibble
            if ((nibbleIndex & 1) == 0)
                return b & 0x0F;

            // high nibble
            return (b >> 4) & 0x0F;
        }

        private static short DecodeNibble(short predictor, ref int index, int code)
        {
            var step = ImaStepTable[index];

            // The 4-bit code has a sign bit (0x8) and magnitude bits (0x7).
            // The magnitude bits indicate how much to change the predictor,
            // scaled by the current step size.
            // The sign bit indicates the direction of the change.

            var diff = step >> 3;

            if ((code & 1) != 0)
                diff += step >> 2;

            if ((code & 2) != 0)
                diff += step >> 1;

            if ((code & 4) != 0)
                diff += step;

            int pred = predictor;

            if ((code & 8) != 0)
                pred -= diff;
            else
                pred += diff;

            if (pred > 32767)
                pred = 32767;

            else if (pred < -32768)
                pred = -32768;

            // Update index based on the code, then clamp to valid range
            index += ImaIndexTable[code];

            index = Math.Max(0, Math.Min(88, index));

            return (short)pred;
        }

        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Is Valid Format
        internal static bool Validator(string fileName, byte[] data)
        {
            if (data == null || data.Length < MinExpectedLen)
                return false;

            return data.StartsWith(Def.MagicBytes) && data.Matches(WaveId, 8);
        }
        #endregion

    }

}