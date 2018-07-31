namespace Sir
{
    public interface IQueryParser : IPlugin
    {
        Query Parse(string query);
    }
}
