namespace FoxyLink
{
    public class GlobalConfiguration : IGlobalConfiguration
    {
        public static IGlobalConfiguration Configuration { get; } = new GlobalConfiguration();

        internal GlobalConfiguration()
        {
        }
    }
}
