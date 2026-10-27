namespace RaidCheck.Core;

public interface IRandomState { void Push(int seed); void Pop(); }
public static class RandomIsolation
{
    public static T Run<T>(IRandomState state, int seed, Func<T> action)
    {
        state.Push(seed);
        try { return action(); }
        finally { state.Pop(); }
    }
}
