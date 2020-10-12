namespace Sir
{
    /// <summary>
    /// Pixels are organized row-wise.
    /// Pixel values are 0 to 255. 0 means background(white), 255 means foreground(black). 
    /// </summary>
    public interface IImage
    {
        byte[][] Pixels { get; }
        object DisplayName { get; }
    }
}
