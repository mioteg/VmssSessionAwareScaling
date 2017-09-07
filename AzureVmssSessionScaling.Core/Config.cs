namespace AzureVmssSessionScaling.Core
{
    public static class Config
    {

        public const int MaxLoadPerInstance = 5;
        public const int MinFreeVmssCapacity = 2;
        public const int MinActiveInstances = 1;

        public const string DirectoryId = "<id-of-azure-ad-directory>";
        public const string ApplicationId = "<id-of-application-in-azure-ad>";
        public const string ApplicationKey = "<id-of-application-in-azure-ad>";
        public const string ResourceGroupName = "CustomVmssScaleDemo";
        public const string VmssName = "demovmms";
    }
}
