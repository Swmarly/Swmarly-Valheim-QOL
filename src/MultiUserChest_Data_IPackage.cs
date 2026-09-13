// Common wire-format contract for the multi-user chest protocol.
namespace SwmarlyValheimQOL {
    public interface IPackage {
        ZPackage WriteToPackage();

#if DEBUG
        void PrintDebug();
#endif
    }
}
