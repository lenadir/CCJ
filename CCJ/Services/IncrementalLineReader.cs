using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CCJ.Services
{
    /// <summary>
    /// Reads a growing text file a few lines at a time, remembering where it stopped. It never
    /// locks the file, so robocopy can keep writing while the progress bar reads.
    ///
    /// Only complete lines are returned. A line robocopy is half way through writing is left for
    /// the next call. If the file shrinks, or its beginning changes, it has been rewritten (the
    /// script reuses one log name for every attempt) and reading starts again from the top;
    /// <see cref="WasReset"/> tells the caller.
    /// </summary>
    public class IncrementalLineReader
    {
        /// <summary>
        /// Long enough to reach the "Started" line in a robocopy header, which differs for every
        /// attempt, so a rewritten log is told apart from the old one even if it is the same length.
        /// </summary>
        private const int SignatureLength = 600;

        private readonly string _path;
        private readonly Encoding _encoding;
        private long _position;
        private byte[] _signature = new byte[0];

        public IncrementalLineReader(string path, Encoding encoding)
        {
            _path = path;
            _encoding = encoding;
        }

        /// <summary>True if the last call found that the file had been rewritten.</summary>
        public bool WasReset { get; private set; }

        public string Path { get { return _path; } }

        public List<string> ReadNewLines()
        {
            WasReset = false;
            List<string> lines = new List<string>();

            using (FileStream stream = new FileStream(
                _path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                long length = stream.Length;

                if (_position > 0 && (length < _position || !SignatureStillMatches(stream)))
                {
                    _position = 0;
                    _signature = new byte[0];
                    WasReset = true;
                }

                if (length <= _position) return lines;

                stream.Seek(_position, SeekOrigin.Begin);
                byte[] buffer = new byte[length - _position];
                int total = 0;
                while (total < buffer.Length)
                {
                    int read = stream.Read(buffer, total, buffer.Length - total);
                    if (read <= 0) break;
                    total += read;
                }

                int lastBreak = Array.LastIndexOf(buffer, (byte)'\n', total - 1);
                if (lastBreak < 0) return lines;

                string text = _encoding.GetString(buffer, 0, lastBreak + 1);
                _position += lastBreak + 1;

                foreach (string piece in text.Split('\n'))
                {
                    lines.Add(piece.TrimEnd('\r'));
                }
                // Splitting "a\nb\n" gives a trailing empty entry that is not a real line.
                if (lines.Count > 0 && lines[lines.Count - 1].Length == 0)
                    lines.RemoveAt(lines.Count - 1);

                RememberSignature(stream);
            }

            return lines;
        }

        private bool SignatureStillMatches(FileStream stream)
        {
            if (_signature.Length == 0) return true;
            if (stream.Length < _signature.Length) return false;

            byte[] now = new byte[_signature.Length];
            stream.Seek(0, SeekOrigin.Begin);
            int total = 0;
            while (total < now.Length)
            {
                int read = stream.Read(now, total, now.Length - total);
                if (read <= 0) return false;
                total += read;
            }

            for (int i = 0; i < now.Length; i++)
            {
                if (now[i] != _signature[i]) return false;
            }
            return true;
        }

        private void RememberSignature(FileStream stream)
        {
            long wanted = Math.Min(SignatureLength, _position);
            if (_signature.Length >= wanted) return;

            byte[] head = new byte[wanted];
            stream.Seek(0, SeekOrigin.Begin);
            int total = 0;
            while (total < head.Length)
            {
                int read = stream.Read(head, total, head.Length - total);
                if (read <= 0) break;
                total += read;
            }
            if (total == head.Length) _signature = head;
        }
    }
}
