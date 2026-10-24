/// <summary>The original run owner supplies the staged/restored floor; recipes own no mutable run state.</summary>
public interface IEcologyFloorContext
{
    int EcologyFloorNumber { get; }
}
