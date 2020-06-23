namespace MassiveJobs.Core
{
    public class VoidArgs
    {
        public static readonly VoidArgs Instance;

        static VoidArgs()
        {
            Instance = new VoidArgs();
        }

        private VoidArgs()
        {
        }
    }
}
