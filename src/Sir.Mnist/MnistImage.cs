using System.Text;

namespace Sir.Mnist
{
    public class MnistImage
    {
        public byte[][] Pixels;

        public byte Label { get; }

        public MnistImage(byte[][] pixels, byte label)
        {
            Pixels = pixels;
            Label = label;
        }

        public override string ToString()
        {
            // Pixels are organized row-wise.
            // Pixel values are 0 to 255. 0 means background(white), 255 means foreground(black). 

            var s = new StringBuilder();

            for (int i = 0; i < Pixels.Length; ++i)
            {
                for (int j = 0; j < Pixels[i].Length; ++j)
                {
                    if (Pixels[i][j] == 0)
                        s.Append(' '); // white

                    else if (Pixels[i][j] >= 250)
                        s.Append('O'); // black-ish

                    else
                        s.Append('.'); // gray
                }
                s.Append('\n');
            }

            return s.ToString();
        }
    }
}
