namespace SwmarlyValheimQOL {
    public interface IPackage {
        ZPackage WriteToPackage();

#if DEBUG
        void PrintDebug();
#endif
    }
}

