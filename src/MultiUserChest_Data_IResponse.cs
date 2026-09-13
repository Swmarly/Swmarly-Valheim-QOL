// Response contract shared by add, remove, consume, drop, and move replies.
namespace SwmarlyValheimQOL {
    public interface IResponse : IPackage {
        int SourceID { get; set; }
        bool Success { get; set; }
        int Amount { get; set; }
    }
}
