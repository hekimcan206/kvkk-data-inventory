using System.Security.Cryptography;
using System.Text;

namespace KvkkInventory.Masking;

/// <summary>
/// A keyed, deterministic stream of uniform digits/letters derived from HMAC-SHA256(key, type ‖ value).
/// The same input always yields the same stream (so masked values stay joinable across tables and runs); without
/// the key the stream is indistinguishable from random, so the original cannot be recomputed or looked up.
/// </summary>
internal sealed class Pseudorandom
{
    private readonly byte[] _seed;
    private byte[] _block;
    private int _position;
    private uint _counter;

    public Pseudorandom(byte[] key, string domain, string normalizedValue)
    {
        _seed = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(domain + '\u001f' + normalizedValue));
        _block = _seed;
    }

    /// <summary>The raw 32-byte PRF output, used for tokens.</summary>
    public ReadOnlySpan<byte> Seed => _seed;

    /// <summary>Uniform integer in [0, exclusiveMax) by rejection sampling (no modulo bias).</summary>
    public int Next(int exclusiveMax)
    {
        if (exclusiveMax is <= 0 or > 256)
        {
            throw new ArgumentOutOfRangeException(nameof(exclusiveMax));
        }

        var limit = 256 - (256 % exclusiveMax);
        while (true)
        {
            var b = NextByte();
            if (b < limit)
            {
                return b % exclusiveMax;
            }
        }
    }

    public char Digit() => (char)('0' + Next(10));

    public char NonZeroDigit() => (char)('1' + Next(9));

    public char Letter(string alphabet) => alphabet[Next(alphabet.Length)];

    private byte NextByte()
    {
        if (_position == _block.Length)
        {
            // Counter mode over the seed extends the stream for long values.
            Span<byte> input = stackalloc byte[_seed.Length + 4];
            _seed.CopyTo(input);
            BitConverter.TryWriteBytes(input[_seed.Length..], ++_counter);
            _block = SHA256.HashData(input);
            _position = 0;
        }

        return _block[_position++];
    }
}
