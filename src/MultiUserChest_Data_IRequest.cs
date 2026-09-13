// Request contract: identifies the source/target inventories and correlates
// the asynchronous server response with the originating client action.
namespace SwmarlyValheimQOL {
    public interface IRequest : IPackage {
        int RequestID { get; set; }
        Inventory SourceInventory { get; }
        Inventory TargetInventory { get; }
    }
}
