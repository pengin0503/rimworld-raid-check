namespace RaidCheck.Tests;

static class Program
{
    private static int passed, failed;
    public static int Main()
    {
        CoreTests.Run();
        AdapterTests.Run();
        UiTests.Run();
        BatchTests.Run();
        Console.WriteLine($"{passed} passed; {failed} failed");
        return failed == 0 ? 0 : 1;
    }
    internal static void Test(string name, Action body)
    {
        try { body(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failed++; Console.WriteLine($"FAIL {name}: {ex.Message}"); }
    }
    internal static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception($"expected {expected}, got {actual}");
    }
    internal static void True(bool value) { if (!value) throw new Exception("expected true"); }
    internal static void Throws<T>(Action body) where T : Exception
    {
        try { body(); } catch (T) { return; }
        throw new Exception("expected " + typeof(T).Name);
    }
}
