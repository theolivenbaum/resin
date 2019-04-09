namespace Sir.Store
{
    public static class CosineSimilarity
    {
        public static readonly (float identicalAngle, float foldAngle) Term = (0.999f, 0.65f);
        public static readonly (float identicalAngle, float foldAngle) Document = (0.97f, 0.65f);
    }
}
