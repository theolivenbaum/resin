using System.Collections.Generic;
using System.IO;

namespace Sir.Mnist
{
    public class MnistReader
    {
        private readonly string _imageFileName;
        private readonly string _labelFileName;

        public MnistReader(string imageFileName, string labelFileName)
        {
            _imageFileName = imageFileName;
            _labelFileName = labelFileName;
        }

        public IEnumerable<MnistImage> Read()
        {
            using (var labelFile = new FileStream(_labelFileName, FileMode.Open))
            using (var imageFile = new FileStream(_imageFileName, FileMode.Open))
            using (var labels = new BinaryReader(labelFile))
            using (var images = new BinaryReader(imageFile))
            {
                int discard1 = images.ReadInt32();
                int numImages = images.ReadInt32WithCorrectEndianness();
                int numRows = images.ReadInt32WithCorrectEndianness();
                int numCols = images.ReadInt32WithCorrectEndianness();
                int discard2 = labels.ReadInt32WithCorrectEndianness();
                int numLabels = labels.ReadInt32WithCorrectEndianness();

                byte[][] pixels = new byte[numRows][];

                for (int i = 0; i < pixels.Length; ++i)
                    pixels[i] = new byte[numCols];

                for (int di = 0; di < numImages; ++di)
                {
                    for (int i = 0; i < 28; ++i)
                    {
                        for (int j = 0; j < 28; ++j)
                        {
                            byte b = images.ReadByte();
                            pixels[i][j] = b;
                        }
                    }

                    byte label = labels.ReadByte();

                    yield return new MnistImage(pixels, label);
                }
            }
        }
    }
}
