namespace Sir
{
    public interface IConfigurationProvider
    {
        string Get(string key);
        string[] GetMany(string key);
    }
}
