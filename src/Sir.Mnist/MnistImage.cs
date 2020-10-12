using System.Text;

namespace Sir.Mnist
{
    public class MnistImage : IImage
    {
        public byte[][] Pixels { get; }

        public object DisplayName { get; }

        public MnistImage(byte[][] pixels, byte label)
        {
            Pixels = pixels;
            DisplayName = label;
        }

        public string Print()
        {
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

        public override string ToString()
        {
            return DisplayName?.ToString();
        }
    }
}
