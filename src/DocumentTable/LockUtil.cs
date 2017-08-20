using System;
using System.IO;

namespace Resin.Documents
{
    public static class LockUtil
    {
        private static readonly Random Rnd;
        private static long Ticks;

        static LockUtil()
        {
            Rnd = new Random();
            Ticks = DateTime.Now.Ticks;
        }


        public static string GetFirstIndexFileNameInChronologicalOrder(string directory)
        {
            var files = Directory.GetFiles(directory, "*.ix");
            if (files.Length == 0) return null;
            return files[0];
        }

        public static bool TryAquireWriteLock(string directory, out FileStream lockFile)
        {
            lockFile = null;

            var fileName = Path.Combine(directory, "write.lock");
            try
            {
                lockFile = new FileStream(
                                fileName, FileMode.CreateNew, FileAccess.Write,
                                FileShare.None, 4, FileOptions.DeleteOnClose);
                return true;
            }
            catch (IOException)
            {
                if (lockFile != null)
                {
                    lockFile.Dispose();
                    lockFile = null;
                }
                return false;
            }
        }

        public static long GetNextChronologicalFileId()
        {
            return GetTicks();
        }

        private static Int64 GetTicks()
        {
            var count = Rnd.Next(1, 4);
            for (int i = 0; i < count; i++)
            {
                if (Rnd.Next(1, 6).Equals(count)) break;
            }
            return Ticks++;
        }
    }
}
