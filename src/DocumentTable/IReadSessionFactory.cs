namespace DocumentTable
{
    public interface IReadSessionFactory
    {
        IReadSession OpenReadSession(long version);
        IReadSession OpenReadSession(SegmentInfo version);
    }
}
