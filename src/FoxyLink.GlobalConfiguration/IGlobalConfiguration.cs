namespace FoxyLink
{
    public interface IGlobalConfiguration
    {
    }

    public interface IGlobalConfiguration<out T> : IGlobalConfiguration
    {
        T Entry { get; }
    }
}
