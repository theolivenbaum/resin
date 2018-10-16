namespace Sir
{
    public interface IReader : IPlugin
    {
       Result Read(Query query);
    }
}
