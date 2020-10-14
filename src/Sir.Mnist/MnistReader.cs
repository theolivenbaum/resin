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

        public IEnumerable<IImage> Read()
        {
            using (var labelFile = new FileStream(_labelFileName, FileMode.Open))
            using (var imageFile = new FileStream(_imageFileName, FileMode.Open))
            using (var labelReader = new BinaryReader(labelFile))
            using (var imageReader = new BinaryReader(imageFile))
            {
                int discard1 = imageReader.ReadInt32();
                int numImages = imageReader.ReadInt32WithCorrectEndianness();
                int numRows = imageReader.ReadInt32WithCorrectEndianness();
                int numCols = imageReader.ReadInt32WithCorrectEndianness();
                int discard2 = labelReader.ReadInt32WithCorrectEndianness();
                int numLabels = labelReader.ReadInt32WithCorrectEndianness();
                int numOfDimensions = numRows * numCols;

                for (int di = 0; di < numImages; ++di)
                {
                    byte[] pixels = new byte[numOfDimensions];

                    for (int i = 0; i < numOfDimensions; ++i)
                    {
                        byte b = imageReader.ReadByte();
                        pixels[i] = b;
                    }

                    byte label = labelReader.ReadByte();

                    yield return new MnistImage(pixels, label);
                }
            }
        }
    }
}
